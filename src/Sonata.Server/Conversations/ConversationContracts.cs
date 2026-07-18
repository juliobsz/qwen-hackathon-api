namespace Sonata.Server.Conversations;

public sealed record ContinueConversationCommand(Guid ConversationId, string Content);

public sealed record ConversationMessage(long Id, int Sequence, string Role, string Content, DateTimeOffset CreatedAt);

public sealed record ConversationTurn(Guid ConversationId, ConversationMessage UserMessage, ConversationMessage AssistantMessage);