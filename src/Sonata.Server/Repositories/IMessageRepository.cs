using Sonata.Server.Models;

namespace Sonata.Server.Repositories;

public interface IMessageRepository
{
    Task<Message?> GetMessageAsync(Guid userId, long id, CancellationToken cancellationToken);
    
    Task<Message> AddMessageAsync(Message message, CancellationToken cancellationToken);
    
    Task<Message> AddAssistantMessageWithMemoryUsesAsync(
        Message message,
        IReadOnlyList<MemoryUse> memoryUses,
        CancellationToken cancellationToken);
    
    Task<IReadOnlyList<Message>> GetMessagesByConversationId(Guid userId, Guid conversationId, CancellationToken cancellationToken);
    
    Task<IReadOnlyList<MemoryUse>> GetMemoryUsesByResponseMessageIdAsync(
        Guid userId,
        long responseMessageId,
        CancellationToken cancellationToken);
}