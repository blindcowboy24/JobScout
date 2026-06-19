using System.Net.Http.Json;
using System.Text.Json;

namespace JobScout.Ingestion.Http;

/// <summary>
/// Reads JSON from a response by streaming the body and deserializing directly, ignoring the
/// response's declared charset. Some APIs (e.g. Adzuna) label their content <c>charset=utf8</c>,
/// which .NET's <see cref="HttpContentJsonExtensions"/> rejects because the canonical name is
/// <c>utf-8</c>. Streaming the raw bytes sidesteps that without giving up correctness.
/// </summary>
internal static class JsonContentExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task<T?> ReadFromJsonSafeAsync<T>(this HttpContent content, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options, ct);
    }
}
