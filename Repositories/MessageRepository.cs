using Microsoft.EntityFrameworkCore;
using sonata_api.Data;
using sonata_api.Models;

namespace sonata_api.Repositories;

public class MessageRepository(ApplicationDbContext context) : IMessageRepository
{
    public async Task<Message?> GetMessageAsync(int id)
    {
        return await context.Messages.FindAsync(id);
    }

    public async Task<Message> AddMessageAsync(Message message)
    {
        context.Messages.Add(message);
        await context.SaveChangesAsync();
        return message;
    }

    public async Task<IEnumerable<Message>> GetMessagesBySessionId(Guid sessionId)
    {
        return await context.Messages.Where(m => m.SessionId == sessionId).ToListAsync();
    }
}