using Microsoft.EntityFrameworkCore;
using Sonata.Server.Models;
using Sonata.Server.Identity;

namespace Sonata.Server.Tests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class MemoryPersistenceTests(PostgreSqlFixture fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task PersistsSourcedMemoryAndItsRecordedUse()
    {
        var userId = Guid.NewGuid();
        var movementId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        await using (var context = fixture.CreateDbContext())
        {
            var user = NewUser(userId);
            var movement = new Movement
            {
                Id = movementId,
                UserId = userId,
                Name = "Persistence proof"
            };
            var conversation = new Conversation
            {
                Id = conversationId,
                UserId = userId,
                MovementId = movementId
            };
            
            var sourceMessage = NewMessage(
                conversationId,
                sequence: 1,
                role: "user",
                content: "Use C# for the backend.");

            var responseMessage = NewMessage(
                conversationId,
                sequence: 2,
                role: "assistant",
                content: "I will keep the backend in C#.");
            
            context.AddRange(
                user,
                movement,
                conversation,
                sourceMessage,
                responseMessage);
            await context.SaveChangesAsync();

            var sourceNote = new SourceNote
            {
                MessageId = sourceMessage.Id,
                Excerpt = sourceMessage.Content
            };

            var memory = new Memory
            {
                Id = memoryId,
                UserId = userId,
                MovementId = movementId,
                SourceNote = sourceNote,
                Text = "The backend uses C#.",
                Type = MemoryType.ProjectContext,
                LifecycleState = MemoryLifecycleState.Active
            };

            var memoryUse = new MemoryUse
            {
                Memory = memory,
                ResponseMessageId = responseMessage.Id,
                Rank = 1,
                Reason = "MovementMatch"
            };

            context.Add(memoryUse);
            await context.SaveChangesAsync();
        }
        
        await using (var verificationContext = fixture.CreateDbContext())
        {
            var savedMemory = await verificationContext.Memories
                .AsNoTracking()
                .Include(memory => memory.Movement)
                .Include(memory => memory.SourceNote)
                .ThenInclude(sourceNote => sourceNote.Message)
                .Include(memory => memory.Uses)
                .ThenInclude(memoryUse => memoryUse.ResponseMessage)
                .SingleAsync(memory => memory.Id == memoryId);
            
            Assert.Equal(userId, savedMemory.UserId);
            Assert.Equal(userId, savedMemory.Movement.UserId);
            Assert.Equal("Persistence proof", savedMemory.Movement.Name);

            var savedUse = Assert.Single(savedMemory.Uses);
            Assert.Equal(1, savedUse.Rank);
            Assert.Equal("MovementMatch", savedUse.Reason);
            Assert.Equal("assistant", savedUse.ResponseMessage.Role);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RejectsMemoryWithoutARealSourceNote()
    {
        var userId = Guid.NewGuid();
        var movementId = Guid.NewGuid();
        await using var context = fixture.CreateDbContext();
        context.AddRange(
            NewUser(userId),
            new Movement
            {
                Id = movementId,
                UserId = userId,
                Name = "Invalid Source Note proof"
            });
        await context.SaveChangesAsync();

        context.Memories.Add(new Memory
        {
            UserId = userId,
            MovementId = movementId,
            SourceNoteId = Guid.NewGuid(),
            Text = "This claim has no evidence.",
            Type = MemoryType.ProjectContext,
            LifecycleState = MemoryLifecycleState.Active
        });
        
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static Message NewMessage(Guid conversationId, int sequence, string role, string content)
    {
        return new Message
        {
            ConversationId = conversationId,
            Sequence = sequence,
            Role = role,
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
    
    private static ApplicationUser NewUser(Guid userId)
    {
        var email = $"persistence-{userId:N}@example.com";
        return new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant()
        };
    }
}
