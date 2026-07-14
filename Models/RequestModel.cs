using System.Text.Json.Serialization;

namespace qwen_hackathon_api.Models;

public class ChatRequest
{
    public string? Content { get; set; }
    public string? SessionId { get; set; }
}

public partial class ChatResponse
{
    [JsonPropertyName("choices")]
    public Choice[] Choices { get; set; }
}

public partial class Choice
{
    [JsonPropertyName("message")]
    public ResultMessage Message { get; set; }
}

public partial class ResultMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = null!;

    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;
}