using Microsoft.EntityFrameworkCore;
using Sonata.Server.Data;
using Sonata.Server.Models;

namespace Sonata.Server.Repositories;

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
