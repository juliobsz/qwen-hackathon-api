using Microsoft.AspNetCore.Mvc;
using Sonata.Server.Models;
using Sonata.Server.Repositories;

namespace Sonata.Server.Controllers;

[ApiController]
[Route("v1/")]
public class QwenController(IHttpClientFactory httpClientFactory, IConfiguration config, ISessionRepository sessionRepository, IMessageRepository messageRepository) : Controller
{
    [HttpPost("responses")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest data)
    {
        var content = data.Content;
        var sessionId = data.SessionId ?? "";
        if (content == null || !Guid.TryParse(sessionId, out var sessionGuid)) return BadRequest();
        
        var session = await sessionRepository.GetSessionAsync(sessionGuid) ??
                      await sessionRepository.AddSessionAsync(new Session() 
                      {
                          Id = sessionGuid,
                          StartedAt = DateTimeOffset.UtcNow,
                      });
        
        await messageRepository.AddMessageAsync(new Message()
        {
            SessionId = session.Id,
            Content = content,
            Role = "user",
            CreatedAt = DateTimeOffset.UtcNow
        });
        
        var client = httpClientFactory.CreateClient("qwen");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + config["Qwen:ApiKey"]);
        
        var messages = await messageRepository.GetMessagesBySessionId(session.Id);
        
        var res = await client.PostAsJsonAsync(config["Qwen:ApiUrl"] + "/responses", new
            {
                model = config["Qwen:Model"],
                input = messages.Select(message => new
                {
                    role = message.Role,
                    content = message.Content
                })
            });
        var body = await res.Content.ReadFromJsonAsync<ChatResponse>();
        if (!res.IsSuccessStatusCode || body == null) return BadRequest();
        
        await messageRepository.AddMessageAsync(new Message()
        {
            SessionId = session.Id,
            Content = body.Output[0].Content[0].Text,
            Role = body.Output[0].Role,
            CreatedAt = DateTimeOffset.UtcNow
        });
        
        return Ok(new
        {
            Content = body.Output[0].Content[0].Text,
            SessionId = session.Id
        });
    }
    
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var client = httpClientFactory.CreateClient("qwen");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + config["Qwen:ApiKey"]);
        
        var sessions = await sessionRepository.GetSessionsAsync();
        
        return Ok(new
        {
            Sessions = sessions
        });
    }
    
    [HttpGet("messages/{sessionId}")]
    public async Task<IActionResult> GetMessages([FromRoute] string sessionId)
    {
        var client = httpClientFactory.CreateClient("qwen");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + config["Qwen:ApiKey"]);
        
        if (!Guid.TryParse(sessionId, out _)) return BadRequest();
        var messages = await messageRepository.GetMessagesBySessionId(Guid.Parse(sessionId));
        
        return Ok(new
        {
            Messages = messages
        });
    }
}
