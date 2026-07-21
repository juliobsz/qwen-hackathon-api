namespace Sonata.Server.Conversations;

public sealed record ContinueConversationCommand(Guid UserId, Guid ConversationId, string Content);

public sealed record ConversationMessage(long Id, int Sequence, string Role, string Content, DateTimeOffset CreatedAt);

public sealed record MemoryDiffItem(
    Guid MemoryId,
    Guid SourceNoteId,
    string Text,
    string Type,
    int Rank,
    string Reason);
    
public sealed record ConversationTurn(
    Guid ConversationId,
    ConversationMessage UserMessage,
    ConversationMessage AssistantMessage,
    IReadOnlyList<MemoryDiffItem> MemoryDiff);