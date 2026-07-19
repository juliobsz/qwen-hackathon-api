using System.Net;
using System.Text;
using System.Text.Json;
using Sonata.Desktop.Services;

namespace Sonata.Desktop.Tests;

public sealed class ApiServiceTests
{
    private static readonly Guid ConversationId = Guid.Parse(
        "20000000-0000-0000-0000-000000000001");

    private static readonly Guid MemoryId = Guid.Parse(
        "30000000-0000-0000-0000-000000000001");

    private static readonly Guid SourceNoteId = Guid.Parse(
        "40000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task SendMessageReadsPersistedMemoryDiff()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "content": "Use C# for the backend.",
              "conversationId": "20000000-0000-0000-0000-000000000001",
              "memoryDiff": [
                {
                  "memoryId": "30000000-0000-0000-0000-000000000001",
                  "sourceNoteId": "40000000-0000-0000-0000-000000000001",
                  "text": "The backend uses C#.",
                  "type": "ProjectContext",
                  "rank": 1,
                  "reason": "MovementMatch"
                }
              ]
            }
            """));
        using var httpClient = NewHttpClient(handler);
        var service = new ApiService(httpClient);

        var response = await service.SendMessageAsync(
            "What should I use?",
            ConversationId);

        Assert.Equal("Use C# for the backend.", response.Content);
        Assert.Equal(ConversationId, response.ConversationId);
        var diff = Assert.Single(response.MemoryDiff);
        Assert.Equal(MemoryId, diff.MemoryId);
        Assert.Equal(SourceNoteId, diff.SourceNoteId);
        Assert.Equal("MovementMatch", diff.Reason);
        Assert.Equal(HttpMethod.Post, handler.ReceivedMethod);
        Assert.Equal(
            "http://localhost:3000/v1/responses",
            handler.ReceivedUri?.ToString());
    }

    [Fact]
    public async Task CreateMemorySendsSourceIdentityAndReadsEvidence()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.Created,
            ActiveMemoryJson));
        using var httpClient = NewHttpClient(handler);
        var service = new ApiService(httpClient);

        var memory = await service.CreateMemoryAsync(
            sourceMessageId: 42,
            movementId: Sonata.Desktop.Models.PrototypeMovement.Id,
            text: "The backend uses C#.",
            type: "ProjectContext");

        Assert.Equal(MemoryId, memory.Id);
        Assert.Equal(42, memory.SourceNote.MessageId);
        Assert.Equal("Use C# for the backend.", memory.SourceNote.Excerpt);
        Assert.True(memory.IsActive);

        using var requestJson = JsonDocument.Parse(handler.ReceivedContent!);
        var root = requestJson.RootElement;
        Assert.Equal(42, root.GetProperty("sourceMessageId").GetInt64());
        Assert.Equal(
            Sonata.Desktop.Models.PrototypeMovement.Id,
            root.GetProperty("movementId").GetGuid());
        Assert.Equal(
            "ProjectContext",
            root.GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetMemoriesReadsTheListEnvelope()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            $$"""
            {
              "memories": [{{ActiveMemoryJson}}]
            }
            """));
        using var httpClient = NewHttpClient(handler);
        var service = new ApiService(httpClient);

        var memories = await service.GetMemoriesAsync(
            Sonata.Desktop.Models.PrototypeMovement.Id);

        var memory = Assert.Single(memories);
        Assert.Equal(MemoryId, memory.Id);
        Assert.Equal("Active", memory.LifecycleState);
        Assert.Equal(
            "http://localhost:3000/v1/movements/10000000-0000-0000-0000-000000000001/memories",
            handler.ReceivedUri?.ToString());
    }

    [Fact]
    public async Task ArchiveMemoryReadsTheNewLifecycleState()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            ArchivedMemoryJson));
        using var httpClient = NewHttpClient(handler);
        var service = new ApiService(httpClient);

        var memory = await service.ArchiveMemoryAsync(MemoryId);

        Assert.Equal("Archived", memory.LifecycleState);
        Assert.False(memory.IsActive);
        Assert.Equal(HttpMethod.Post, handler.ReceivedMethod);
        Assert.Equal(
            "http://localhost:3000/v1/memories/30000000-0000-0000-0000-000000000001/archive",
            handler.ReceivedUri?.ToString());
    }

    [Fact]
    public async Task StructuredApiFailureUsesTheSafeServerMessage()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.BadRequest,
            """
            {
              "error": "SourceMessageMustBeUser",
              "message": "Only a user Message can be saved as a Memory."
            }
            """));
        using var httpClient = NewHttpClient(handler);
        var service = new ApiService(httpClient);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.CreateMemoryAsync(
                sourceMessageId: 42,
                movementId: Sonata.Desktop.Models.PrototypeMovement.Id,
                text: "The backend uses C#.",
                type: "ProjectContext"));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("SourceMessageMustBeUser", exception.ErrorCode);
        Assert.Equal(
            "Only a user Message can be saved as a Memory.",
            exception.Message);
    }

    [Fact]
    public async Task UnstructuredApiFailureUsesANeutralFallback()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                ReasonPhrase = "Service Unavailable",
                Content = new StringContent(
                    "<html>temporary proxy page</html>",
                    Encoding.UTF8,
                    "text/html")
            });
        using var httpClient = NewHttpClient(handler);
        var service = new ApiService(httpClient);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.GetMemoriesAsync(
                Sonata.Desktop.Models.PrototypeMovement.Id));

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            exception.StatusCode);
        Assert.Null(exception.ErrorCode);
        Assert.Equal(
            "The API returned 503 (Service Unavailable).",
            exception.Message);
        Assert.DoesNotContain("proxy page", exception.Message);
    }
    
    private const string ActiveMemoryJson =
        """
        {
          "id": "30000000-0000-0000-0000-000000000001",
          "movementId": "10000000-0000-0000-0000-000000000001",
          "text": "The backend uses C#.",
          "type": "ProjectContext",
          "lifecycleState": "Active",
          "sourceNote": {
            "id": "40000000-0000-0000-0000-000000000001",
            "messageId": 42,
            "excerpt": "Use C# for the backend.",
            "createdAt": "2026-07-19T12:00:00+00:00"
          },
          "createdAt": "2026-07-19T12:00:00+00:00"
        }
        """;

    private const string ArchivedMemoryJson =
        """
        {
          "id": "30000000-0000-0000-0000-000000000001",
          "movementId": "10000000-0000-0000-0000-000000000001",
          "text": "The backend uses C#.",
          "type": "ProjectContext",
          "lifecycleState": "Archived",
          "sourceNote": {
            "id": "40000000-0000-0000-0000-000000000001",
            "messageId": 42,
            "excerpt": "Use C# for the backend.",
            "createdAt": "2026-07-19T12:00:00+00:00"
          },
          "createdAt": "2026-07-19T12:00:00+00:00"
        }
        """;

    private static HttpClient NewHttpClient(
        HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:3000/v1/")
        };
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json")
        };
    }
}