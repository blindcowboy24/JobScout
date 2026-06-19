using JobScout.Core.Model;

namespace JobScout.Worker;

/// <summary>Bound from the <c>Crawl</c> configuration section.</summary>
public sealed class CrawlOptions
{
    public const string SectionName = "Crawl";

    /// <summary>Minutes between full crawl cycles. The first cycle runs immediately on startup.</summary>
    public int IntervalMinutes { get; set; } = 360;

    /// <summary>How many top-ranked postings to log at the end of each cycle.</summary>
    public int TopN { get; set; } = 10;

    /// <summary>The feeds to crawl, each a (source, feed) pair.</summary>
    public List<BoardOption> Boards { get; set; } = [];
}

public sealed class BoardOption
{
    public JobSource Source { get; set; }

    /// <summary>
    /// The crawl unit. For a company ATS it's the board slug in the URL (e.g. <c>gitlab</c>);
    /// for an aggregator it's a search query or tag (e.g. <c>.net</c>).
    /// </summary>
    public string Feed { get; set; } = "";
}
