# JobScout

A ghost-job-aware job-intelligence crawler. JobScout ingests postings from public ATS APIs,
tracks each posting over time, and scores **posting intent** — surfacing fresh, real, fillable
roles and de-prioritizing the evergreen "ghost" reqs that never actually hire.

> Most job boards drown you in postings without telling you which are *real*. The strongest
> tell for a ghost job is **time**: a req that sits open for months, vanishes and reappears, or
> gets blasted across a dozen boards behaves differently from one a team is genuinely trying to
> fill. JobScout crawls repeatedly, keeps per-posting history, and turns those time-based
> signals into a transparent ranking score.

Built on **.NET 10** as a portfolio / proof-of-work project — so the reasoning is legible and
the data layer is clean, not a black box.

## Why ATS APIs (and not scraping)

JobScout only consumes **public, consumption-intended JSON APIs**. Each source is a client
implementing `IAtsClient`, and the crawl unit is a *feed* — a company board slug for an ATS, or a
search query/tag for an aggregator:

| Source     | Type        | Feed is…     | Endpoint | Key |
|------------|-------------|--------------|----------|-----|
| Greenhouse | Company ATS | board slug   | `boards-api.greenhouse.io/v1/boards/{slug}/jobs?content=true` | — |
| Lever      | Company ATS | board slug   | `api.lever.co/v0/postings/{slug}?mode=json` | — |
| Ashby      | Company ATS | board slug   | `api.ashbyhq.com/posting-api/job-board/{slug}` | — |
| Jobicy     | Aggregator  | tag          | `jobicy.com/api/v2/remote-jobs?tag={tag}` | — |
| Adzuna     | Aggregator  | keyword      | `api.adzuna.com/v1/api/jobs/{country}/search/1?what={query}` | free app id/key |
| GoogleJobs | Aggregator  | keyword      | `serpapi.com/search?engine=google_jobs&q={query}` (via SerpApi) | SerpApi key |
| TheirStack | Aggregator  | keyword      | `api.theirstack.com/v1/jobs/search` (POST) | API key |
| UsaJobs    | Gov board   | keyword      | `data.usajobs.gov/api/search?Keyword={query}` | free key + email |

Company ATS feeds are where a single company's real reqs live; aggregator feeds let you search
*across* companies by keyword — which is how you find ".NET" roles without knowing who's hiring.
Jobicy is free (no key). Adzuna, **GoogleJobs** (via SerpApi — Google Jobs aggregates Indeed /
LinkedIn / Glassdoor / etc.), and **TheirStack** (315K+ sources incl. ATS) are commercial sources
that surface Indeed-sourced and broader coverage *legitimately* — you consume their structured
API rather than scraping. They're **opt-in and key-gated** (default off), so the clean
direct-source story stays the headline and the paid breadth is something you switch on.

It's a polite client throughout: descriptive User-Agent, request timeouts, and exponential
back-off that honors `Retry-After`.

**Why no Indeed or Dice scraper?** Both are deliberately out of scope. Indeed retired its public
job-search API and now blocks scraping (TOS + Cloudflare); Dice never offered one. Scraping either
would be brittle, violate their terms, and contradict the whole "polite client of
consumption-intended APIs" premise. **Adzuna is the lawful way to get Indeed-sourced coverage.**

## How the intent score works

The score is a **ranking aid, not an oracle** — so it shows its work. It's a transparent,
additive model starting from a neutral baseline; each signal contributes a named, explainable
factor (see [`IntentScorer`](src/JobScout.Core/Scoring/IntentScorer.cs)):

| Signal | Effect | Rationale |
|--------|--------|-----------|
| **Posting age** (still open long after a grace period) | − (ramps, capped) | The classic ghost tell — a req that never fills |
| **Feed freshness** (vanished from the feed) | − | Likely filled/closed → not fillable now |
| **Repost churn** (vanished then reappeared) | − | Evergreen reqs / pipeline-building / resellers |
| **Cross-posting** (same title on many boards at once) | − | Reads as staffing churn, not one real seat |
| **Salary band disclosed** | + | A budgeted, legally-committed role |
| **Custom screening questions** | + | Real screening effort |
| **Live application deadline** | + | An actual hiring timeline |
| **Past deadline, still listed** | − | A strong ghost signal |

