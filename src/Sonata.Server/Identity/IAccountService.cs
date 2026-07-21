namespace Sonata.Server.Identity;

public interface IAccountService
{
    Task<AuthenticationResult> RegisterAsync(RegisterAccountCommand command, CancellationToken cancellationToken);
    
    Task<AuthenticationResult> LoginAsync(LoginAccountCommand command, CancellationToken cancellationToken);
    
    Task<AuthenticationResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    
    Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken);
}