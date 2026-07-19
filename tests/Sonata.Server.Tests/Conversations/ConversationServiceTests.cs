using Sonata.Server.Conversations;
using Sonata.Server.Data;
using Sonata.Server.ModelProviders;
using Sonata.Server.Models;
using Sonata.Server.Repositories;
using Sonata.Server.Retrieval;
using Sonata.Server.Tests.ModelProviders;
using Sonata.Server.Tests.Persistence;

namespace Sonata.Server.Tests.Conversations;

[Collection(PostgreSqlCollection.Name)]
public sealed class ConversationServiceTests(PostgreSqlFixture fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ContinuesConversationWithOrderedHistoryAndEmptyMemoryDiff()
    {
        await using var context = fixture.CreateDbContext();
        var movement = await AddMovementAsync(context, "Empty retrieval");
        var conversation = await AddConversationAsync(context, movement.Id);
        var messageRepository = new MessageRepository(context);

        await messageRepository.AddMessageAsync(
            NewMessage(conversation.Id, "Earlier question", "user"));
        await messageRepository.AddMessageAsync(
            NewMessage(conversation.Id, "Earlier answer", "assistant"));

        var provider = new ScriptedModelProvider(
            new GeneratedResponse(
                "Current answer",
                "assistant",
                "provider-response-123"));
        IConversationService service = CreateService(
            context,
            messageRepository,
            provider);

        var turn = await service.ContinueAsync(
            new ContinueConversationCommand(
                conversation.Id,
                "Current question"),
            CancellationToken.None);

        Assert.Equal(3, turn.UserMessage.Sequence);
        Assert.Equal(4, turn.AssistantMessage.Sequence);
        Assert.Equal("Current answer", turn.AssistantMessage.Content);
        Assert.Empty(turn.MemoryDiff);

        var receivedRequest = Assert.IsType<GenerateResponseRequest>(
            provider.ReceivedRequest);
        Assert.Equal(
            new[]
            {
                "Earlier question",
                "Earlier answer",
                "Current question"
            },
            receivedRequest.Messages.Select(message => message.Content));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ActiveMovementMemoryIsUsedAndRecordedInMemoryDiff()
    {
        await using var context = fixture.CreateDbContext();
        var movement = await AddMovementAsync(context, "Active retrieval");
        var memory = await AddMemoryAsync(
            context,
            movement.Id,
            "The backend uses C#.",
            MemoryLifecycleState.Active,
            new DateTimeOffset(2026, 7, 19, 1, 0, 0, TimeSpan.Zero));
        var conversation = await AddConversationAsync(context, movement.Id);
        var messageRepository = new MessageRepository(context);
        var provider = new ScriptedModelProvider(
            new GeneratedResponse(
                "Keep building the C# backend.",
                "assistant",
                "provider-response-memory"));
        IConversationService service = CreateService(
            context,
            messageRepository,
            provider);

        var turn = await service.ContinueAsync(
            new ContinueConversationCommand(
                conversation.Id,
                "What should I build next?"),
            CancellationToken.None);

        var request = Assert.IsType<GenerateResponseRequest>(
            provider.ReceivedRequest);
        Assert.Equal("system", request.Messages[0].Role);
        Assert.Contains("untrusted data", request.Messages[0].Content);
        Assert.Contains("The backend uses C#.", request.Messages[0].Content);
        Assert.Equal(
            "What should I build next?",
            request.Messages[1].Content);

        var diffItem = Assert.Single(turn.MemoryDiff);
        Assert.Equal(memory.Id, diffItem.MemoryId);
        Assert.Equal(memory.SourceNoteId, diffItem.SourceNoteId);
        Assert.Equal("ProjectContext", diffItem.Type);
        Assert.Equal(1, diffItem.Rank);
        Assert.Equal("MovementMatch", diffItem.Reason);

        var persistedUses = await messageRepository
            .GetMemoryUsesByResponseMessageIdAsync(
                turn.AssistantMessage.Id,
                CancellationToken.None);
        var persistedUse = Assert.Single(persistedUses);
        Assert.Equal(diffItem.MemoryId, persistedUse.MemoryId);
        Assert.Equal(diffItem.Rank, persistedUse.Rank);
        Assert.Equal(diffItem.Reason, persistedUse.Reason);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ArchivedMemoryIsExcluded()
    {
        await using var context = fixture.CreateDbContext();
        var movement = await AddMovementAsync(context, "Archived exclusion");
        await AddMemoryAsync(
            context,
            movement.Id,
            "Do not retrieve this archived claim.",
            MemoryLifecycleState.Archived,
            new DateTimeOffset(2026, 7, 19, 2, 0, 0, TimeSpan.Zero));
        var conversation = await AddConversationAsync(context, movement.Id);
        var messageRepository = new MessageRepository(context);
        var provider = new ScriptedModelProvider(
            new GeneratedResponse("No memory used.", "assistant", null));
        IConversationService service = CreateService(
            context,
            messageRepository,
            provider);

        var turn = await service.ContinueAsync(
            new ContinueConversationCommand(conversation.Id, "Answer me"),
            CancellationToken.None);

        var request = Assert.IsType<GenerateResponseRequest>(
            provider.ReceivedRequest);
        var onlyMessage = Assert.Single(request.Messages);
        Assert.Equal("user", onlyMessage.Role);
        Assert.Empty(turn.MemoryDiff);

        var persistedUses = await messageRepository
            .GetMemoryUsesByResponseMessageIdAsync(
                turn.AssistantMessage.Id,
                CancellationToken.None);
        Assert.Empty(persistedUses);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MemoryFromAnotherMovementIsExcluded()
    {
        await using var context = fixture.CreateDbContext();
        var currentMovement = await AddMovementAsync(
            context,
            "Current Movement");
        var otherMovement = await AddMovementAsync(
            context,
            "Other Movement");
        await AddMemoryAsync(
            context,
            otherMovement.Id,
            "This belongs to another Movement.",
            MemoryLifecycleState.Active,
            new DateTimeOffset(2026, 7, 19, 3, 0, 0, TimeSpan.Zero));
        var conversation = await AddConversationAsync(
            context,
            currentMovement.Id);
        var messageRepository = new MessageRepository(context);
        var provider = new ScriptedModelProvider(
            new GeneratedResponse("Scoped answer.", "assistant", null));
        IConversationService service = CreateService(
            context,
            messageRepository,
            provider);

        var turn = await service.ContinueAsync(
            new ContinueConversationCommand(conversation.Id, "Answer me"),
            CancellationToken.None);

        var request = Assert.IsType<GenerateResponseRequest>(
            provider.ReceivedRequest);
        Assert.Single(request.Messages);
        Assert.Empty(turn.MemoryDiff);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SelectsAtMostFiveMemoriesInDeterministicOrder()
    {
        await using var context = fixture.CreateDbContext();
        var movement = await AddMovementAsync(context, "Bounded retrieval");

        for (var number = 1; number <= 6; number++)
        {
            await AddMemoryAsync(
                context,
                movement.Id,
                $"Memory {number}",
                MemoryLifecycleState.Active,
                new DateTimeOffset(
                    2026, 7, 19, 4, number, 0, TimeSpan.Zero));
        }

        var conversation = await AddConversationAsync(context, movement.Id);
        var messageRepository = new MessageRepository(context);
        var provider = new ScriptedModelProvider(
            new GeneratedResponse("Bounded answer.", "assistant", null));
        IConversationService service = CreateService(
            context,
            messageRepository,
            provider);

        var turn = await service.ContinueAsync(
            new ContinueConversationCommand(conversation.Id, "Use context"),
            CancellationToken.None);

        Assert.Equal(5, turn.MemoryDiff.Count);
        Assert.Equal(
            new[]
            {
                "Memory 6",
                "Memory 5",
                "Memory 4",
                "Memory 3",
                "Memory 2"
            },
            turn.MemoryDiff.Select(item => item.Text));
        Assert.Equal(
            new[] { 1, 2, 3, 4, 5 },
            turn.MemoryDiff.Select(item => item.Rank));

        var request = Assert.IsType<GenerateResponseRequest>(
            provider.ReceivedRequest);
        Assert.Contains(
            "\"claim\":\"Memory 6\"",
            request.Messages[0].Content);
        Assert.DoesNotContain(
            "\"claim\":\"Memory 1\"",
            request.Messages[0].Content);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProviderFailureDoesNotPersistAssistantMessageOrMemoryUse()
    {
        await using var context = fixture.CreateDbContext();
        var movement = await AddMovementAsync(context, "Provider failure");
        await AddMemoryAsync(
            context,
            movement.Id,
            "This selected Memory must not create a false use.",
            MemoryLifecycleState.Active,
            new DateTimeOffset(2026, 7, 19, 5, 0, 0, TimeSpan.Zero));
        var conversation = await AddConversationAsync(context, movement.Id);
        var messageRepository = new MessageRepository(context);
        var service = CreateService(
            context,
            messageRepository,
            new ScriptedModelProvider.FailingModelProvider());

        await Assert.ThrowsAsync<ModelProviderException>(() =>
            service.ContinueAsync(
                new ContinueConversationCommand(
                    conversation.Id,
                    "Please answer"),
                CancellationToken.None));

        var messages = await messageRepository
            .GetMessagesByConversationId(conversation.Id);

        var onlyMessage = Assert.Single(messages);
        Assert.Equal("user", onlyMessage.Role);
        Assert.Equal("Please answer", onlyMessage.Content);
    }

    private static IConversationService CreateService(
        ApplicationDbContext context,
        IMessageRepository messageRepository,
        IModelProvider provider)
    {
        return new ConversationService(
            new ConversationRepository(context),
            messageRepository,
            new MemorySelector(context),
            provider);
    }

    private static async Task<Movement> AddMovementAsync(
        ApplicationDbContext context,
        string name)
    {
        var movement = new Movement
        {
            Id = Guid.NewGuid(),
            Name = name
        };

        context.Movements.Add(movement);
        await context.SaveChangesAsync();
        return movement;
    }

    private static async Task<Conversation> AddConversationAsync(
        ApplicationDbContext context,
        Guid movementId)
    {
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            MovementId = movementId
        };

        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();
        return conversation;
    }

    private static async Task<Memory> AddMemoryAsync(
        ApplicationDbContext context,
        Guid movementId,
        string text,
        MemoryLifecycleState lifecycleState,
        DateTimeOffset createdAt)
    {
        var sourceConversation = new Conversation
        {
            Id = Guid.NewGuid(),
            MovementId = movementId
        };
        var sourceMessage = new Message
        {
            Conversation = sourceConversation,
            Sequence = 1,
            Role = "user",
            Content = text,
            CreatedAt = createdAt
        };

        context.Messages.Add(sourceMessage);
        await context.SaveChangesAsync();

        var memory = new Memory
        {
            MovementId = movementId,
            SourceNote = new SourceNote
            {
                MessageId = sourceMessage.Id,
                Excerpt = text,
                CreatedAt = createdAt
            },
            Text = text,
            Type = MemoryType.ProjectContext,
            LifecycleState = lifecycleState,
            CreatedAt = createdAt
        };

        context.Memories.Add(memory);
        await context.SaveChangesAsync();
        return memory;
    }

    private static Message NewMessage(
        Guid conversationId,
        string content,
        string role)
    {
        return new Message
        {
            ConversationId = conversationId,
            Content = content,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}