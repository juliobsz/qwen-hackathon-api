using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Sonata.Desktop.Models;

namespace Sonata.Desktop.Services;

public class ApiService(HttpClient? httpClient = null)
{
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient
    {
        BaseAddress = new Uri("http://localhost:3000/v1/"),
    };
    
    public async Task<ContinueConversationResponse> SendMessageAsync(string content, Guid conversationId)
    {
        using var response = await _httpClient.PostAsJsonAsync("responses", new 
            { content, conversationId });

        return await ReadRequiredBodyAsync<ContinueConversationResponse>(response);
    }

    public async Task<ObservableCollection<Conversation>> GetAllConversationsAsync()
    {
        using var response = await _httpClient.GetAsync("conversations");
        var body = await ReadRequiredBodyAsync<ConversationListResponse>(response);

        return new ObservableCollection<Conversation>(body.Conversations);
    }

    public async Task<ObservableCollection<Message>> GetMessagesAsync(
        Guid conversationId)
    {
        using var response = await _httpClient.GetAsync($"conversations/{conversationId}/messages");
        var body = await ReadRequiredBodyAsync<MessageResponse>(response);

        return new ObservableCollection<Message>(body.Messages);
    }

    public async Task<ObservableCollection<MemoryItem>> GetMemoriesAsync(Guid movementId)
    {
        using var response = await _httpClient.GetAsync($"movements/{movementId}/memories");
        var body = await ReadRequiredBodyAsync<MemoryListResponse>(response);

        return new ObservableCollection<MemoryItem>(body.Memories);
    }

    public async Task<MemoryItem> CreateMemoryAsync(
        long sourceMessageId,
        Guid movementId,
        string text,
        string type)
    {
        using var response = await _httpClient.PostAsJsonAsync("memories", new
            {
                sourceMessageId,
                movementId,
                text,
                type
            });

        return await ReadRequiredBodyAsync<MemoryItem>(response);
    }

    public async Task<MemoryItem> ArchiveMemoryAsync(Guid memoryId)
    {
        using var response = await _httpClient.PostAsync($"memories/{memoryId}/archive",
            content: null);

        return await ReadRequiredBodyAsync<MemoryItem>(response);
    }

    private static async Task<T> ReadRequiredBodyAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) throw await CreateApiExceptionAsync(response);
        
        var body = await response.Content.ReadFromJsonAsync<T>();
        return body ?? throw new InvalidOperationException("The API returned an empty response body.");
    }

    private static async Task<ApiException> CreateApiExceptionAsync(HttpResponseMessage response)
    {
        var reasonPhrase = response.ReasonPhrase ?? response.StatusCode.ToString();
        var fallbackMessage = $"The API returned {(int)response.StatusCode} " +
                              $"({reasonPhrase}).";
        var responseText = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(responseText))
        {
            return new ApiException(
                response.StatusCode,
                errorCode: null,
                fallbackMessage);
        }

        try
        {
            var error = JsonSerializer.Deserialize<ApiErrorResponse>(responseText,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                return new ApiException(
                    response.StatusCode,
                    error.Error,
                    error.Message);
            }
        }
        catch (JsonException)
        {
        }

        return new ApiException(
            response.StatusCode,
            errorCode: null,
            fallbackMessage);
    }
}