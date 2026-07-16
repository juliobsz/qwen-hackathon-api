using sonata_api.Models;

namespace sonata_api.Repositories;

public interface ISessionRepository
{
    Task<Session?> GetSessionAsync(Guid id);
    Task<Session> AddSessionAsync(Session session);
    Task<IEnumerable<Session>> GetSessionsAsync();
}

public interface IMessageRepository
{
    Task<Message?> GetMessageAsync(int id);
    Task<Message> AddMessageAsync(Message message);
    Task<IEnumerable<Message>> GetMessagesBySessionId(Guid sessionId);
}