The time-based signals (age, freshness, repost churn) are the strongest — and they only become
meaningful after **repeated crawls** build up per-posting history. A single crawl mostly returns
baseline scores; the value compounds over days of tracking. Signals a source doesn't expose are
treated as genuinely *unknown* (neutral), never guessed.

## Architecture

A clean, dependency-directed solution (`JobScout.slnx`):

```
JobScout.Core       Domain models, scoring, interfaces — pure, no infrastructure deps
JobScout.Ingestion  Greenhouse / Lever / Ashby typed HttpClients → canonical JobPosting
JobScout.Data       EF Core DbContext, entities, per-posting history, scoring repository
JobScout.Worker     BackgroundService host: schedules crawls, persists, ranks (headless)
JobScout.Web        Blazor dashboard to browse the ranked store — also runs the crawler
```

- **`Core`** stays free of infrastructure so the models and scoring are pure and unit-testable.
  Everything downstream speaks the canonical [`JobPosting`](src/JobScout.Core/Model/JobPosting.cs).
- **`Data`** owns each posting's timeline ([`TrackedPosting`](src/JobScout.Data/Entities/TrackedPosting.cs))
  plus an append-only [`PostingSnapshot`](src/JobScout.Data/Entities/PostingSnapshot.cs) history,
  and recomputes scores from that history on every crawl.
- The crawl loop ([`CrawlService`](src/JobScout.Worker/CrawlService.cs)) reconciles each board:
  upsert what's present, advance first/last-seen and repost counts, mark vanished postings
  closed, rescore everything touched. A failure on one board is logged and skipped, never
  aborting the cycle.
- Reads and writes are split CQRS-style: the crawl path uses
  [`IJobRepository`](src/JobScout.Core/Abstractions/IJobRepository.cs); the UI depends only on a
  separate read contract [`IPostingReadStore`](src/JobScout.Core/Abstractions/PostingQueries.cs).

## UI

A **Blazor** dashboard ([`JobScout.Web`](src/JobScout.Web)) for browsing the ranked store —
interactive server rendering, all C#. It hosts the crawler in the same process, so one app keeps
the data fresh and serves the views.

- **Dashboard** — headline stats (active / tracked, counts by intent band and by source, last
  crawl) over a ranked, clickable table with broad relevance search, **a filter on every column**
  (score ≥, title/company/location contains, source, age ≤, reposts ≤, salary disclosed), a
  **Remote-only** toggle with per-row Remote/Hybrid badges, plus sort (highest/lowest intent,
  newest, recently seen, title) and a one-click Clear. Filters combine with AND.
  - **Remote** is taken from each source's structured signal where it exists (Ashby `isRemote`,
    Lever/Ashby `workplaceType`, Google Jobs `work_from_home`, Jobicy = remote-only board) and
    inferred from title/location/snippet text where it doesn't (Greenhouse, Adzuna). Treat the
    text-inferred ones as a strong hint to confirm on the listing, not gospel.
  - **Search reads the job description, not just the title** — the tech stack ("C#", ".NET",
    "Azure") almost never appears in a job title, so a title-only search would miss real roles.
    Each posting's description is ingested as bounded plain text and indexed. The search box is a
    small query grammar: `/` or `,` = alternatives (`​.net / c#`), spaces = all-required
    (`senior azure`), matched across title, company, location, and description.
- **Detail** — the payoff for the "show its work" thesis: a posting's full **score breakdown**,
  factor by factor with signed contributions, recomputed live from stored history via the same
  scoring logic the crawl uses — plus the posting's timeline and a direct link to apply.

```bash
dotnet run --project src/JobScout.Web   # → http://localhost:5179
```

## Data store

- **SQLite by default** — zero-setup local dev; the database file is created on first run.
- **SQL Server** provider is included; swap via configuration with the data layer staying
  provider-agnostic.

