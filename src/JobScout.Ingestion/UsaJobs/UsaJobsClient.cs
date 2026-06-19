using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Abstractions;
using JobScout.Core.Model;
using JobScout.Ingestion.Http;
using Microsoft.Extensions.Options;

namespace JobScout.Ingestion.UsaJobs;

/// <summary>
/// Client for the USAJOBS API:
/// <c>GET data.usajobs.gov/api/search?Keyword={query}</c>. The US federal job board — the feed is
/// a keyword query and each posting's "company" is the hiring agency. Federal roles skew
/// .NET-heavy, which makes this a useful targeted source. Requires a free API key + the registered
/// email as User-Agent.
/// </summary>
public sealed class UsaJobsClient(HttpClient http, IOptions<UsaJobsOptions> options) : IAtsClient
{
    private readonly UsaJobsOptions _options = options.Value;

    public JobSource Source => JobSource.UsaJobs;

    public async Task<IReadOnlyList<JobPosting>> FetchPostingsAsync(string feed, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/search?Keyword={Uri.EscapeDataString(feed)}&ResultsPerPage=50");

        // USAJOBS auth: the key in a custom header and the registered email as User-Agent.
        request.Headers.TryAddWithoutValidation("Authorization-Key", _options.ApiKey);
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgentEmail);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>(ct);
        var items = payload?.SearchResult?.Items ?? [];
        return items.Select(i => Map(i.Descriptor)).Where(p => p is not null).Select(p => p!).ToList();
    }

    private static JobPosting? Map(Descriptor? d)
    {
        if (d is null) return null;
        return new JobPosting
        {
            Source = JobSource.UsaJobs,
            Company = string.IsNullOrWhiteSpace(d.OrganizationName) ? "(US federal agency)" : d.OrganizationName,
            ExternalId = d.PositionId ?? ContentHasher.Hash(d.PositionTitle, d.OrganizationName, d.LocationDisplay),
            Title = d.PositionTitle ?? "(untitled)",
            Location = d.LocationDisplay,
            Department = d.DepartmentName,
            Description = HtmlText.ToPlainText(d.UserArea?.Details?.JobSummary),
            Url = d.PositionUri,
            PostedAt = ParseDate(d.PublicationStartDate),
            ApplicationDeadline = ParseDateOnly(d.ApplicationCloseDate),
            Salary = MapSalary(d.Remuneration?.FirstOrDefault()),
            HasScreeningQuestions = null,
            ContentHash = ContentHasher.Hash(d.PositionTitle, d.OrganizationName, d.LocationDisplay),
        };
    }

    private static SalaryBand? MapSalary(Remuneration? r)
    {
        if (r is null) return null;
        var min = ParseDecimal(r.MinimumRange);
        var max = ParseDecimal(r.MaximumRange);
        if (min is null && max is null) return null;
        return new SalaryBand(min, max, "USD", r.RateIntervalCode);
    }

    private static decimal? ParseDecimal(string? s) =>
        decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static DateTimeOffset? ParseDate(string? s) =>
        DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var v) ? v : null;

    private static DateOnly? ParseDateOnly(string? s) =>
        DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) ? v : null;

    // --- Wire DTOs (only the fields we consume) ---

    private sealed record SearchResponse([property: JsonPropertyName("SearchResult")] SearchResult? SearchResult);

    private sealed record SearchResult([property: JsonPropertyName("SearchResultItems")] List<Item>? Items);

    private sealed record Item([property: JsonPropertyName("MatchedObjectDescriptor")] Descriptor? Descriptor);

    private sealed record Descriptor(
        [property: JsonPropertyName("PositionID")] string? PositionId,
        [property: JsonPropertyName("PositionTitle")] string? PositionTitle,
        [property: JsonPropertyName("PositionURI")] string? PositionUri,
        [property: JsonPropertyName("OrganizationName")] string? OrganizationName,
        [property: JsonPropertyName("DepartmentName")] string? DepartmentName,
        [property: JsonPropertyName("PositionLocationDisplay")] string? LocationDisplay,
        [property: JsonPropertyName("PublicationStartDate")] string? PublicationStartDate,
        [property: JsonPropertyName("ApplicationCloseDate")] string? ApplicationCloseDate,
        [property: JsonPropertyName("PositionRemuneration")] List<Remuneration>? Remuneration,
        [property: JsonPropertyName("UserArea")] UserArea? UserArea);

    private sealed record Remuneration(
        [property: JsonPropertyName("MinimumRange")] string? MinimumRange,
        [property: JsonPropertyName("MaximumRange")] string? MaximumRange,
        [property: JsonPropertyName("RateIntervalCode")] string? RateIntervalCode);

    private sealed record UserArea([property: JsonPropertyName("Details")] Details? Details);

    private sealed record Details([property: JsonPropertyName("JobSummary")] string? JobSummary);
}
