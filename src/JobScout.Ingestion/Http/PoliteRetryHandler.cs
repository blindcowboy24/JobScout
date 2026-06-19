using System.Net;
using Microsoft.Extensions.Logging;

namespace JobScout.Ingestion.Http;

/// <summary>
/// A small, dependency-free retry handler so we stay a polite client of public ATS APIs:
/// it backs off on rate-limit (429) and transient 5xx responses, honouring a server-supplied
/// <c>Retry-After</c> when present, and gives up after a bounded number of attempts.
/// </summary>
internal sealed class PoliteRetryHandler(ILogger<PoliteRetryHandler> logger) : DelegatingHandler
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var response = await base.SendAsync(request, ct);
            if (attempt >= MaxAttempts || !IsTransient(response.StatusCode))
                return response;

            var delay = RetryAfter(response) ?? TimeSpan.FromSeconds(BaseDelay.TotalSeconds * Math.Pow(2, attempt - 1));
            logger.LogWarning(
                "ATS request to {Url} returned {Status}; backing off {Delay:n1}s (attempt {Attempt}/{Max}).",
                request.RequestUri, (int)response.StatusCode, delay.TotalSeconds, attempt, MaxAttempts);

            response.Dispose();
            await Task.Delay(delay, ct);
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests || (int)status >= 500;

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null) return null;
        if (retryAfter.Delta is { } delta) return delta;
        if (retryAfter.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }
        return null;
    }
}
