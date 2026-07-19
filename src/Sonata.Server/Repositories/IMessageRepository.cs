using Sonata.Server.Models;

namespace Sonata.Server.Repositories;

public interface IMessageRepository
{
    Task<Message?> GetMessageAsync(long id);
    
    Task<Message> AddMessageAsync(Message message);
    
    Task<Message> AddAssistantMessageWithMemoryUsesAsync(
        Message message,
        IReadOnlyList<MemoryUse> memoryUses,
        CancellationToken cancellationToken);
    
    Task<IReadOnlyList<Message>> GetMessagesByConversationId(Guid conversationId);
    
    Task<IReadOnlyList<MemoryUse>> GetMemoryUsesByResponseMessageIdAsync(
        long responseMessageId,
        CancellationToken cancellationToken);
}