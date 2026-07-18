using Microsoft.EntityFrameworkCore;
using Sonata.Server.Data;
using Sonata.Server.Models;

namespace Sonata.Server.Repositories;

public class MessageRepository(ApplicationDbContext context) : IMessageRepository
{
    public async Task<Message?> GetMessageAsync(long id)
    {
        return await context.Messages.FindAsync(id);
    }

    public async Task<Message> AddMessageAsync(Message message)
    {
        var previousSequence = await context.Messages
            .Where(existing => existing.SessionId == message.SessionId)
            .MaxAsync(existing => (int?)existing.Sequence) ?? 0;
        
        message.Sequence = previousSequence + 1;
        
        context.Messages.Add(message);
        await context.SaveChangesAsync();
        return message;
    }

    public async Task<IReadOnlyList<Message>> GetMessagesBySessionId(Guid sessionId)
    {
        return await context.Messages
            .AsNoTracking()
            .Where(message => message.SessionId == sessionId)
            .OrderBy(message => message.Sequence)
            .ThenBy(message => message.Id)
            .ToListAsync();
    }
}
