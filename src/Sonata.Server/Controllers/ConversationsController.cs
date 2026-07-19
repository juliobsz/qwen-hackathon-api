using Microsoft.AspNetCore.Mvc;
using Sonata.Server.Repositories;
using Sonata.Server.Conversations;
using Sonata.Server.ModelProviders;

namespace Sonata.Server.Controllers;

[ApiController]
[Route("v1/")]
public sealed class ConversationsController(IConversationService conversationService, IConversationRepository conversationRepository, IMessageRepository messageRepository) : ControllerBase
{
    [HttpPost("responses")]
    public async Task<IActionResult> ContinueConversation([FromBody] ContinueConversationRequest data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(data.Content) || !Guid.TryParse(data.ConversationId, out var conversationId)) return BadRequest();

        try
        {
            var turn = await conversationService.ContinueAsync(
                new ContinueConversationCommand(conversationId, data.Content),
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
    
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var conversations = await conversationRepository.GetConversationsAsync();
        
        return Ok(new
        {
            Conversations = conversations
        });
    }
    
    [HttpGet("conversations/{conversationId}/messages")]
    public async Task<IActionResult> GetMessages([FromRoute] string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var conversationGuid)) return BadRequest();
        var messages = await messageRepository.GetMessagesByConversationId(conversationGuid);
        
        return Ok(new
        {
            Messages = messages
        });
    }
}
