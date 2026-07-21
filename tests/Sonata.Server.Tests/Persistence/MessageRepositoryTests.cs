using Microsoft.EntityFrameworkCore;
using Sonata.Server.Data;
using Sonata.Server.Repositories;
using Sonata.Server.Models;
using Sonata.Server.Identity;

namespace Sonata.Server.Tests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class MessageRepositoryTests(PostgreSqlFixture fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddsAndReturnsMessagesInSequenceOrder()
    {
        await using var context = fixture.CreateDbContext();
        var owned = await AddConversationAsync(context);
        var conversation = owned.Conversation;
        var userId = owned.UserId;
 
        var repository = new MessageRepository(context);

        await repository.AddMessageAsync(NewMessage(conversation.Id, "first"), CancellationToken.None);
        await repository.AddMessageAsync(NewMessage(conversation.Id, "second"), CancellationToken.None);
        await repository.AddMessageAsync(NewMessage(conversation.Id, "third"), CancellationToken.None);
        
        var messages = await repository.GetMessagesByConversationId(userId, conversation.Id, CancellationToken.None);
        
        Assert.Equal(new[] { 1, 2, 3 }, messages.Select(message => message.Sequence));
        Assert.Equal(new[] { "first", "second", "third" }, messages.Select(message => message.Content));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RejectsDuplicateSequenceWithinOneConversation()
    {
        await using var context = fixture.CreateDbContext();
        var owned = await AddConversationAsync(context);
        var conversation = owned.Conversation;

        context.AddRange(
            NewMessage(conversation.Id, "one", sequence: 1),
            NewMessage(conversation.Id, "duplicate", sequence: 1));
        
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RejectsMessageWithoutARealConversation()
    {
        await using var context = fixture.CreateDbContext();
        context.Add(NewMessage(Guid.NewGuid(), "Orphan", sequence: 1));
        
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
    
    [Fact]
    [Trait("Category", "Integration")]
    public async Task NeverReturnsForeignConversationHistory()
    {
        await using var context = fixture.CreateDbContext();
        var owned = await AddConversationAsync(context);
        var strangerEmail =
            $"stranger-{Guid.NewGuid():N}@example.com";
        var stranger = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = strangerEmail,
            NormalizedUserName = strangerEmail.ToUpperInvariant(),
            Email = strangerEmail,
            NormalizedEmail = strangerEmail.ToUpperInvariant()
        };
        context.Users.Add(stranger);
        await context.SaveChangesAsync();

        var repository = new MessageRepository(context);
        await repository.AddMessageAsync(
            NewMessage(
                owned.Conversation.Id,
                "Owner-only history"),
            CancellationToken.None);

        var foreign = await repository
            .GetMessagesByConversationId(
                stranger.Id,
                owned.Conversation.Id,
                CancellationToken.None);
        var owner = await repository
            .GetMessagesByConversationId(
                owned.UserId,
                owned.Conversation.Id,
                CancellationToken.None);

        Assert.Empty(foreign);
        Assert.Single(owner);
    }

    private static Message NewMessage(Guid conversationId, string content, int sequence = 0)
    {
        return new Message
        {
            ConversationId = conversationId,
            Content = content,
            Role = "user",
            Sequence = sequence,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
    
    private sealed record OwnedConversation(
        Guid UserId,
        Conversation Conversation);

    private static async Task<OwnedConversation>
        AddConversationAsync(ApplicationDbContext context)
    {
        var email = $"messages-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant()
        };
        var movement = new Movement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Message ordering"
        };
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            MovementId = movement.Id
        };

        context.AddRange(user, movement, conversation);
        await context.SaveChangesAsync();
        return new OwnedConversation(user.Id, conversation);
    }
}
