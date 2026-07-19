namespace Sonata.Server.Retrieval;

public interface IMemorySelector
{
    Task<IReadOnlyList<SelectedMemory>> SelectAsync(
        Guid movementId,
        int maximumCount,
        CancellationToken cancellationToken = default);
}