using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Abstractions;
using JobScout.Core.Model;
using JobScout.Ingestion.Http;

namespace JobScout.Ingestion.Ashby;

/// <summary>
/// Client for the Ashby public job-board posting API:
/// <c>GET api.ashbyhq.com/posting-api/job-board/{company}?includeCompensation=true</c>.
/// Only listed postings are returned to callers. Ashby discloses pay as a human-readable
/// summary rather than guaranteed numbers, so we capture that as the salary signal.
/// </summary>
public sealed class AshbyClient(HttpClient http) : IAtsClient
{
    public JobSource Source => JobSource.Ashby;

    public async Task<IReadOnlyList<JobPosting>> FetchPostingsAsync(string feed, CancellationToken ct)
    {
        var payload = await http.GetFromJsonAsync<JobsResponse>(
            $"{Uri.EscapeDataString(feed)}?includeCompensation=true", ct);

        var jobs = payload?.Jobs ?? [];
        return jobs
            .Where(j => j.IsListed)
            .Select(j => Map(feed, j))
            .ToList();
    }

    private static JobPosting Map(string company, Job j) => new()
    {
        Source = JobSource.Ashby,
        Company = company,
        ExternalId = j.Id ?? "",
        Title = j.Title ?? "(untitled)",
        Location = j.Location,
        Department = j.Department ?? j.Team,
        Remote = j.IsRemote == true ? RemoteMode.Remote : RemoteDetector.FromWorkplaceType(j.WorkplaceType),
        Description = HtmlText.ToPlainText(j.DescriptionPlain ?? j.DescriptionHtml),
        Url = j.JobUrl,
        PostedAt = j.PublishedAt,
        Salary = MapSalary(j.Compensation),
        HasScreeningQuestions = null, // not exposed by the posting API
        ContentHash = ContentHasher.Hash(j.Title, j.Location, j.Department ?? j.Team),
    };

    private static SalaryBand? MapSalary(Compensation? c)
    {
        var summary = c?.CompensationTierSummary ?? c?.ScrapeableCompensationSalarySummary;
        if (string.IsNullOrWhiteSpace(summary)) return null;
        return new SalaryBand(null, null, null, null, summary);
    }

    // --- Wire DTOs (only the fields we consume) ---

    private sealed record JobsResponse([property: JsonPropertyName("jobs")] List<Job>? Jobs);

    private sealed record Job(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("department")] string? Department,
        [property: JsonPropertyName("team")] string? Team,
        [property: JsonPropertyName("jobUrl")] string? JobUrl,
        [property: JsonPropertyName("publishedAt")] DateTimeOffset? PublishedAt,
        [property: JsonPropertyName("isListed")] bool IsListed,
        [property: JsonPropertyName("isRemote")] bool? IsRemote,
        [property: JsonPropertyName("workplaceType")] string? WorkplaceType,
        [property: JsonPropertyName("descriptionPlain")] string? DescriptionPlain,
        [property: JsonPropertyName("descriptionHtml")] string? DescriptionHtml,
        [property: JsonPropertyName("compensation")] Compensation? Compensation);

    private sealed record Compensation(
        [property: JsonPropertyName("compensationTierSummary")] string? CompensationTierSummary,
        [property: JsonPropertyName("scrapeableCompensationSalarySummary")] string? ScrapeableCompensationSalarySummary);
}
