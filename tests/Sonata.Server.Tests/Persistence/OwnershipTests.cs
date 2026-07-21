using Microsoft.EntityFrameworkCore;
using Sonata.Server.Identity;
using Sonata.Server.Models;
using Sonata.Server.Repositories;
using Sonata.Server.Retrieval;

namespace Sonata.Server.Tests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class OwnershipTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task ConversationRepositoryNeverReturnsForeignRow()
    {
        await using var db = database.CreateContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        var ownerId = Guid.Parse(
            "60000000-0000-0000-0000-000000000001");
        var strangerId = Guid.Parse(
            "60000000-0000-0000-0000-000000000002");
        var movementId = Guid.Parse(
            "61000000-0000-0000-0000-000000000001");
        var conversationId = Guid.Parse(
            "62000000-0000-0000-0000-000000000001");
        db.AddRange(
            User(ownerId, "owner@example.com"),
            User(strangerId, "stranger@example.com"),
            new Movement
            {
                Id = movementId,
                UserId = ownerId,
                Name = "Owner Movement"
            },
            new Conversation
            {
                Id = conversationId,
                UserId = ownerId,
                MovementId = movementId
            });
        await db.SaveChangesAsync();
        var repository = new ConversationRepository(db);

        var foreign = await repository.GetConversationAsync(
            strangerId,
            conversationId,
            CancellationToken.None);
        var owned = await repository.GetConversationAsync(
            ownerId,
            conversationId,
            CancellationToken.None);

        var foreignList = await repository.GetConversationsAsync(
            strangerId,
            CancellationToken.None);
        var ownedList = await repository.GetConversationsAsync(
            ownerId,
            CancellationToken.None);
        
        Assert.Null(foreign);
        Assert.NotNull(owned);
        Assert.Equal(ownerId, owned.UserId);
        Assert.DoesNotContain(foreignList, item => 
            item.Id == conversationId);
        Assert.Contains(ownedList, item => 
            item.Id == conversationId);
    }
    
    [Fact]
    public async Task MemorySelectorNeverReturnsForeignMemory()
    {
        await using var db = database.CreateContext();
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var movementId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        db.AddRange(
            User(ownerId, $"owner-{ownerId:N}@example.com"),
            User(strangerId, $"stranger-{strangerId:N}@example.com"),
            new Movement
            {
                Id = movementId,
                UserId = ownerId,
                Name = "Private retrieval"
            },
            new Conversation
            {
                Id = conversationId,
                UserId = ownerId,
                MovementId = movementId
            });
        await db.SaveChangesAsync();

        var message = new Message
        {
            ConversationId = conversationId,
            Sequence = 1,
            Role = "user",
            Content = "The owner chose PostgreSQL."
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        db.Memories.Add(new Memory
        {
            UserId = ownerId,
            MovementId = movementId,
            SourceNote = new SourceNote
            {
                MessageId = message.Id,
                Excerpt = message.Content
            },
            Text = "The owner chose PostgreSQL.",
            Type = MemoryType.Decision,
            LifecycleState = MemoryLifecycleState.Active
        });
        await db.SaveChangesAsync();

        var selector = new MemorySelector(db);
        var foreign = await selector.SelectAsync(
            strangerId,
            movementId,
            5,
            CancellationToken.None);
        var owned = await selector.SelectAsync(
            ownerId,
            movementId,
            5,
            CancellationToken.None);

        Assert.Empty(foreign);
        Assert.Single(owned);
    }

    private static ApplicationUser User(
        Guid id,
        string email)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant()
        };
    }
}