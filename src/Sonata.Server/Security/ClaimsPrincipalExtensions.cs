using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Sonata.Server.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid RequireUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        
        return !Guid.TryParse(value, out var userId)
            ? throw new InvalidOperationException("The authenticated principal has no valid subject.")
            : userId;
    }
}