using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sonata.Server.Data;
using Sonata.Server.Models;

namespace Sonata.Server.Identity;

public sealed class AccountService(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ApplicationDbContext db,
    JwtTokenIssuer tokens,
    TimeProvider timeProvider) : IAccountService
{
    private static readonly TimeSpan RememberedLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan TemporaryLifetime = TimeSpan.FromHours(8);

    public async Task<AuthenticationResult> RegisterAsync(RegisterAccountCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var email = command.Email.Trim();
        if (email.Length == 0 || email.Length > 254)
        {
            throw new AccountFailureException(AccountFailure.InvalidInput, "Enter a valid email address.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            CreatedAt = timeProvider.GetUtcNow()
        };
        var result = await users.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            var duplicate =
                result.Errors.Any(error => error.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
            throw new AccountFailureException(duplicate ? AccountFailure.EmailAlreadyRegistered : AccountFailure.InvalidInput, 
                duplicate ? "An account already uses that email address." : string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        var movement = new Movement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Welcome to Sonata",
            StartedAt = timeProvider.GetUtcNow(),
        };
        db.Add(movement);
        await db.SaveChangesAsync(cancellationToken);

        var authentication = await CreateAuthenticationAsync(
            user,
            movement.Id,
            command.RememberMe,
            Guid.NewGuid(),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        
        return authentication;
    }

    public async Task<AuthenticationResult> LoginAsync(LoginAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(command.Email.Trim());
        if (user == null) throw InvalidCredentials();

        var result = await signIn.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            throw new AccountFailureException(AccountFailure.LockedOut,
                "Sign-in is temporarily unavailable. Try again later.");
        }
        if (!result.Succeeded) throw InvalidCredentials();

        var movementId = await db.Movements
            .Where(movement => movement.UserId == user.Id)
            .OrderBy(movement => movement.StartedAt)
            .Select(movement => movement.Id)
            .FirstAsync(cancellationToken);
        
        return await CreateAuthenticationAsync(
            user,
            movementId,
            command.RememberMe,
            Guid.NewGuid(),
            cancellationToken);
    }

    public async Task<AuthenticationResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);
        var hash = JwtTokenIssuer.HashRefresh(refreshToken);
        var current = await db.RefreshTokenRecords
            .FromSqlInterpolated($"SELECT * FROM refresh_token_records WHERE token_hash = {hash} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (current == null) throw InvalidRefreshCredential();
        if (current.ReplaceByRecordId != null)
        {
            await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidRefreshCredential();
        }
        if (current.RevokedAt != null || current.ExpiresAt <= now) throw InvalidRefreshCredential();

        var user = await users.FindByIdAsync(current.UserId.ToString()) ??
                   throw InvalidRefreshCredential();
        var movementId = await db.Movements
            .Where(movement => movement.UserId == user.Id)
            .OrderBy(movement => movement.StartedAt)
            .Select(movement => movement.Id)
            .FirstAsync(cancellationToken);
        var lifetime = current.ExpiresAt - current.CreatedAt;
        var replacementToken = JwtTokenIssuer.IssueRefresh();
        var replacement = new RefreshTokenRecord
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FamilyId = current.FamilyId,
            TokenHash = replacementToken.Hash,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        };

        current.RevokedAt = now;
        current.ReplaceByRecordId = replacement.Id;
        db.RefreshTokenRecords.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var access = tokens.Issue(user);
        return new AuthenticationResult(
            user.Id,
            user.Email ?? string.Empty,
            access.Value,
            access.ExpiresAt,
            replacementToken.RawValue,
            replacement.ExpiresAt,
            replacement.FamilyId,
            movementId);
    }

    public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken)
    {
        var hash = JwtTokenIssuer.HashRefresh(refreshToken);
        var record = await db.RefreshTokenRecords.SingleOrDefaultAsync(
            candidate => candidate.UserId == userId &&
            candidate.TokenHash == hash, cancellationToken);

        if (record == null || record.RevokedAt != null) return;
        
        record.RevokedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthenticationResult> CreateAuthenticationAsync(ApplicationUser user, Guid movementId,
        bool rememberMe, Guid familyId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var refresh = JwtTokenIssuer.IssueRefresh();
        var refreshEntity = new RefreshTokenRecord
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FamilyId = familyId,
            TokenHash = refresh.Hash,
            CreatedAt = now,
            ExpiresAt = now.Add(rememberMe ? RememberedLifetime : TemporaryLifetime)
        };
        
        db.RefreshTokenRecords.Add(refreshEntity);
        await db.SaveChangesAsync(cancellationToken);
        
        var access = tokens.Issue(user);
        return new AuthenticationResult(
            user.Id,
            user.Email ?? string.Empty,
            access.Value,
            access.ExpiresAt,
            refresh.RawValue,
            refreshEntity.ExpiresAt,
            familyId,
            movementId);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        var records = await db.RefreshTokenRecords
            .Where(record => record.FamilyId == familyId && record.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var record in records)
        {
            record.RevokedAt = revokedAt;
        }
        
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AccountFailureException InvalidCredentials()
    {
        return new AccountFailureException(AccountFailure.InvalidCredentials, "Email or password is invalid.");
    }

    private static AccountFailureException InvalidRefreshCredential()
    {
        return new AccountFailureException(AccountFailure.InvalidRefreshCredential, "The refresh credential is no longer valid.");
    }
}