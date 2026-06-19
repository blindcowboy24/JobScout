using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Abstractions;
using JobScout.Core.Model;
using JobScout.Ingestion.Http;
using Microsoft.Extensions.Options;

namespace JobScout.Ingestion.TheirStack;

/// <summary>
/// Client for the TheirStack job-search API:
/// <c>POST api.theirstack.com/v1/jobs/search</c> with a Bearer token. An aggregator over 315K+
/// sources (including ATS platforms); the feed is a job-title keyword and each posting carries its
/// real hiring company. Requires an API key.
/// </summary>
public sealed class TheirStackClient(HttpClient http, IOptions<TheirStackOptions> options) : IAtsClient
{
    private readonly TheirStackOptions _options = options.Value;

    public JobSource Source => JobSource.TheirStack;

    public async Task<IReadOnlyList<JobPosting>> FetchPostingsAsync(string feed, CancellationToken ct)
    {
        var body = new SearchRequest(
            Page: 0,
            Limit: _options.Limit,
            PostedAtMaxAgeDays: _options.MaxAgeDays,
            JobTitleOr: [feed]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/jobs/search")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonSafeAsync<SearchResponse>(ct);
        var data = payload?.Data ?? [];
        return data.Select(Map).ToList();
    }

    private static JobPosting Map(Job j) => new()
    {
        Source = JobSource.TheirStack,
        Company = j.Company ?? j.CompanyObject?.Name ?? "(unknown)",
        ExternalId = j.Id.ToString(CultureInfo.InvariantCulture),
        Title = j.JobTitle ?? "(untitled)",
        Location = j.Location,
        Remote = j.Remote == true ? RemoteMode.Remote : RemoteDetector.FromText(j.JobTitle, j.Location),
        Description = HtmlText.ToPlainText(j.Description),
        Url = j.FinalUrl ?? j.Url,
        PostedAt = DateTimeOffset.TryParse(j.DatePosted, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null,
        Salary = string.IsNullOrWhiteSpace(j.SalaryString) ? null : new SalaryBand(null, null, null, null, j.SalaryString),
        HasScreeningQuestions = null,
        ContentHash = ContentHasher.Hash(j.JobTitle, j.Company ?? j.CompanyObject?.Name, j.Location),
    };

    // --- Wire DTOs (only the fields we consume) ---

    private sealed record SearchRequest(
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("limit")] int Limit,
        [property: JsonPropertyName("posted_at_max_age_days")] int PostedAtMaxAgeDays,
        [property: JsonPropertyName("job_title_or")] string[] JobTitleOr);

    private sealed record SearchResponse([property: JsonPropertyName("data")] List<Job>? Data);

    private sealed record Job(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("job_title")] string? JobTitle,
        [property: JsonPropertyName("company")] string? Company,
        [property: JsonPropertyName("company_object")] CompanyObject? CompanyObject,
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("remote")] bool? Remote,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("final_url")] string? FinalUrl,
        [property: JsonPropertyName("salary_string")] string? SalaryString,
        [property: JsonPropertyName("date_posted")] string? DatePosted,
        [property: JsonPropertyName("description")] string? Description);

    private sealed record CompanyObject([property: JsonPropertyName("name")] string? Name);
}
