using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Abstractions;
using JobScout.Core.Model;
using JobScout.Ingestion.Http;
using Microsoft.Extensions.Options;

namespace JobScout.Ingestion.Adzuna;

/// <summary>
/// Client for the Adzuna public job-search API:
/// <c>GET api.adzuna.com/v1/api/jobs/{country}/search/1?app_id=&amp;app_key=&amp;what={query}</c>.
/// An aggregator: the feed is a keyword query and results span many boards (including
/// Indeed-sourced listings) with each posting's real hiring company. Requires a free app id/key.
/// </summary>
public sealed class AdzunaClient(HttpClient http, IOptions<AdzunaOptions> options) : IAtsClient
{
    private readonly AdzunaOptions _options = options.Value;

    public JobSource Source => JobSource.Adzuna;

    public async Task<IReadOnlyList<JobPosting>> FetchPostingsAsync(string feed, CancellationToken ct)
    {
        var country = Uri.EscapeDataString(_options.Country);
        var query =
            $"{country}/search/1?app_id={Uri.EscapeDataString(_options.AppId!)}" +
            $"&app_key={Uri.EscapeDataString(_options.AppKey!)}" +
            $"&what={Uri.EscapeDataString(feed)}&results_per_page=50&content-type=application/json";

        using var response = await http.GetAsync(query, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonSafeAsync<SearchResponse>(ct);
        var results = payload?.Results ?? [];
        return results.Select(Map).ToList();
    }

    private JobPosting Map(Result r) => new()
    {
        Source = JobSource.Adzuna,
        Company = string.IsNullOrWhiteSpace(r.Company?.DisplayName) ? "(unknown)" : r.Company!.DisplayName!,
        ExternalId = r.Id ?? "",
        Title = HtmlText.ToPlainText(r.Title) ?? "(untitled)",
        Location = r.Location?.DisplayName,
        Department = r.Category?.Label,
        // Adzuna exposes no structured remote flag and uses city locations, so its only signal is
        // the text — including the description teaser, where these postings actually say "remote".
        Remote = RemoteDetector.FromText(r.Title, r.Location?.DisplayName, r.Description),
        Description = HtmlText.ToPlainText(r.Description),
        Url = r.RedirectUrl,
        PostedAt = r.Created,
        Salary = MapSalary(r),
        HasScreeningQuestions = null,
        ContentHash = ContentHasher.Hash(r.Title, r.Company?.DisplayName, r.Location?.DisplayName),
    };

    private SalaryBand? MapSalary(Result r)
    {
        if (r.SalaryMin is null && r.SalaryMax is null) return null;
        var band = new SalaryBand(r.SalaryMin, r.SalaryMax, CurrencyFor(_options.Country), "year");
        return band.IsDisclosed ? band : null;
    }

    private static string? CurrencyFor(string country) => country.ToLowerInvariant() switch
    {
        "us" => "USD",
        "gb" => "GBP",
        "ca" => "CAD",
        "au" => "AUD",
        "in" => "INR",
        _ => null,
    };

    // --- Wire DTOs (only the fields we consume) ---

    private sealed record SearchResponse([property: JsonPropertyName("results")] List<Result>? Results);

    private sealed record Result(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("redirect_url")] string? RedirectUrl,
        [property: JsonPropertyName("created")] DateTimeOffset? Created,
        [property: JsonPropertyName("salary_min")] decimal? SalaryMin,
        [property: JsonPropertyName("salary_max")] decimal? SalaryMax,
        [property: JsonPropertyName("company")] Company? Company,
        [property: JsonPropertyName("location")] Location? Location,
        [property: JsonPropertyName("category")] Category? Category);

    private sealed record Company([property: JsonPropertyName("display_name")] string? DisplayName);

    private sealed record Location([property: JsonPropertyName("display_name")] string? DisplayName);

    private sealed record Category([property: JsonPropertyName("label")] string? Label);
}
