using Microsoft.EntityFrameworkCore;
using Sonata.Server.Data;
using Sonata.Server.Models;

namespace Sonata.Server.Memories;

public sealed class MemoryService(ApplicationDbContext context) : IMemoryService
{
    private const int MaximumMemoryTextLength = 500;
    private const int MaximumExcerptLength = 500;

    public async Task<MemoryOperationResult> CreateAsync(CreateMemoryCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalizedText = command.Text.Trim();

        if (normalizedText.Length == 0 || normalizedText.Length > MaximumMemoryTextLength) 
            return MemoryOperationResult.Failure(MemoryError.InvalidText, "Memory text must contain between 1 and 500 characters.");

        if (!Enum.IsDefined(typeof(MemoryType), command.Type)) 
            return MemoryOperationResult.Failure(MemoryError.UnsupportedType, "The selected Memory type is not supported.");

        var ownsMovement = await context.Movements
            .AsNoTracking()
            .AnyAsync(movement =>
                movement.Id == command.MovementId &&
                movement.UserId == command.UserId,
                cancellationToken);
        if (!ownsMovement)
            return MemoryOperationResult.Failure(MemoryError.SourceMessageOutsideMovement,
                "The source Message does not belong to this Movement.");
        
        var sourceMessage = await context.Messages
            .AsNoTracking()
            .Include(message => message.Conversation)
            .SingleOrDefaultAsync(message => 
                message.Id == command.SourceMessageId &&
                message.Conversation.UserId == command.UserId,
                cancellationToken);

        if (sourceMessage == null)
            return MemoryOperationResult.Failure(MemoryError.SourceMessageNotFound, 
                "The selected source message does not exist.");

        if (sourceMessage.Role != "user") 
            return MemoryOperationResult.Failure(MemoryError.SourceMessageMustBeUser,
                "Only a user Message can be saved as a Memory");

        if (sourceMessage.Conversation.MovementId != command.MovementId) 
            return MemoryOperationResult.Failure(MemoryError.SourceMessageOutsideMovement, 
                "The source Message does not belong to this movement.");

        var sourceNote = new SourceNote
        {
            MessageId = sourceMessage.Id,
            Excerpt = sourceMessage.Content[..Math.Min(sourceMessage.Content.Length, MaximumExcerptLength)]
        };

        var memory = new Memory
        {
            UserId = command.UserId,
            MovementId = command.MovementId,
            SourceNote = sourceNote,
            Text = normalizedText,
            Type = command.Type,
            LifecycleState = MemoryLifecycleState.Active
        };

        context.Memories.Add(memory);
        await context.SaveChangesAsync(cancellationToken);

        return MemoryOperationResult.Success(ToDetails(memory));
    }

    public async Task<IReadOnlyList<MemoryDetails>> ListAsync(Guid userId, Guid movementId,
        CancellationToken cancellationToken = default)
    {
        return await context.Memories
            .AsNoTracking()
            .Where(memory =>
                memory.MovementId == movementId &&
                memory.UserId == userId)
            .OrderByDescending(memory => memory.CreatedAt)
            .ThenBy(memory => memory.Id)
            .Select(memory => new MemoryDetails(
                memory.Id,
                memory.MovementId,
                memory.Text,
                memory.Type,
                memory.LifecycleState,
                new SourceNoteDetails(
                    memory.SourceNote.Id,
                    memory.SourceNote.MessageId,
                    memory.SourceNote.Excerpt,
                    memory.SourceNote.CreatedAt),
                memory.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<MemoryOperationResult> ArchiveAsync(Guid userId, Guid memoryId, CancellationToken cancellationToken = default)
    {
        var memory = await context.Memories
            .Include(item => item.SourceNote)
            .SingleOrDefaultAsync(item => 
                item.Id == memoryId && item.UserId == userId,
                cancellationToken);

        if (memory == null) 
            return MemoryOperationResult.Failure(MemoryError.MemoryNotFound, "The requested Memory does not exist.");

        if (memory.LifecycleState != MemoryLifecycleState.Archived)
        {
            memory.LifecycleState = MemoryLifecycleState.Archived;
            await context.SaveChangesAsync(cancellationToken);
        }

        return MemoryOperationResult.Success(ToDetails(memory));
    }

    private static MemoryDetails ToDetails(Memory memory)
    {
        return new MemoryDetails(
            memory.Id,
            memory.MovementId,
            memory.Text,
            memory.Type,
            memory.LifecycleState,
            new SourceNoteDetails(
                memory.SourceNote.Id,
                memory.SourceNote.MessageId,
                memory.SourceNote.Excerpt,
                memory.SourceNote.CreatedAt),
            memory.CreatedAt);
    }
}