namespace Sonata.Desktop.Models;

public class Message
{
    public long Id { get; set; }
    public Guid SessionId  { get; set; }
    public string Content { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
