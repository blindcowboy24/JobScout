namespace JobScout.Core.Model;

/// <summary>
/// Where a posting was ingested from. Ordered loosely by trust: a direct ATS feed
/// is a stronger intent signal than an aggregator or a staffing reseller, because the
/// hiring company controls it directly.
/// </summary>
public enum JobSource
{
    /// <summary>Greenhouse public board API (boards-api.greenhouse.io).</summary>
    Greenhouse,

    /// <summary>Lever public postings API (api.lever.co).</summary>
    Lever,

    /// <summary>Ashby public job-board posting API (api.ashbyhq.com).</summary>
    Ashby,

    /// <summary>Jobicy public remote-jobs API (jobicy.com) — an aggregator, queried by tag. No key.</summary>
    Jobicy,

    /// <summary>Adzuna public job-search API (api.adzuna.com) — an aggregator, queried by keyword. Needs a free app id/key.</summary>
    Adzuna,

    /// <summary>Google Jobs via SerpApi (serpapi.com) — aggregates Indeed/LinkedIn/Glassdoor/etc. Needs a SerpApi key.</summary>
    GoogleJobs,

    /// <summary>TheirStack job-search API (api.theirstack.com) — 315K+ sources incl. ATS. Needs an API key.</summary>
    TheirStack,

    /// <summary>USAJOBS API (data.usajobs.gov) — US federal job board, queried by keyword. Needs a free API key.</summary>
    UsaJobs,
}
