namespace Sonata.Server.Retrieval;

public interface IMemorySelector
{
    Task<IReadOnlyList<SelectedMemory>> SelectAsync(
        Guid userId,
        Guid movementId,
        int maximumCount,
        CancellationToken cancellationToken = default);
}