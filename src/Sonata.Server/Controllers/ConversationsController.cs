using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sonata.Server.Repositories;
using Sonata.Server.Conversations;
using Sonata.Server.ModelProviders;
using Sonata.Server.Security;

namespace Sonata.Server.Controllers;

[ApiController]
[Route("v1/")]
public sealed class ConversationsController(IConversationService conversationService, IConversationRepository conversationRepository, IMessageRepository messageRepository) : ControllerBase
{
    [Authorize]
    [HttpPost("responses")]
    public async Task<IActionResult> ContinueConversation([FromBody] ContinueConversationRequest data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(data.Content) || !Guid.TryParse(data.ConversationId, out var conversationId)) return BadRequest();

        try
        {
            var turn = await conversationService.ContinueAsync(
                new ContinueConversationCommand(User.RequireUserId(), conversationId, data.Content),
                cancellationToken);

            return Ok(new
            {
                Content = turn.AssistantMessage.Content,
                ConversationId = turn.ConversationId,
                MemoryDiff = turn.MemoryDiff
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
    
    [Authorize]
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        var conversations = await conversationRepository.GetConversationsAsync(User.RequireUserId(), cancellationToken);
        
        return Ok(new
        {
            Conversations = conversations
        });
    }
    
    [Authorize]
    [HttpGet("conversations/{conversationId}/messages")]
    public async Task<IActionResult> GetMessages([FromRoute] string conversationId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(conversationId, out var conversationGuid)) return BadRequest();
        
        var userId = User.RequireUserId();
        var conversation = await conversationRepository.GetConversationAsync(userId, conversationGuid, cancellationToken);
        if (conversation == null) return NotFound();
        
        var messages = await messageRepository.GetMessagesByConversationId(userId, conversationGuid, cancellationToken);
        
        return Ok(new
        {
            Messages = messages
        });
    }
}
