using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;

namespace SamurAICouncil.Data.Migrations;

public static class MigrationRunner
{
    /// <summary>
    /// Configure FluentMigrator services for dependency injection.
    /// </summary>
    public static IServiceCollection AddFluentMigrator(this IServiceCollection services, string connectionString)
    {
        services.AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(MigrationRunner).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        return services;
    }

    /// <summary>
    /// Run all pending migrations.
    /// </summary>
    public static void RunMigrations(IServiceProvider serviceProvider)
    {
        var runner = serviceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }

    /// <summary>
    /// Roll back the last migration.
    /// </summary>
    public static void RollbackLastMigration(IServiceProvider serviceProvider)
    {
        var runner = serviceProvider.GetRequiredService<IMigrationRunner>();
        runner.Rollback(1);
    }

    /// <summary>
    /// Roll back to a specific migration version.
    /// </summary>
    public static void RollbackToVersion(IServiceProvider serviceProvider, long version)
    {
        var runner = serviceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateDown(version);
    }

    /// <summary>
    /// Create a standalone service provider for running migrations.
    /// Use this when running migrations outside of the main application.
    /// </summary>
    public static IServiceProvider CreateMigrationServiceProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddFluentMigrator(connectionString);
        return services.BuildServiceProvider();
    }
}
