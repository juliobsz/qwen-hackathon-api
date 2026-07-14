using Microsoft.EntityFrameworkCore;
using qwen_hackathon_api.Data;
using qwen_hackathon_api.Models;

namespace qwen_hackathon_api.Repositories;

public class SessionRepository(ApplicationDbContext context) : ISessionRepository
{
    public async Task<Session?> GetSessionAsync(Guid id)
    {
        return await context.Sessions.FindAsync(id);
    }

    public async Task<Session> AddSessionAsync(Session session)
    {
        context.Sessions.Add(session);
        await context.SaveChangesAsync();
        return session;
    }
}