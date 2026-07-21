using Microsoft.EntityFrameworkCore;
using Sonata.Server.Data;
using Sonata.Server.Models;

namespace Sonata.Server.Repositories;

public sealed class ConversationRepository(ApplicationDbContext context) : IConversationRepository
{
    public async Task<Conversation?> GetConversationAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        return await context.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(conversation => 
                conversation.Id == id && conversation.UserId == userId, 
                cancellationToken);
    }

    public async Task<Conversation> AddConversationAsync(Conversation conversation,  CancellationToken cancellationToken)
    {
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    public async Task<IReadOnlyList<Conversation>> GetConversationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await context.Conversations
            .AsNoTracking()
            .Where(conversation => 
                conversation.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(conversation => conversation.Id)
            .ToListAsync(cancellationToken);
    }
}
