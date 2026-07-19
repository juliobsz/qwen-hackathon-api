using Sonata.Server.Models;

namespace Sonata.Server.Retrieval;

public sealed record SelectedMemory(
    Guid MemoryId,
    string Text,
    MemoryType Type,
    int Rank,
    string Reason);