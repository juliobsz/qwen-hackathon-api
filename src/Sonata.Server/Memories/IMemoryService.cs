namespace Sonata.Server.Memories;

public interface IMemoryService
{
    Task<MemoryOperationResult> CreateAsync(CreateMemoryCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryDetails>> ListAsync(Guid userId, Guid movementId,  CancellationToken cancellationToken = default);
    Task<MemoryOperationResult> ArchiveAsync(Guid userId, Guid memoryId, CancellationToken cancellationToken = default);
}