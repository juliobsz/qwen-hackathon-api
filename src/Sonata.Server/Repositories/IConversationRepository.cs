using Sonata.Server.Models;

namespace Sonata.Server.Repositories;

public interface IConversationRepository
{
    Task<Conversation?> GetConversationAsync(Guid userId, Guid id, CancellationToken cancellationToken);
    
    Task<Conversation> AddConversationAsync(Conversation conversation, CancellationToken cancellationToken);
    
    Task<IReadOnlyList<Conversation>> GetConversationsAsync(Guid userId, CancellationToken cancellationToken);
}