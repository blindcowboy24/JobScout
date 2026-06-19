using JobScout.Core.Abstractions;
using JobScout.Core.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobScout.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DbContext (provider chosen from config), the repository, and the scorer.
    /// <c>Database:Provider</c> selects <c>Sqlite</c> (default, zero-setup) or <c>SqlServer</c>;
    /// <c>ConnectionStrings:JobScout</c> supplies the connection (defaulting to a local SQLite file).
    /// </summary>
    public static IServiceCollection AddJobScoutData(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Database:Provider"] ?? "Sqlite";
        var connectionString = config.GetConnectionString("JobScout");

        services.AddDbContext<JobScoutDbContext>(options =>
        {
            switch (provider.ToLowerInvariant())
            {
                case "sqlserver":
                    options.UseSqlServer(connectionString
                        ?? throw new InvalidOperationException(
                            "Database:Provider=SqlServer requires ConnectionStrings:JobScout."));
                    break;

                case "sqlite":
                    options.UseSqlite(connectionString ?? "Data Source=jobscout.db");
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unknown Database:Provider '{provider}'. Use 'Sqlite' or 'SqlServer'.");
            }
        });

        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IPostingReadStore, PostingReadStore>();
        services.AddSingleton<IIntentScorer, IntentScorer>();

        return services;
    }
}
