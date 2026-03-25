using Microsoft.Extensions.DependencyInjection;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Data.Migrations;
using SamurAICouncil.Data.Repositories;

namespace SamurAICouncil.Data;

public static class DependencyInjection
{
    /// <summary>
    /// Add data layer services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="useResilientRepositories">Whether to wrap repositories with Polly resilience policies.</param>
    public static IServiceCollection AddDataServices(
        this IServiceCollection services,
        string connectionString,
        bool useResilientRepositories = true)
    {
        // Register base repositories
        services.AddScoped<ConversationRepository>(_ => new ConversationRepository(connectionString));
        services.AddScoped<MessageRepository>(_ => new MessageRepository(connectionString));

        if (useResilientRepositories)
        {
            // Register resilient wrappers as the interface implementations
            services.AddScoped<IConversationRepository>(sp =>
                new ResilientConversationRepository(sp.GetRequiredService<ConversationRepository>()));
            services.AddScoped<IMessageRepository>(sp =>
                new ResilientMessageRepository(sp.GetRequiredService<MessageRepository>()));
        }
        else
        {
            // Register base repositories directly
            services.AddScoped<IConversationRepository>(sp => sp.GetRequiredService<ConversationRepository>());
            services.AddScoped<IMessageRepository>(sp => sp.GetRequiredService<MessageRepository>());
        }

        return services;
    }

    /// <summary>
    /// Add FluentMigrator services for database migrations.
    /// </summary>
    public static IServiceCollection AddMigrations(this IServiceCollection services, string connectionString)
    {
        return services.AddFluentMigrator(connectionString);
    }
}
