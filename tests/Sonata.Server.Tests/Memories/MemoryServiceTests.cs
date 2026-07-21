using Microsoft.EntityFrameworkCore;
using Sonata.Server.Memories;
using Sonata.Server.Models;
using Sonata.Server.Identity;

namespace Sonata.Server.Tests.Memories;

[Collection(Persistence.PostgreSqlCollection.Name)]
public sealed class MemoryServiceTests(
    Persistence.PostgreSqlFixture fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreatesAndListsMemoryFromAUserMessage()
    {
        var userId = await AddUserAsync();
        var movementId = await AddMovementAsync(
            userId,
            "A unique descriptive Movement name");
        
        var sourceMessageId = await AddMessageAsync(
            userId,
            movementId,
            role: "user",
            content: "Use C# for the backend.");

        var memoryId = Guid.Empty;

        await using (var context = fixture.CreateDbContext())
        {
            IMemoryService service = new MemoryService(context);

            var result = await service.CreateAsync(
                new CreateMemoryCommand(
                    userId,
                    movementId,
                    sourceMessageId,
                    "The backend uses C#.",
                    MemoryType.ProjectContext));

            Assert.True(result.Succeeded);
            Assert.Equal(MemoryError.None, result.Error);
            Assert.NotNull(result.Memory);
            Assert.Equal("The backend uses C#.", result.Memory.Text);
            Assert.Equal(
                MemoryLifecycleState.Active,
                result.Memory.LifecycleState);
            Assert.Equal(
                sourceMessageId,
                result.Memory.SourceNote.MessageId);
            Assert.Equal(
                "Use C# for the backend.",
                result.Memory.SourceNote.Excerpt);

            memoryId = result.Memory.Id;
        }

        await using (var verificationContext = fixture.CreateDbContext())
        {
            IMemoryService service = new MemoryService(verificationContext);
            var memories = await service.ListAsync(userId, movementId, CancellationToken.None);

            var savedMemory = Assert.Single(
                memories,
                memory => memory.Id == memoryId);
            Assert.Equal(MemoryType.ProjectContext, savedMemory.Type);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RejectsAnAssistantMessageAsEvidence()
    {
        var userId = await AddUserAsync();
        var movementId = await AddMovementAsync(
            userId,
            "A unique descriptive Movement name");
        
        var sourceMessageId = await AddMessageAsync(
            userId,
            movementId,
            role: "assistant",
            content: "Generated text is not a user decision.");

        await using var context = fixture.CreateDbContext();
        IMemoryService service = new MemoryService(context);

        var result = await service.CreateAsync(
            new CreateMemoryCommand(
                userId,
                movementId,
                sourceMessageId,
                "The backend uses C#.",
                MemoryType.ProjectContext));

        Assert.False(result.Succeeded);
        Assert.Equal(
            MemoryError.SourceMessageMustBeUser,
            result.Error);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RejectsAnUnknownSourceMessage()
    {
        var userId = await AddUserAsync();
        var movementId = await AddMovementAsync(
            userId,
            "A unique descriptive Movement name");
        
        await using var context = fixture.CreateDbContext();
        IMemoryService service = new MemoryService(context);

        var result = await service.CreateAsync(
            new CreateMemoryCommand(
                userId,
                movementId,
                long.MaxValue,
                "This has no source.",
                MemoryType.ProjectContext));

        Assert.False(result.Succeeded);
        Assert.Equal(
            MemoryError.SourceMessageNotFound,
            result.Error);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RejectsAnUnsupportedMemoryType()
    {
        var userId = await AddUserAsync();
        var movementId = await AddMovementAsync(
            userId,
            "A unique descriptive Movement name");
        
        var sourceMessageId = await AddMessageAsync(
            userId,
            movementId,
            role: "user",
            content: "There is evidence for a supported type.");

        await using var context = fixture.CreateDbContext();
        IMemoryService service = new MemoryService(context);

        var result = await service.CreateAsync(
            new CreateMemoryCommand(
                userId,
                movementId,
                sourceMessageId,
                "This type is invalid.",
                (MemoryType)999));

        Assert.False(result.Succeeded);
        Assert.Equal(MemoryError.UnsupportedType, result.Error);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RejectsEvidenceFromAnotherMovement()
    {
        var userId = await AddUserAsync();
        var movementId = await AddMovementAsync(
            userId,
            "A unique descriptive Movement name");
        
        var otherMovementId = await AddMovementAsync(
            userId,
            "Other Movement");
        var sourceMessageId = await AddMessageAsync(
            userId,
            otherMovementId,
            role: "user",
            content: "This belongs elsewhere.");

        await using var context = fixture.CreateDbContext();
        IMemoryService service = new MemoryService(context);

        var result = await service.CreateAsync(
            new CreateMemoryCommand(
                userId,
                movementId,
                sourceMessageId,
                "This must not cross Movements.",
                MemoryType.Decision));

        Assert.False(result.Succeeded);
        Assert.Equal(
            MemoryError.SourceMessageOutsideMovement,
            result.Error);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RejectsBlankMemoryText()
    {
        var userId = await AddUserAsync();
        var movementId = await AddMovementAsync(
            userId,
            "A unique descriptive Movement name");
        
        var sourceMessageId = await AddMessageAsync(
            userId,
            movementId,
            role: "user",
            content: "There is real evidence here.");

        await using var context = fixture.CreateDbContext();
        IMemoryService service = new MemoryService(context);

        var result = await service.CreateAsync(
            new CreateMemoryCommand(
                userId,
                movementId,
                sourceMessageId,
                "   ",
                MemoryType.Decision));

        Assert.False(result.Succeeded);
        Assert.Equal(MemoryError.InvalidText, result.Error);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ArchivesMemoryIdempotently()
    {
        var userId = await AddUserAsync();
        var movementId = await AddMovementAsync(
            userId,
            "A unique descriptive Movement name");
        
        var sourceMessageId = await AddMessageAsync(
            userId,
            movementId,
            role: "user",
            content: "Archive this synthetic preference.");

        await using var context = fixture.CreateDbContext();
        IMemoryService service = new MemoryService(context);
        var created = await service.CreateAsync(
            new CreateMemoryCommand(
                userId,
                movementId,
                sourceMessageId,
                "Prefer concise guidance.",
                MemoryType.Preference));

        Assert.True(created.Succeeded);
        Assert.NotNull(created.Memory);

        var firstArchive = await service.ArchiveAsync(userId, created.Memory.Id, CancellationToken.None);
        var secondArchive = await service.ArchiveAsync(userId, created.Memory.Id, CancellationToken.None);

        Assert.True(firstArchive.Succeeded);
        Assert.True(secondArchive.Succeeded);
        Assert.Equal(
            MemoryLifecycleState.Archived,
            secondArchive.Memory?.LifecycleState);
    }
    
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ForeignUserCannotListOrArchiveMemory()
    {
        var ownerId = await AddUserAsync();
        var strangerId = await AddUserAsync();
        var movementId = await AddMovementAsync(
            ownerId,
            "Owner-only Memory");
        var sourceMessageId = await AddMessageAsync(
            ownerId,
            movementId,
            "user",
            "Keep this private to the owner.");

        Guid memoryId;
        await using (var creationContext = fixture.CreateDbContext())
        {
            IMemoryService creation = new MemoryService(
                creationContext);
            var created = await creation.CreateAsync(
                new CreateMemoryCommand(
                    ownerId,
                    movementId,
                    sourceMessageId,
                    "This belongs to the owner.",
                    MemoryType.ProjectContext));

            Assert.True(created.Succeeded);
            memoryId = created.Memory!.Id;
        }

        await using var strangerContext = fixture.CreateDbContext();
        IMemoryService stranger = new MemoryService(strangerContext);

        var listed = await stranger.ListAsync(
            strangerId,
            movementId,
            CancellationToken.None);
        var archived = await stranger.ArchiveAsync(
            strangerId,
            memoryId,
            CancellationToken.None);

        Assert.Empty(listed);
        Assert.False(archived.Succeeded);
        Assert.Equal(MemoryError.MemoryNotFound, archived.Error);
    }
    
    [Fact]
    [Trait("Category", "Integration")]
    public async Task AcceptsExactlyFiveHundredCharacters()
    {
        var userId = await AddUserAsync();
        var movementId = await AddMovementAsync(
            userId,
            "Memory length boundary");
        var sourceMessageId = await AddMessageAsync(
            userId,
            movementId,
            "user",
            "This Message is valid evidence.");

        await using var context = fixture.CreateDbContext();
        IMemoryService service = new MemoryService(context);
        var result = await service.CreateAsync(
            new CreateMemoryCommand(
                userId,
                movementId,
                sourceMessageId,
                new string('x', 500),
                MemoryType.ProjectContext));

        Assert.True(result.Succeeded);
        Assert.Equal(500, result.Memory!.Text.Length);
    }

    private async Task<Guid> AddUserAsync()
    {
        await using var context = fixture.CreateDbContext();
        var email = $"memory-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    private async Task<Guid> AddMovementAsync(
        Guid userId,
        string name)
    {
        await using var context = fixture.CreateDbContext();
        var movement = new Movement
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name
        };

        context.Movements.Add(movement);
        await context.SaveChangesAsync();
        return movement.Id;
    }

    private async Task<long> AddMessageAsync(
        Guid userId,
        Guid movementId,
        string role,
        string content)
    {
        await using var context = fixture.CreateDbContext();
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovementId = movementId
        };
        var message = new Message
        {
            Conversation = conversation,
            Sequence = 1,
            Role = role,
            Content = content
        };

        context.Messages.Add(message);
        await context.SaveChangesAsync();
        return message.Id;
    }
}
