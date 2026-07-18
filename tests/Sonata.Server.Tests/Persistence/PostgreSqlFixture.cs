using Microsoft.EntityFrameworkCore;
using Sonata.Server.Data;
using Testcontainers.PostgreSql;

namespace Sonata.Server.Tests.Persistence;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18.4-alpine")
        .WithDatabase("sonata_tests")
        .WithUsername("sonata")
        .WithPassword("sonata-tests-only")
        .Build();

    public ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        
        return new ApplicationDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        return _container.DisposeAsync().AsTask();
    }
}