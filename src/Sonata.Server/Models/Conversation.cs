using System.ComponentModel.DataAnnotations.Schema;

namespace Sonata.Server.Models;

[Table("conversations")]
public sealed class Conversation
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }
    
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    [Column("ended_at")]
    public DateTimeOffset? EndedAt { get; set; }
    
    [Column("movement_id")]
    public Guid MovementId { get; set; }
    
    public Movement Movement { get; set; } = null!;
    
    public ICollection<Message> Messages { get; } = new List<Message>();
}
