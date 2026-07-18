using Microsoft.EntityFrameworkCore;
using Sonata.Server.Repositories;
using Sonata.Server.Models;

namespace Sonata.Server.Tests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class MessageRepositoryTests(PostgreSqlFixture fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddsAndReturnsMessagesInSequenceOrder()
    {
        await using var context = fixture.CreateDbContext();
        var session = new Session { Id = Guid.NewGuid() };
        context.Sessions.Add(session);
        await context.SaveChangesAsync();

        var repository = new MessageRepository(context);

        await repository.AddMessageAsync(NewMessage(session.Id, "first"));
        await repository.AddMessageAsync(NewMessage(session.Id, "second"));
        await repository.AddMessageAsync(NewMessage(session.Id, "third"));
        
        var messages = await repository.GetMessagesBySessionId(session.Id);
        
        Assert.Equal(new[] { 1, 2, 3 }, messages.Select(message => message.Sequence));
        Assert.Equal(new[] { "first", "second", "third" }, messages.Select(message => message.Content));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RejectsDuplicateSequenceWithinOneSession()
    {
        await using var context = fixture.CreateDbContext();
        var session = new Session { Id = Guid.NewGuid() };
        
        context.Add(session);
        context.AddRange(
            NewMessage(session.Id, "one", sequence: 1),
            NewMessage(session.Id, "duplicate", sequence: 1));
        
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RejectsMessageWithoutARealSession()
    {
        await using var context = fixture.CreateDbContext();
        context.Add(NewMessage(Guid.NewGuid(), "Orphan", sequence: 1));
        
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static Message NewMessage(Guid sessionId, string content, int sequence = 0)
    {
        return new Message
        {
            SessionId = sessionId,
            Content = content,
            Role = "user",
            Sequence = sequence,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}