namespace Sonata.Server.Tests.Persistence;

public static class PostgreSqlCollection
{
    public const string Name = "PostgreSQL integration tests";
}

[CollectionDefinition(PostgreSqlCollection.Name)]
public sealed class PostgreSqlCollectionDefinition : ICollectionFixture<PostgreSqlFixture> { }