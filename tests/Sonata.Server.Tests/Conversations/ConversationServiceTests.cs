using Sonata.Server.Conversations;
using Sonata.Server.ModelProviders;
using Sonata.Server.Models;
using Sonata.Server.Repositories;
using Sonata.Server.Tests.ModelProviders;
using Sonata.Server.Tests.Persistence;

namespace Sonata.Server.Tests.Conversations;

[Collection(PostgreSqlCollection.Name)]
public sealed class ConversationServiceTests(PostgreSqlFixture fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ContinuesConversationWithOrderedHistory()
    {
        await using var context = fixture.CreateDbContext();
        var conversationId = Guid.NewGuid();
        context.Sessions.Add(new Session
        {
            Id = conversationId
        });
        await context.SaveChangesAsync();
        
        var messageRepository = new MessageRepository(context);
        await messageRepository.AddMessageAsync(NewMessage(conversationId, "Earlier question", "user"));
        await messageRepository.AddMessageAsync(NewMessage(conversationId, "Earlier answer", "assistant"));

        var provider =
            new ScriptedModelProvider(new GeneratedResponse("Current answer", "assistant", "provider-response-123"));
        IConversationService service = new ConversationService(new SessionRepository(context), messageRepository, provider);
        
        var turn = await service.ContinueAsync(new ContinueConversationCommand(conversationId, "Current question"), CancellationToken.None);
        
        Assert.Equal(3, turn.UserMessage.Sequence);
        Assert.Equal(4, turn.AssistantMessage.Sequence);
        Assert.Equal("Current answer", turn.AssistantMessage.Content);

        var receivedRequest = Assert.IsType<GenerateResponseRequest>(provider.ReceivedRequest);
        Assert.Equal(new[]
        {
            "Earlier question",
            "Earlier answer",
            "Current question"
        }, receivedRequest.Messages.Select(message => message.Content));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProviderFailureDoesNotPersistAssistantMessage()
    {
        await using var context = fixture.CreateDbContext();
        var conversationId = Guid.NewGuid();
        var messageRepository = new MessageRepository(context);
        IConversationService service = new ConversationService(new SessionRepository(context), messageRepository, new ScriptedModelProvider.FailingModelProvider());
        
        await Assert.ThrowsAsync<ModelProviderException>(() => service.ContinueAsync(
            new ContinueConversationCommand(conversationId, "Please answer"),
            CancellationToken.None));

        var messages = await messageRepository.GetMessagesBySessionId(conversationId);

        var onlyMessage = Assert.Single(messages);
        Assert.Equal("user", onlyMessage.Role);
        Assert.Equal("Please answer", onlyMessage.Content);
    }
    
    private static Message NewMessage(Guid conversationId, string content, string role)
    {
        return new Message
        {
            SessionId = conversationId,
            Content = content,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}