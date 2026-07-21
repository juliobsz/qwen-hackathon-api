using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sonata.Server.Memories;
using Sonata.Server.Models;
using Sonata.Server.Security;

namespace Sonata.Server.Controllers;

[ApiController]
[Route("v1")]
public sealed class MemoriesController(IMemoryService memoryService) : ControllerBase
{
    [Authorize]
    [HttpPost("memories")]
    public async Task<IActionResult> CreateMemory([FromBody] CreateMemoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<MemoryType>(
                request.Type,
                ignoreCase: true,
                out var memoryType) || !Enum.IsDefined(typeof(MemoryType), memoryType))
        {
            return BadRequest(new
            {
                Error = MemoryError.UnsupportedType.ToString(),
                Message = "The selected Memory type is not supported."
            });
        }
        
        var result = await memoryService.CreateAsync(
            new CreateMemoryCommand(
                User.RequireUserId(),
                request.MovementId,
                request.SourceMessageId,
                request.Text ?? string.Empty,
                memoryType),
            cancellationToken);

        if (!result.Succeeded) return MapFailure(result);
        
        return StatusCode(StatusCodes.Status201Created, MemoryResponse.From(result.Memory!));
    }

    [Authorize]
    [HttpGet("movements/{movementId:guid}/memories")]
    public async Task<IActionResult> ListMemories(Guid movementId, CancellationToken cancellationToken)
    {
        var memories = await memoryService.ListAsync(User.RequireUserId(), movementId, cancellationToken);

        return Ok(new
        {
            Memories = memories.Select(MemoryResponse.From).ToArray()
        });
    }

    [Authorize]
    [HttpPost("memories/{memoryId:guid}/archive")]
    public async Task<IActionResult> ArchiveMemory(Guid memoryId, CancellationToken cancellationToken)
    {
        var result = await memoryService.ArchiveAsync(User.RequireUserId(), memoryId, cancellationToken);

        if (!result.Succeeded) return MapFailure(result);

        return Ok(MemoryResponse.From(result.Memory!));
    }

    private IActionResult MapFailure(MemoryOperationResult result)
    {
        var body = new
        {
            Error = result.Error.ToString(),
            Message = result.ErrorMessage
        };

        return result.Error switch
        {
            MemoryError.SourceMessageNotFound or MemoryError.MemoryNotFound => NotFound(body),
            MemoryError.SourceMessageOutsideMovement => Conflict(body),
            MemoryError.InvalidText or MemoryError.UnsupportedType or MemoryError.SourceMessageMustBeUser =>
                BadRequest(body),
            _ => StatusCode(StatusCodes.Status500InternalServerError, body)
        };
    }
}