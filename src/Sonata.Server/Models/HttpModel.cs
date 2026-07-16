using System.Text.Json.Serialization;

namespace Sonata.Server.Models;

public class ChatRequest
{
    public string? Content { get; set; }
    public string? SessionId { get; set; }
}

public class ChatResponse
{
    [JsonPropertyName("output")]
    public Output[] Output { get; set; }
}

public class Output
{
    [JsonPropertyName("content")]
    public Content[] Content { get; set; }
    [JsonPropertyName("role")]
    public string Role { get; set; } = null!;
}

public class Content
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = null!;
}
