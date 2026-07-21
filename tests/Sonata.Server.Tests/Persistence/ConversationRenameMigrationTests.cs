using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sonata.Server.Data;
using Testcontainers.PostgreSql;

namespace Sonata.Server.Tests.Persistence;

public sealed class ConversationRenameMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:18.4-alpine")
            .WithDatabase("sonata_migration_tests")
            .WithUsername("sonata")
            .WithPassword("sonata-tests-only")
            .Build();

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RenamePreservesDataBeforeOwnershipMigrationRemovesOwnerlessPrototypeRows()
    {
        await using var context = CreateDbContext();
        var migrator = context.Database.GetService<IMigrator>();

        await migrator.MigrateAsync("20260718202538_AddMessageSequence");

        var conversationId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO sessions (id, started_at)
            VALUES ({conversationId}, {createdAt});
            """);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO messages
                (session_id, sequence, content, role, created_at)
            VALUES
                ({conversationId}, 1, {"preserve me"},
                 {"user"}, {createdAt});
            """);

        await migrator.MigrateAsync(
            "20260719052952_AddHackathonMemoryPersistence");

        await context.Database.OpenConnectionAsync();
        await using (var command = context.Database
                         .GetDbConnection()
                         .CreateCommand())
        {
            command.CommandText = """
                SELECT c.id, c.movement_id, m.name,
                       msg.content, msg.sequence
                FROM conversations AS c
                INNER JOIN movements AS m
                    ON m.id = c.movement_id
                INNER JOIN messages AS msg
                    ON msg.conversation_id = c.id
                WHERE c.id = @conversation_id;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "conversation_id";
            parameter.Value = conversationId;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(conversationId, reader.GetGuid(0));
            Assert.Equal(
                Guid.Parse("10000000-0000-0000-0000-000000000001"),
                reader.GetGuid(1));
            Assert.Equal("Qwen AI Hackathon", reader.GetString(2));
            Assert.Equal("preserve me", reader.GetString(3));
            Assert.Equal(1, reader.GetInt32(4));
        }

        await context.Database.CloseConnectionAsync();
        await context.Database.MigrateAsync();

        Assert.False(await context.Conversations
            .AnyAsync(item => item.Id == conversationId));
        Assert.False(await context.Messages
            .AnyAsync(item => item.ConversationId == conversationId));
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public Task DisposeAsync()
    {
        return _container.DisposeAsync().AsTask();
    }

    private ApplicationDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(_container.GetConnectionString())
                .Options;

        return new ApplicationDbContext(options);
    }
}
