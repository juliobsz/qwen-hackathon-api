using Sonata.Server.Models;

namespace Sonata.Server.Repositories;

public interface ISessionRepository
{
    Task<Session?> GetSessionAsync(Guid id);
    Task<Session> AddSessionAsync(Session session);
    Task<IEnumerable<Session>> GetSessionsAsync();
}

public interface IMessageRepository
{
    Task<Message?> GetMessageAsync(long id);
    Task<Message> AddMessageAsync(Message message);
    Task<IReadOnlyList<Message>> GetMessagesBySessionId(Guid sessionId);
}
