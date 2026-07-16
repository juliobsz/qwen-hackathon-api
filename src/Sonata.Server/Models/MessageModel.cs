using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sonata.Server.Models;

[Table("messages")]
public class Message
{
    [Column("id")]
    public long Id { get; set; }
    [Column("session_id")]
    public Guid SessionId { get; set; }
    [Column("content")]
    [MaxLength(2000)]
    public string Content { get; set; } = null!;
    [Column("role")]
    [MaxLength(50)]
    public string Role { get; set; } = null!;
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }  = DateTimeOffset.UtcNow;
}
