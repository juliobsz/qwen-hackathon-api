using Microsoft.AspNetCore.Mvc;
using qwen_hackathon_api.Models;

namespace qwen_hackathon_api.Controllers;

[ApiController]
[Route("v1/")]
public class QwenController(IHttpClientFactory httpClientFactory, IConfiguration config) : Controller
{
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest data)
    {
        var content = data.Message;
        var session = data.Session;
        if (content == null || session == null) return BadRequest();
        var client = httpClientFactory.CreateClient("qwen");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + config["Qwen:ApiKey"]);
        var res = await client.PostAsJsonAsync(config["Qwen:ApiUrl"] + "/chat/completions", new
            {
                model = config["Qwen:Model"],
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content
                    }
                }
            });
        var body = await res.Content.ReadAsStringAsync();
        
        return Ok(body);
    }
}