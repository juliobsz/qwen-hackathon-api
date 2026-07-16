using System.Text.Json.Serialization;

namespace Sonata.Desktop.Models;

public class ChatResponse
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }
}

public class SessionResponse
{
    public Session[] Sessions { get; set; } = [];
}

public class MessageResponse
{
    public Message[] Messages { get; set; } = [];
}
