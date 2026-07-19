using System.Text.Json.Serialization;

namespace Sonata.Desktop.Models;

public sealed class ContinueConversationResponse
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    [JsonPropertyName("conversationId")]
    public Guid ConversationId { get; set; }
    
    [JsonPropertyName("memoryDiff")]
    public MemoryDiffItem[] MemoryDiff { get; set; } = [];
}

public sealed class ConversationListResponse
{
    [JsonPropertyName("conversations")]
    public Conversation[] Conversations { get; set; } = [];
}

public sealed class MessageResponse
{
    [JsonPropertyName("messages")]
    public Message[] Messages { get; set; } = [];
}

public sealed class MemoryListResponse
{
    [JsonPropertyName("memories")]
    public MemoryItem[] Memories { get; set; } = [];
}

public sealed class ApiErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}