using JobScout.Core.Abstractions;
using JobScout.Ingestion.Adzuna;
using JobScout.Ingestion.Ashby;
using JobScout.Ingestion.GoogleJobs;
using JobScout.Ingestion.Greenhouse;
using JobScout.Ingestion.Http;
using JobScout.Ingestion.Jobicy;
using JobScout.Ingestion.Lever;
using JobScout.Ingestion.TheirStack;
using JobScout.Ingestion.UsaJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobScout.Ingestion;

public static class IngestionServiceCollectionExtensions
{
    // A descriptive, contactable UA — being a courteous client of these public APIs is a rule here.
    private const string DefaultUserAgent =
        "JobScout/0.1 (+https://github.com/blindcowboy24/JobScout; job-intelligence crawler)";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Registers the job-source clients as typed <see cref="HttpClient"/>s — each with its base
    /// address, a descriptive User-Agent, and a polite back-off handler — and exposes them
    /// collectively as <see cref="IAtsClient"/> so the crawler can iterate over all sources.
    /// Company ATS sources (Greenhouse/Lever/Ashby) and Jobicy need no credentials; Adzuna is
    /// registered only when an app id/key is configured, so its absence degrades gracefully.
    /// </summary>
    public static IServiceCollection AddIngestion(this IServiceCollection services, IConfiguration config)
    {
        var ua = config["Crawl:UserAgent"];
        if (string.IsNullOrWhiteSpace(ua)) ua = DefaultUserAgent;

        services.AddTransient<PoliteRetryHandler>();

        AddAtsClient<GreenhouseClient>(services, "https://boards-api.greenhouse.io/v1/boards/", ua);
        AddAtsClient<LeverClient>(services, "https://api.lever.co/v0/postings/", ua);
        AddAtsClient<AshbyClient>(services, "https://api.ashbyhq.com/posting-api/job-board/", ua);
        AddAtsClient<JobicyClient>(services, "https://jobicy.com/api/v2/", ua);

        // Optional commercial aggregators — registered only when credentials are present, so a
        // missing key degrades to "source unavailable, feeds skipped" rather than a startup error.
        services.Configure<AdzunaOptions>(config.GetSection(AdzunaOptions.SectionName));
        if ((config.GetSection(AdzunaOptions.SectionName).Get<AdzunaOptions>() ?? new()).IsConfigured)
            AddAtsClient<AdzunaClient>(services, "https://api.adzuna.com/v1/api/jobs/", ua);

        services.Configure<SerpApiOptions>(config.GetSection(SerpApiOptions.SectionName));
        if ((config.GetSection(SerpApiOptions.SectionName).Get<SerpApiOptions>() ?? new()).IsConfigured)
            AddAtsClient<GoogleJobsClient>(services, "https://serpapi.com/", ua);

        services.Configure<TheirStackOptions>(config.GetSection(TheirStackOptions.SectionName));
        if ((config.GetSection(TheirStackOptions.SectionName).Get<TheirStackOptions>() ?? new()).IsConfigured)
            AddAtsClient<TheirStackClient>(services, "https://api.theirstack.com/", ua);

        services.Configure<UsaJobsOptions>(config.GetSection(UsaJobsOptions.SectionName));
        if ((config.GetSection(UsaJobsOptions.SectionName).Get<UsaJobsOptions>() ?? new()).IsConfigured)
            AddAtsClient<UsaJobsClient>(services, "https://data.usajobs.gov/", ua);

        return services;
    }

    private static void AddAtsClient<TClient>(IServiceCollection services, string baseAddress, string userAgent)
        where TClient : class, IAtsClient
    {
        services.AddHttpClient<TClient>(http =>
            {
                http.BaseAddress = new Uri(baseAddress);
                http.Timeout = RequestTimeout;
                http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
                http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            })
            .AddHttpMessageHandler<PoliteRetryHandler>();

        // Surface the concrete typed client through the IAtsClient collection the crawler consumes.
        services.AddTransient<IAtsClient>(sp => sp.GetRequiredService<TClient>());
    }
}
