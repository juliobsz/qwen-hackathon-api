using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sonata.Server.Data;
using Sonata.Server.Identity;
using Sonata.Server.Tests.Persistence;

namespace Sonata.Server.Tests.Identity;

[Collection(PostgreSqlCollection.Name)]
public sealed class AccountServiceTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task RefreshRotatesTokenAndRejectsReuse()
    {
        await using var db = database.CreateContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        var services = IdentityTestServices.Create(database.ConnectionString);
        await using var scope = services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();

        var registered = await accounts.RegisterAsync(
            new RegisterAccountCommand(
                "judge@example.com",
                "Correct-Horse-7-Battery!",
                true),
            CancellationToken.None);

        var rotated = await accounts.RefreshAsync(
            registered.RefreshToken,
            CancellationToken.None);

        Assert.NotEqual(registered.RefreshToken, rotated.RefreshToken);

        var exception = await Assert.ThrowsAsync<AccountFailureException>(
            () => accounts.RefreshAsync(
                registered.RefreshToken,
                CancellationToken.None));

        Assert.Equal(AccountFailure.InvalidRefreshCredential, exception.Failure);

        await using var verification = database.CreateContext();
        var family = await verification.RefreshTokenRecords
            .Where(record => record.FamilyId == registered.RefreshFamilyId)
            .ToListAsync();

        Assert.All(family, record =>
            Assert.NotNull(record.RevokedAt));
    }

    [Fact]
    public async Task LoginFailureDoesNotRevealWhetherEmailExists()
    {
        var services = IdentityTestServices.Create(database.ConnectionString);
        await using var scope = services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();

        var missing = await Assert.ThrowsAsync<AccountFailureException>(
            () => accounts.LoginAsync(
                new LoginAccountCommand(
                    "missing@example.com",
                    "Incorrect-Password-9!",
                    false),
                CancellationToken.None));

        Assert.Equal(AccountFailure.InvalidCredentials, missing.Failure);
        Assert.Equal("Email or password is invalid.", missing.Message);
    }
    
    [Fact]
    public async Task ConcurrentRefreshRevokesTheRotatedFamily()
    {
        await using var reset = database.CreateContext();
        await reset.Database.EnsureDeletedAsync();
        await reset.Database.MigrateAsync();
        var services = IdentityTestServices.Create(
            database.ConnectionString);
        string original;
        await using (var registrationScope =
                     services.CreateAsyncScope())
        {
            var accounts = registrationScope.ServiceProvider
                .GetRequiredService<IAccountService>();
            var registered = await accounts.RegisterAsync(
                new RegisterAccountCommand(
                    "race@example.com",
                    "Correct-Horse-7-Battery!",
                    true),
                CancellationToken.None);
            original = registered.RefreshToken;
        }

        var start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<AuthenticationResult?> TryRefreshAsync()
        {
            await start.Task;
            await using var scope = services.CreateAsyncScope();
            var accounts = scope.ServiceProvider
                .GetRequiredService<IAccountService>();
            try
            {
                return await accounts.RefreshAsync(
                    original,
                    CancellationToken.None);
            }
            catch (AccountFailureException exception)
                when (exception.Failure ==
                      AccountFailure.InvalidRefreshCredential)
            {
                return null;
            }
        }

        var first = TryRefreshAsync();
        var second = TryRefreshAsync();
        start.SetResult();
        var outcomes = await Task.WhenAll(first, second);
        var successful = Assert.Single(
            outcomes,
            result => result is not null);

        await using var verificationScope =
            services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider
            .GetRequiredService<IAccountService>();
        var exception = await Assert.ThrowsAsync<
            AccountFailureException>(
                () => verification.RefreshAsync(
                    successful!.RefreshToken,
                    CancellationToken.None));
        Assert.Equal(
            AccountFailure.InvalidRefreshCredential,
            exception.Failure);
    }
}

internal static class IdentityTestServices
{
    public static ServiceProvider Create(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<ApplicationDbContext>(
            options => options.UseNpgsql(connectionString));
        services.AddIdentityCore<ApplicationUser>(options =>
                AccountIdentityOptions.Configure(options))
            .AddSignInManager()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddOptions<JwtOptions>()
            .Configure(options =>
            {
                options.Issuer = "Sonata.Tests";
                options.Audience = "Sonata.Desktop.Tests";
                options.SigningKey =
                    "test-only-signing-key-with-at-least-32-bytes";
            });
        services.AddScoped<JwtTokenIssuer>();
        services.AddScoped<IAccountService, AccountService>();
        return services.BuildServiceProvider();
    }
}
