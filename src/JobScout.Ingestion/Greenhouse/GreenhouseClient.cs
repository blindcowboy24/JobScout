using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Abstractions;
using JobScout.Core.Model;
using JobScout.Ingestion.Http;

namespace JobScout.Ingestion.Greenhouse;

/// <summary>
/// Client for the Greenhouse public board API:
/// <c>GET boards-api.greenhouse.io/v1/boards/{company}/jobs?content=true</c>.
/// Greenhouse does not expose pay or screening questions on the list endpoint, so those
/// signals are left unknown (null) rather than guessed.
/// </summary>
public sealed class GreenhouseClient(HttpClient http) : IAtsClient
{
    public JobSource Source => JobSource.Greenhouse;

    public async Task<IReadOnlyList<JobPosting>> FetchPostingsAsync(string feed, CancellationToken ct)
    {
        var payload = await http.GetFromJsonAsync<JobsResponse>(
            $"{Uri.EscapeDataString(feed)}/jobs?content=true", ct);

        var jobs = payload?.Jobs ?? [];
        return jobs.Select(j => Map(feed, j)).ToList();
    }

    private static JobPosting Map(string company, Job j) => new()
    {
        Source = JobSource.Greenhouse,
        Company = company,
        ExternalId = j.Id.ToString(CultureInfo.InvariantCulture),
        Title = j.Title ?? "(untitled)",
        Location = j.Location?.Name,
        Department = j.Departments?.FirstOrDefault()?.Name,
        Remote = RemoteDetector.FromText(j.Location?.Name, j.Title),
        Description = HtmlText.ToPlainText(j.Content),
        Url = j.AbsoluteUrl,
        PostedAt = ParseOffset(j.FirstPublished),
        UpdatedAt = ParseOffset(j.UpdatedAt),
        ApplicationDeadline = ParseDate(j.ApplicationDeadline),
        Salary = null,                 // not exposed by the board API
        HasScreeningQuestions = null,  // not exposed by the board API
        ContentHash = ContentHasher.Hash(j.Title, j.Location?.Name, j.Departments?.FirstOrDefault()?.Name, j.RequisitionId),
    };

    private static DateTimeOffset? ParseOffset(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto) ? dto : null;

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    // --- Wire DTOs (only the fields we consume) ---

    private sealed record JobsResponse([property: JsonPropertyName("jobs")] List<Job>? Jobs);

    private sealed record Job(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("absolute_url")] string? AbsoluteUrl,
        [property: JsonPropertyName("updated_at")] string? UpdatedAt,
        [property: JsonPropertyName("first_published")] string? FirstPublished,
        [property: JsonPropertyName("requisition_id")] string? RequisitionId,
        [property: JsonPropertyName("application_deadline")] string? ApplicationDeadline,
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("location")] Location? Location,
        [property: JsonPropertyName("departments")] List<Department>? Departments);

    private sealed record Location([property: JsonPropertyName("name")] string? Name);

    private sealed record Department([property: JsonPropertyName("name")] string? Name);
}
