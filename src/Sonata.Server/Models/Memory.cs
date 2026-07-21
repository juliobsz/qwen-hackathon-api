using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sonata.Server.Models;

[Table("memories")]
public sealed class Memory
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }
    
    [Column("movement_id")]
    public Guid MovementId { get; set; }

    public Movement Movement { get; set; } = null!;
    
    [Column("source_note_id")]
    public Guid SourceNoteId { get; set; }

    public SourceNote SourceNote { get; set; } = null!;
    
    [Column("text")]
    [MaxLength(500)]
    public string Text { get; set; } = null!;
    
    [Column("type")]
    public MemoryType Type { get; set; }
    
    [Column("lifecycle_state")]
    public MemoryLifecycleState LifecycleState { get; set; } = MemoryLifecycleState.Active;
    
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MemoryUse> Uses { get; } = new List<MemoryUse>();
}