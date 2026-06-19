using JobScout.Core.Abstractions;
using JobScout.Core.Model;
using JobScout.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobScout.Worker;

/// <summary>
/// The crawl loop. On startup it ensures the database exists, then every configured interval it
/// fetches each board, reconciles the results into the store (which advances history and
/// rescores), and logs a ranked read-out of the highest-intent roles.
/// </summary>
/// <remarks>
/// A failure crawling one board is logged and skipped — it never aborts the cycle or the loop —
/// so one flaky ATS or bad slug can't starve the others.
/// </remarks>
public sealed class CrawlService(
    IServiceScopeFactory scopeFactory,
    IOptions<CrawlOptions> options,
    ILogger<CrawlService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        await EnsureDatabaseAsync(stoppingToken);

        if (opts.Boards.Count == 0)
        {
            logger.LogWarning(
                "No boards configured under '{Section}'. Add some and restart; nothing to crawl.",
                CrawlOptions.SectionName);
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, opts.IntervalMinutes));
        logger.LogInformation(
            "JobScout starting: {BoardCount} board(s), crawling every {Interval}.",
            opts.Boards.Count, interval);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await RunCycleAsync(opts, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Crawl cycle failed; will retry next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunCycleAsync(CrawlOptions opts, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var clients = scope.ServiceProvider.GetServices<IAtsClient>().ToDictionary(c => c.Source);

        var observedAt = DateTimeOffset.UtcNow;
        logger.LogInformation("--- Crawl cycle @ {Time:u} ---", observedAt);

        foreach (var board in opts.Boards)
        {
            if (!clients.TryGetValue(board.Source, out var client))
            {
                logger.LogWarning(
                    "Source {Source} is not available (no client registered — e.g. missing API key); skipping feed '{Feed}'.",
                    board.Source, board.Feed);
                continue;
            }

            try
            {
                var postings = await client.FetchPostingsAsync(board.Feed, ct);
                var summary = await repo.RecordCrawlAsync(board.Source, board.Feed, postings, observedAt, ct);
                logger.LogInformation(
                    "{Source}/{Feed}: {Live} live ({New} new, {Reappeared} reappeared, {Closed} closed).",
                    summary.Source, summary.Feed, summary.TotalObserved,
                    summary.New, summary.Reappeared, summary.Closed);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed crawling {Source}/{Feed}; skipping this feed.",
                    board.Source, board.Feed);
            }
        }

        await LogTopPostingsAsync(repo, opts.TopN, ct);
    }

    private async Task LogTopPostingsAsync(IJobRepository repo, int topN, CancellationToken ct)
    {
        var top = await repo.GetTopPostingsAsync(topN, ct);
        if (top.Count == 0) return;

        logger.LogInformation("Top {Count} high-intent roles:", top.Count);
        var rank = 1;
        foreach (var p in top)
        {
            logger.LogInformation(
                "  #{Rank,-2} [{Score,5:0.0} {Band,-6}] {Title} — {Company} ({Source}){Location}",
                rank++, p.Score, p.Band, p.Title, p.Company, p.Source,
                string.IsNullOrWhiteSpace(p.Location) ? "" : $" · {p.Location}");
        }
    }

    private async Task EnsureDatabaseAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobScoutDbContext>();
        // EnsureCreated keeps local dev zero-setup and stays provider-agnostic (SQLite or SQL Server).
        await db.Database.EnsureCreatedAsync(ct);
    }
}
