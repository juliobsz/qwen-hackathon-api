using Microsoft.EntityFrameworkCore;
using qwen_hackathon_api.Data;
using qwen_hackathon_api.Models;

namespace qwen_hackathon_api.Repositories;

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