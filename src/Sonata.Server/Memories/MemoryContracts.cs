using Sonata.Server.Models;

namespace Sonata.Server.Memories;

public sealed record CreateMemoryCommand(
    Guid UserId,
    Guid MovementId,
    long SourceMessageId,
    string Text,
    MemoryType Type);

public sealed record SourceNoteDetails(
    Guid Id,
    long MessageId,
    string Excerpt,
    DateTimeOffset CreatedAt);
    
public sealed record MemoryDetails(
    Guid Id,
    Guid MovementId,
    string Text,
    MemoryType Type,
    MemoryLifecycleState LifecycleState,
    SourceNoteDetails SourceNote,
    DateTimeOffset CreatedAt);

public enum MemoryError
{
    None,
    InvalidText,
    UnsupportedType,
    SourceMessageNotFound,
    SourceMessageMustBeUser,
    SourceMessageOutsideMovement,
    MemoryNotFound
}

public sealed record MemoryOperationResult(MemoryDetails? Memory, MemoryError Error, string? ErrorMessage)
{
    public bool Succeeded => Error == MemoryError.None;

    public static MemoryOperationResult Success(MemoryDetails memory)
    {
        return new MemoryOperationResult(memory, MemoryError.None, null);
    }

    public static MemoryOperationResult Failure(MemoryError error, string message)
    {
        return new MemoryOperationResult(null, error, message);
    }
}