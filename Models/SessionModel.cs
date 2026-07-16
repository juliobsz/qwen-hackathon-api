using System.ComponentModel.DataAnnotations.Schema;
namespace sonata_api.Models;

[Table("sessions")]
public class Session
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Column("started_at")]
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    [Column("ended_at")]
    public DateTimeOffset? EndedAt { get; set; }
}