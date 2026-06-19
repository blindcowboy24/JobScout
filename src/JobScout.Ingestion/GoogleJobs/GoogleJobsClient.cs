using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Abstractions;
using JobScout.Core.Model;
using JobScout.Ingestion.Http;
using Microsoft.Extensions.Options;

namespace JobScout.Ingestion.GoogleJobs;

/// <summary>
/// Client for Google Jobs via the SerpApi search API:
/// <c>GET serpapi.com/search?engine=google_jobs&amp;q={query}&amp;api_key=…</c>. Google Jobs
/// aggregates many boards (Indeed, LinkedIn, Glassdoor, ZipRecruiter, …), so one feed — a keyword
/// query — spans them all, each posting carrying its real hiring company. Requires a SerpApi key.
/// </summary>
public sealed class GoogleJobsClient(HttpClient http, IOptions<SerpApiOptions> options) : IAtsClient
{
    private readonly SerpApiOptions _options = options.Value;

    public JobSource Source => JobSource.GoogleJobs;

    public async Task<IReadOnlyList<JobPosting>> FetchPostingsAsync(string feed, CancellationToken ct)
    {
        var url =
            $"search?engine=google_jobs&q={Uri.EscapeDataString(feed)}" +
            $"&api_key={Uri.EscapeDataString(_options.ApiKey!)}";
        if (!string.IsNullOrWhiteSpace(_options.Location))
            url += $"&location={Uri.EscapeDataString(_options.Location)}";

        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonSafeAsync<SearchResponse>(ct);
        var results = payload?.JobsResults ?? [];
        return results.Select(Map).ToList();
    }

    private static JobPosting Map(Result r) => new()
    {
        Source = JobSource.GoogleJobs,
        Company = string.IsNullOrWhiteSpace(r.CompanyName) ? "(unknown)" : r.CompanyName,
        // Google's job_id is a very long opaque token; hash it to a stable, bounded key.
        ExternalId = !string.IsNullOrWhiteSpace(r.JobId)
            ? ContentHasher.Hash(r.JobId)
            : ContentHasher.Hash(r.Title, r.CompanyName, r.Location),
        Title = r.Title ?? "(untitled)",
        Location = r.Location,
        Department = r.Via, // e.g. "via Indeed" — which underlying board surfaced it
        Remote = r.DetectedExtensions?.WorkFromHome == true ? RemoteMode.Remote : RemoteDetector.FromText(r.Title, r.Location),
        Description = HtmlText.ToPlainText(r.Description),
        Url = r.ApplyOptions?.FirstOrDefault()?.Link ?? r.ShareLink,
        Salary = MapSalary(r.DetectedExtensions?.Salary),
        HasScreeningQuestions = null,
        ContentHash = ContentHasher.Hash(r.Title, r.CompanyName, r.Location),
    };

    private static SalaryBand? MapSalary(string? summary) =>
        string.IsNullOrWhiteSpace(summary) ? null : new SalaryBand(null, null, null, null, summary);

    // --- Wire DTOs (only the fields we consume) ---

    private sealed record SearchResponse([property: JsonPropertyName("jobs_results")] List<Result>? JobsResults);

    private sealed record Result(
        [property: JsonPropertyName("job_id")] string? JobId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("company_name")] string? CompanyName,
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("via")] string? Via,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("share_link")] string? ShareLink,
        [property: JsonPropertyName("detected_extensions")] DetectedExtensions? DetectedExtensions,
        [property: JsonPropertyName("apply_options")] List<ApplyOption>? ApplyOptions);

    private sealed record DetectedExtensions(
        [property: JsonPropertyName("salary")] string? Salary,
        [property: JsonPropertyName("work_from_home")] bool? WorkFromHome);

    private sealed record ApplyOption([property: JsonPropertyName("link")] string? Link);
}
