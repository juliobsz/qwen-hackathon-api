using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sonata.Server.Models;

[Table("movements")]
public sealed class Movement
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }
    
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = null!;
    
    [Column("started_at")]
    public DateTimeOffset StartedAt { get; set; } =  DateTimeOffset.UtcNow;
    
    public ICollection<Conversation> Conversations { get; } = new List<Conversation>();
    
    public ICollection<Memory> Memories { get; } = new List<Memory>();
}