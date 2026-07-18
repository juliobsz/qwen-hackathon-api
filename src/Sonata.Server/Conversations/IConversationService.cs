namespace Sonata.Server.Conversations;

public interface IConversationService 
{
    Task<ConversationTurn> ContinueAsync(ContinueConversationCommand command, CancellationToken cancellationToken);
}