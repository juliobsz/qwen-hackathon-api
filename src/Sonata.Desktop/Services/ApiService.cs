using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using Sonata.Desktop.Models;

namespace Sonata.Desktop.Services;

public class ApiService(HttpClient? httpClient = null)
{
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient
    {
        BaseAddress = new Uri("http://localhost:3000/v1/"),
    };
    
    public async Task<string> SendMessageAsync(string content, Guid sessionId)
    {
        var res = await _httpClient.PostAsJsonAsync("responses",
            new { content, sessionId });
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<ChatResponse>();
        return body?.Content ?? throw new InvalidOperationException("The API returned an empty response.");
    }

    public async Task<ObservableCollection<Session>> GetAllSessionsAsync()
    {
        var res = await _httpClient.GetAsync("sessions");
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<SessionResponse>();
        return new ObservableCollection<Session>(body?.Sessions ?? Array.Empty<Session>());
    }

    public async Task<ObservableCollection<Message>> GetMessagesAsync(Guid sessionId)
    {
        var res = await _httpClient.GetAsync("messages/" + sessionId);
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<MessageResponse>();
        return new ObservableCollection<Message>(body?.Messages ?? Array.Empty<Message>());
    }
}
