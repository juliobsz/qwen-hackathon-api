using Microsoft.EntityFrameworkCore;
using Sonata.Server.Data;
using Sonata.Server.Models;

namespace Sonata.Server.Retrieval;

public sealed class MemorySelector(ApplicationDbContext context) : IMemorySelector
{
    public async Task<IReadOnlyList<SelectedMemory>> SelectAsync(
        Guid movementId,
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        if (maximumCount <= 0) return [];

        var memories = await context.Memories
            .AsNoTracking()
            .Where(memory => memory.MovementId == movementId
                             && memory.LifecycleState == MemoryLifecycleState.Active)
            .OrderByDescending(memory => memory.CreatedAt)
            .ThenBy(memory => memory.Id)
            .Take(maximumCount)
            .ToListAsync(cancellationToken);
        
        return memories.Select((memory, index) => new SelectedMemory(
            memory.Id,
            memory.Text,
            memory.Type,
            Rank: index + 1,
            Reason: "MovementMatch"))
            .ToArray();
    }
}