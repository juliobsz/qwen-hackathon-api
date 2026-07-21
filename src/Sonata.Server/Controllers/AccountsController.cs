using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sonata.Server.Identity;
using Sonata.Server.Security;

namespace Sonata.Server.Controllers;

public sealed record RegisterRequest(string Email, string Password, bool RememberMe);

public sealed record LoginRequest(string Email, string Password, bool RememberMe);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record AuthenticationResponse(
    Guid UserId,
    string Email,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid InitialMovementId);

[ApiController]
[Route("v1/accounts")]
public sealed class AccountsController(IAccountService accounts) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("account")]
    [HttpPost("register")]
    public Task<ActionResult<AuthenticationResponse>> Register(RegisterRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(() => accounts.RegisterAsync(
            new RegisterAccountCommand(
                request.Email,
                request.Password,
                request.RememberMe),
            cancellationToken));
    }
    
    [AllowAnonymous]
    [EnableRateLimiting("account")]
    [HttpPost("login")]
    public Task<ActionResult<AuthenticationResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        return ExecuteAsync(() => accounts.LoginAsync(
            new LoginAccountCommand(
                request.Email,
                request.Password,
                request.RememberMe),
            cancellationToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting("refresh")]
    [HttpPost("refresh")]
    public Task<ActionResult<AuthenticationResponse>> Refresh(RefreshRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(() => accounts.RefreshAsync(
            request.RefreshToken,
            cancellationToken));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<ActionResult<IActionResult>> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await accounts.LogoutAsync(
            User.RequireUserId(),
            request.RefreshToken,
            cancellationToken);

        return NoContent();
    }

    private async Task<ActionResult<AuthenticationResponse>> ExecuteAsync(Func<Task<AuthenticationResult>> operation)
    {
        try
        {
            var result = await operation();
            return Ok(ToResponse(result));
        }
        catch (AccountFailureException exception)
        {
            var status = exception.Failure switch
            {
                AccountFailure.InvalidInput => 400,
                AccountFailure.EmailAlreadyRegistered => 409,
                AccountFailure.LockedOut => 429,
                _ => 401
            };
            
            return StatusCode(status, new
            {
                error = exception.Failure.ToString(),
                message = exception.Message
            });
        }
    }

    private static AuthenticationResponse ToResponse(AuthenticationResult result)
    {
        return new AuthenticationResponse(
            result.UserId,
            result.Email,
            result.AccessToken,
            result.AccessTokenExpiresAt,
            result.RefreshToken,
            result.RefreshTokenExpiresAt,
            result.InitialMovementId);
    }
}