using Microsoft.AspNetCore.Mvc;
using qwen_hackathon_api.Models;
using qwen_hackathon_api.Repositories;

namespace qwen_hackathon_api.Controllers;

[ApiController]
[Route("v1/")]
public class QwenController(IHttpClientFactory httpClientFactory, IConfiguration config, ISessionRepository sessionRepository, IMessageRepository messageRepository) : Controller
{
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest data)
    {
        var content = data.Content;
        var sessionId = data.SessionId ?? "";
        if (content == null) return BadRequest();
        
        if (!Guid.TryParse(sessionId, out Guid id)) sessionId = Guid.NewGuid().ToString();
        var session = await sessionRepository.GetSessionAsync(Guid.Parse(sessionId)) ??
                      await sessionRepository.AddSessionAsync(new Session() 
                      {
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
        var res = await client.PostAsJsonAsync(config["Qwen:ApiUrl"] + "/chat/completions", new
            {
                model = config["Qwen:Model"],
                messages
            });
        var body = await res.Content.ReadFromJsonAsync<ChatResponse>();
        if (!res.IsSuccessStatusCode || body == null) return BadRequest();

        await messageRepository.AddMessageAsync(new Message()
        {
            SessionId = session.Id,
            Content = body.Choices[0].Message.Content,
            Role = body.Choices[0].Message.Role,
            CreatedAt = DateTimeOffset.UtcNow
        });
        
        return Ok(new
        {
            Content = body.Choices[0].Message.Content,
            StatusId = session.Id
        });
    }
}