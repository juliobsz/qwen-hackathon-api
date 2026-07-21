using Microsoft.AspNetCore.Identity;

namespace Sonata.Server.Identity;

public sealed record RegisterAccountCommand(
    string Email,
    string Password,
    bool RememberMe);
    
public sealed record LoginAccountCommand(
    string Email,
    string Password,
    bool RememberMe);
    
public sealed record AuthenticationResult(
    Guid UserId,
    string Email,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid RefreshFamilyId,
    Guid InitialMovementId);

public enum AccountFailure
{
    InvalidInput,
    EmailAlreadyRegistered,
    InvalidCredentials,
    LockedOut,
    InvalidRefreshCredential
}

public sealed class AccountFailureException(AccountFailure failure, string message) : Exception(message)
{
    public AccountFailure Failure { get; } = failure;
}

public static class AccountIdentityOptions
{
    public static void Configure(IdentityOptions options)
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    }
}