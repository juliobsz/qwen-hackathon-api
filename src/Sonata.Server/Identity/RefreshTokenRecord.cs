using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sonata.Server.Identity;

[Table("refresh_token_records")]
public sealed class RefreshTokenRecord
{
    [Column("id")]
    public Guid Id { get; set; } =  Guid.NewGuid();
    
    [Column("user_id")]
    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;
    
    [Column("family_id")]
    public Guid FamilyId { get; set; }
    
    [Column("token_hash")]
    [MaxLength(64)]
    public string TokenHash { get; set; } = null!;
    
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
    
    [Column("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }
    
    [Column("revoked_at")]
    public DateTimeOffset? RevokedAt { get; set; }
    
    [Column("replace_by_record_id")]
    public Guid? ReplaceByRecordId { get; set; }
}