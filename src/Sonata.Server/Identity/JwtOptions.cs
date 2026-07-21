using System.ComponentModel.DataAnnotations;

namespace Sonata.Server.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    
    [Required]
    public string Issuer { get; set; } = null!;
    
    [Required]
    public string Audience { get; set; } = null!;
    
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = null!;
    
    [Range(5, 15)]
    public int AccessMinutes { get; set; } = 10;
}