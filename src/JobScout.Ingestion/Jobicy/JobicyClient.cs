using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Abstractions;
using JobScout.Core.Model;
using JobScout.Ingestion.Http;

namespace JobScout.Ingestion.Jobicy;

/// <summary>
/// Client for the Jobicy public remote-jobs API:
/// <c>GET jobicy.com/api/v2/remote-jobs?tag={tag}&amp;count=50</c>. Unlike a company ATS, the
/// feed is a <em>tag</em> (e.g. <c>.net</c>) and each posting carries its real hiring company.
/// No API key required.
/// </summary>
public sealed class JobicyClient(HttpClient http) : IAtsClient
{
    public JobSource Source => JobSource.Jobicy;

    public async Task<IReadOnlyList<JobPosting>> FetchPostingsAsync(string feed, CancellationToken ct)
    {
        var payload = await http.GetFromJsonAsync<JobsResponse>(
            $"remote-jobs?count=50&tag={Uri.EscapeDataString(feed)}", ct);

        var jobs = payload?.Jobs ?? [];
        return jobs.Select(Map).ToList();
    }

    private static JobPosting Map(Job j) => new()
    {
        Source = JobSource.Jobicy,
        Company = string.IsNullOrWhiteSpace(j.CompanyName) ? "(unknown)" : j.CompanyName,
        ExternalId = j.Id.ToString(CultureInfo.InvariantCulture),
        Title = j.JobTitle ?? "(untitled)",
        Location = j.JobGeo,
        Department = j.JobIndustry?.FirstOrDefault(),
        Remote = RemoteMode.Remote, // Jobicy is a remote-only board
        Description = HtmlText.ToPlainText(j.JobDescription ?? j.JobExcerpt),
        Url = j.Url,
        PostedAt = DateTimeOffset.TryParse(j.PubDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null,
        Salary = MapSalary(j),
        HasScreeningQuestions = null,
        ContentHash = ContentHasher.Hash(j.JobTitle, j.CompanyName, j.JobGeo),
    };

    private static SalaryBand? MapSalary(Job j)
    {
        if (j.AnnualSalaryMin is null && j.AnnualSalaryMax is null) return null;
        var band = new SalaryBand(j.AnnualSalaryMin, j.AnnualSalaryMax, j.SalaryCurrency, "year");
        return band.IsDisclosed ? band : null;
    }

    // --- Wire DTOs (only the fields we consume) ---

    private sealed record JobsResponse([property: JsonPropertyName("jobs")] List<Job>? Jobs);

    private sealed record Job(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("jobTitle")] string? JobTitle,
        [property: JsonPropertyName("companyName")] string? CompanyName,
        [property: JsonPropertyName("jobGeo")] string? JobGeo,
        [property: JsonPropertyName("jobIndustry")] List<string>? JobIndustry,
        [property: JsonPropertyName("pubDate")] string? PubDate,
        [property: JsonPropertyName("jobExcerpt")] string? JobExcerpt,
        [property: JsonPropertyName("jobDescription")] string? JobDescription,
        [property: JsonPropertyName("annualSalaryMin")] decimal? AnnualSalaryMin,
        [property: JsonPropertyName("annualSalaryMax")] decimal? AnnualSalaryMax,
        [property: JsonPropertyName("salaryCurrency")] string? SalaryCurrency);
}
