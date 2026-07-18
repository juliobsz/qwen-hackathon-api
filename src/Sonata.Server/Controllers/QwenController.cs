using Microsoft.AspNetCore.Mvc;
using Sonata.Server.Models;
using Sonata.Server.Repositories;
using Sonata.Server.Conversations;
using Sonata.Server.ModelProviders;

namespace Sonata.Server.Controllers;

[ApiController]
[Route("v1/")]
public class QwenController(IConversationService conversationService, ISessionRepository sessionRepository, IMessageRepository messageRepository) : Controller
{
    [HttpPost("responses")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(data.Content) || !Guid.TryParse(data.SessionId, out var conversationId)) return BadRequest();

        try
        {
            var turn = await conversationService.ContinueAsync(
                new ContinueConversationCommand(conversationId, data.Content),
                cancellationToken);

            return Ok(new
            {
                Content = turn.AssistantMessage.Content,
                SessionId = turn.ConversationId
            });
        }
        catch (ModelProviderException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                Error = "The model provider couldn't complete the response."
            });
        }
    }
    
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await sessionRepository.GetSessionsAsync();
        
        return Ok(new
        {
            Sessions = sessions
        });
    }
    
    [HttpGet("messages/{sessionId}")]
    public async Task<IActionResult> GetMessages([FromRoute] string sessionId)
    {
        if (!Guid.TryParse(sessionId, out var sessionGuid)) return BadRequest();
        var messages = await messageRepository.GetMessagesBySessionId(sessionGuid);
        
        return Ok(new
        {
            Messages = messages
        });
    }
}
