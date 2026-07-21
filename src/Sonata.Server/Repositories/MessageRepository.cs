using Microsoft.EntityFrameworkCore;
using Sonata.Server.Data;
using Sonata.Server.Models;

namespace Sonata.Server.Repositories;

public class MessageRepository(ApplicationDbContext context) : IMessageRepository
{
    public async Task<Message?> GetMessageAsync(Guid userId, long id, CancellationToken cancellationToken)
    {
        return await context.Messages
            .AsNoTracking()
            .SingleOrDefaultAsync(message => 
                message.Id == id && message.Conversation.UserId == userId, 
                cancellationToken);
    }

    public async Task<Message> AddMessageAsync(Message message, CancellationToken cancellationToken)
    {
        message.Sequence = await GetNextSequenceAsync(message.ConversationId, CancellationToken.None);
        
        context.Messages.Add(message);
        await context.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task<Message> AddAssistantMessageWithMemoryUsesAsync(Message message, IReadOnlyList<MemoryUse> memoryUses,
        CancellationToken cancellationToken)
    {
        message.Sequence = await GetNextSequenceAsync(message.ConversationId, cancellationToken);

        foreach (var memoryUse in memoryUses)
        {
            memoryUse.ResponseMessage = message;
        }
        
        context.Messages.Add(message);
        context.MemoryUses.AddRange(memoryUses);
        await context.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task<IReadOnlyList<Message>> GetMessagesByConversationId(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        return await context.Messages
            .AsNoTracking()
            .Where(message => 
                message.ConversationId == conversationId && message.Conversation.UserId == userId)
            .OrderBy(message => message.Sequence)
            .ThenBy(message => message.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryUse>> GetMemoryUsesByResponseMessageIdAsync(Guid userId, long responseMessageId,
        CancellationToken cancellationToken)
    {
        return await context.MemoryUses
            .AsNoTracking()
            .Include(memoryUse => memoryUse.Memory)
            .Where(memoryUse => 
                memoryUse.ResponseMessageId == responseMessageId &&
                memoryUse.Memory.UserId == userId &&
                memoryUse.ResponseMessage.Conversation.UserId == userId)
            .OrderBy(memoryUse => memoryUse.Rank)
            .ThenBy(memoryUse => memoryUse.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<int> GetNextSequenceAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var previousSequence = await context.Messages
            .Where(existing => existing.ConversationId == conversationId)
            .MaxAsync(existing => (int?)existing.Sequence, cancellationToken) ?? 0;
        
        return previousSequence + 1;
    }
}