```jsonc
// appsettings.json
"Database":    { "Provider": "Sqlite" },              // or "SqlServer"
"ConnectionStrings": { "JobScout": "Data Source=jobscout.db" }
```

The schema is created at startup via `EnsureCreated`, which keeps local dev zero-setup and works
identically across both providers. (EF migrations can be added for a managed production rollout;
see _Build / run_ below.)

## Configuration

Feeds to crawl, cadence, and read-out size live under the `Crawl` section:

```jsonc
"Crawl": {
  "IntervalMinutes": 360,        // first cycle runs immediately on startup
  "TopN": 10,                    // ranked roles logged after each cycle
  "Boards": [
    { "Source": "Greenhouse", "Feed": "gitlab" },        // ATS: Feed = board slug
    { "Source": "Lever",      "Feed": "spotify" },
    { "Source": "Ashby",      "Feed": "Linear" },
    { "Source": "Jobicy",     "Feed": ".net" },          // Aggregator: Feed = tag
    { "Source": "Adzuna",     "Feed": ".net developer" } // Aggregator: Feed = keyword query
  ]
}
```

`Feed` is the crawl unit: a board slug for a company ATS, or a search query/tag for an aggregator.
**To target your own hunt, add the companies and queries you care about here** — that's where the
real value is. Non-secret settings (country, location, page size) live in `appsettings.json`;
**API keys never do.**

The commercial aggregators (**Adzuna**, **GoogleJobs**/SerpApi, **TheirStack**) each need a key.
Without one, that source simply isn't registered and its feeds are skipped with a log line — the
rest of the crawl runs normally. Keys are kept out of the repo with **.NET user-secrets**, so set
them per machine:

```powershell
dotnet user-secrets set "Adzuna:AppId"  "your-id"  --project src/JobScout.Web
dotnet user-secrets set "Adzuna:AppKey" "your-key" --project src/JobScout.Web
dotnet user-secrets set "SerpApi:ApiKey"    "your-key" --project src/JobScout.Web
dotnet user-secrets set "TheirStack:ApiKey" "your-key" --project src/JobScout.Web
```

User-secrets are merged over `appsettings.json` at startup (Development), so the options bind the
same way — the values just live in your user profile, never in git. Restart after setting a key
and that source comes online.

## Build / run

```bash
dotnet build
dotnet run --project src/JobScout.Worker
```

On startup it creates the database, runs a crawl immediately, then repeats every
`IntervalMinutes`. Each cycle logs a per-board summary and the top-N highest-intent roles:

```
JobScout starting: 3 board(s), crawling every 06:00:00.
--- Crawl cycle @ 2026-06-12 00:08:18Z ---
Greenhouse/gitlab: 141 live (141 new, 0 reappeared, 0 closed).
Lever/spotify: 145 live (145 new, 0 reappeared, 0 closed).
Ashby/Linear: 25 live (25 new, 0 reappeared, 0 closed).
Top 10 high-intent roles:
  #1  [ 55.0 Medium] AI Engineer — gitlab (Greenhouse) · Remote, US
  ...
```

### EF migrations (optional)

`EnsureCreated` covers local dev. For a versioned schema:

```bash
dotnet ef migrations add <Name> --project src/JobScout.Data --startup-project src/JobScout.Worker
```

## Branches

- **`main`** — stable; protected (changes land via pull request).
- **`dev`** — active development; branch features off here and PR into `main` for stable points.

## Roadmap

- ~~A small UI over the ranked store~~ ✓ Blazor dashboard with per-posting score breakdown.
- A JSON read API alongside the UI (the [`IPostingReadStore`](src/JobScout.Core/Abstractions/PostingQueries.cs) read model is already there).
- Smarter cross-post matching (fuzzy title + company normalization).
- Salary parsing from Ashby compensation tiers and embedded text.
- Unit tests over the scorer's factor logic (pure and dependency-free by design).

---

*JobScout consumes only public, consumption-intended ATS APIs and is a courteous client by
design. It is not affiliated with Greenhouse, Lever, or Ashby.*
