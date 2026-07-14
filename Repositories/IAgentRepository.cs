using qwen_hackathon_api.Models;

namespace qwen_hackathon_api.Repositories;

public interface ISessionRepository
{
    Task<Session?> GetSessionAsync(Guid id);
    Task<Session> AddSessionAsync(Session session);
}

public interface IMessageRepository
{
    Task<Message?> GetMessageAsync(int id);
    Task<Message> AddMessageAsync(Message message);
    Task<IEnumerable<Message>> GetMessagesBySessionId(Guid sessionId);
}