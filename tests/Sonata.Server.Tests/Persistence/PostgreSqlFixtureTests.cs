using Microsoft.EntityFrameworkCore;

namespace  Sonata.Server.Tests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgreSqlFixtureTests(PostgreSqlFixture fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task AppliesTrackedMigrations()
    {
        await using var context = fixture.CreateDbContext();
        
        var canConnect = await context.Database.CanConnectAsync();
        var migrations = await context.Database.GetAppliedMigrationsAsync();
        
        Assert.True(canConnect);
        Assert.Contains("20260714200047_InitialCreate", migrations);
    }
}