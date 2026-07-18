using Sonata.Server.Repositories;
using Sonata.Server.ModelProviders;
using Sonata.Server.Models;

namespace Sonata.Server.Conversations;

public sealed class ConversationService(
    ISessionRepository sessionRepository,
    IMessageRepository messageRepository,
    IModelProvider modelProvider) : IConversationService
{
    public async Task<ConversationTurn> ContinueAsync(ContinueConversationCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Content))
            throw new ArgumentException("Conversation content can't be empty.", nameof(command));

        var session = await sessionRepository.GetSessionAsync(command.ConversationId)
                      ?? await sessionRepository.AddSessionAsync(new Session
                      {
                          Id = command.ConversationId,
                          StartedAt = DateTimeOffset.UtcNow,
                      });

        var userMessage = await messageRepository.AddMessageAsync(new Message
        {
            SessionId = session.Id,
            Content = command.Content,
            Role = "user",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        
        var history = await messageRepository.GetMessagesBySessionId(session.Id);

        var generated = await modelProvider.GenerateResponseAsync(new GenerateResponseRequest(
                history.Select(message => new ModelMessage(
                    message.Role,
                    message.Content
                )).ToArray()),
            cancellationToken);

        var assistantMessage = await messageRepository.AddMessageAsync(new Message
        {
            SessionId = session.Id,
            Content = generated.Text,
            Role = generated.Role,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        
        return new ConversationTurn(session.Id, ToContract(userMessage), ToContract(assistantMessage));
    }
    
    private static ConversationMessage ToContract(Message message)
    {
        return new ConversationMessage(
            message.Id,
            message.Sequence,
            message.Role,
            message.Content,
            message.CreatedAt
        );
    }
}