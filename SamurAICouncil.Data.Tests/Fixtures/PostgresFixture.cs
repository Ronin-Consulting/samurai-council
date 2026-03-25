using Microsoft.Extensions.DependencyInjection;
using SamurAICouncil.Data.Migrations;
using Testcontainers.PostgreSql;

namespace SamurAICouncil.Data.Tests.Fixtures;

/// <summary>
/// Shared PostgreSQL container fixture for integration tests.
/// Uses MSTest's AssemblyInitialize/AssemblyCleanup for lifecycle management.
/// </summary>
public static class PostgresFixture
{
    private static PostgreSqlContainer? _container;
    private static string? _connectionString;
    private static bool _migrationsRun;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public static string ConnectionString => _connectionString
        ?? throw new InvalidOperationException("PostgreSQL container not initialized. Call InitializeAsync first.");

    /// <summary>
    /// Initialize the PostgreSQL container and run migrations.
    /// Safe to call multiple times - will only initialize once.
    /// </summary>
    public static async Task InitializeAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_container != null)
                return;

            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("samuraicouncil_test")
                .WithUsername("test")
                .WithPassword("test")
                .Build();

            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();

            // Run migrations
            await RunMigrationsAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Run database migrations if not already run.
    /// </summary>
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

    /// <summary>
    /// Clean up the PostgreSQL container.
    /// </summary>
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

    /// <summary>
    /// Clear all data from tables (for test isolation).
    /// </summary>
    public static async Task ClearDataAsync()
    {
        if (_connectionString == null)
            return;

        await using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Delete in order respecting foreign keys
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "TRUNCATE TABLE messages, conversations RESTART IDENTITY CASCADE";
        await cmd.ExecuteNonQueryAsync();
    }
}
