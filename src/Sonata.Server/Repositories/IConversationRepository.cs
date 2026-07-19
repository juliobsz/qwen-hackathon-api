using Sonata.Server.Models;

namespace Sonata.Server.Repositories;

public interface IConversationRepository
{
    Task<Conversation?> GetConversationAsync(Guid id);
    
    Task<Conversation> AddConversationAsync(Conversation conversation);
    
    Task<IEnumerable<Conversation>> GetConversationsAsync();
}