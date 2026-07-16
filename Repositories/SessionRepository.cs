using Microsoft.EntityFrameworkCore;
using sonata_api.Data;
using sonata_api.Models;

namespace sonata_api.Repositories;

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

    public async Task<IEnumerable<Session>> GetSessionsAsync()
    {
        return await context.Sessions.ToArrayAsync();
    }
}