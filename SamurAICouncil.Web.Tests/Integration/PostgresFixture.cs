using Microsoft.Extensions.DependencyInjection;
using SamurAICouncil.Data.Migrations;
using Testcontainers.PostgreSql;

namespace SamurAICouncil.Web.Tests.Integration;

/// <summary>
/// Shared PostgreSQL container fixture for Web integration tests.
/// </summary>
public static class PostgresFixture
{
    private static PostgreSqlContainer? _container;
    private static string? _connectionString;
    private static bool _migrationsRun;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public static string ConnectionString => _connectionString
        ?? throw new InvalidOperationException("PostgreSQL container not initialized. Call InitializeAsync first.");

    public static async Task InitializeAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_container != null)
                return;

            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("samuraicouncil_web_test")
                .WithUsername("test")
                .WithPassword("test")
                .Build();

            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();

            await RunMigrationsAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static async Task RunMigrationsAsync()
    {
        if (_migrationsRun)
            return;

        var services = new ServiceCollection();
        services.AddFluentMigrator(ConnectionString);

        await using var serviceProvider = services.BuildServiceProvider();
        MigrationRunner.RunMigrations(serviceProvider);

        _migrationsRun = true;
    }

    public static async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
            _container = null;
            _connectionString = null;
            _migrationsRun = false;
        }
    }

    public static async Task ClearDataAsync()
    {
        if (_connectionString == null)
            return;

        await using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "TRUNCATE TABLE messages, conversations RESTART IDENTITY CASCADE";
        await cmd.ExecuteNonQueryAsync();
    }
}
