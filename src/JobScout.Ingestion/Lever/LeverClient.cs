using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Abstractions;
using JobScout.Core.Model;
using JobScout.Ingestion.Http;

namespace JobScout.Ingestion.Lever;

/// <summary>
/// Client for the Lever public postings API:
/// <c>GET api.lever.co/v0/postings/{company}?mode=json</c>.
/// Lever returns a flat array and, when the employer entered it, a structured
/// <c>salaryRange</c> — the one direct pay signal among the three sources.
/// </summary>
public sealed class LeverClient(HttpClient http) : IAtsClient
{
    public JobSource Source => JobSource.Lever;

    public async Task<IReadOnlyList<JobPosting>> FetchPostingsAsync(string feed, CancellationToken ct)
    {
        var postings = await http.GetFromJsonAsync<List<Posting>>(
            $"{Uri.EscapeDataString(feed)}?mode=json", ct);

        return (postings ?? []).Select(p => Map(feed, p)).ToList();
    }

    private static JobPosting Map(string company, Posting p) => new()
    {
        Source = JobSource.Lever,
        Company = company,
        ExternalId = p.Id ?? "",
        Title = p.Text ?? "(untitled)",
        Location = p.Categories?.Location,
        Department = p.Categories?.Department ?? p.Categories?.Team,
        Remote = ResolveRemote(p),
        Description = HtmlText.ToPlainText(p.DescriptionPlain),
        Url = p.HostedUrl,
        PostedAt = p.CreatedAt is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null,
        Salary = MapSalary(p.SalaryRange),
        HasScreeningQuestions = null, // not exposed by the postings API
        ContentHash = ContentHasher.Hash(p.Text, p.Categories?.Location, p.Categories?.Department),
    };

    private static RemoteMode ResolveRemote(Posting p)
    {
        var fromType = RemoteDetector.FromWorkplaceType(p.WorkplaceType);
        return fromType != RemoteMode.Unknown ? fromType : RemoteDetector.FromText(p.Categories?.Location, p.Text);
    }

    private static SalaryBand? MapSalary(SalaryRange? r)
    {
        if (r is null) return null;
        var band = new SalaryBand(r.Min, r.Max, r.Currency, r.Interval);
        return band.IsDisclosed ? band : null;
    }

    // --- Wire DTOs (only the fields we consume) ---

    private sealed record Posting(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("hostedUrl")] string? HostedUrl,
        [property: JsonPropertyName("createdAt")] long? CreatedAt,
        [property: JsonPropertyName("descriptionPlain")] string? DescriptionPlain,
        [property: JsonPropertyName("workplaceType")] string? WorkplaceType,
        [property: JsonPropertyName("categories")] Categories? Categories,
        [property: JsonPropertyName("salaryRange")] SalaryRange? SalaryRange);

    private sealed record Categories(
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("department")] string? Department,
        [property: JsonPropertyName("team")] string? Team);

    private sealed record SalaryRange(
        [property: JsonPropertyName("min")] decimal? Min,
        [property: JsonPropertyName("max")] decimal? Max,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("interval")] string? Interval);
}
