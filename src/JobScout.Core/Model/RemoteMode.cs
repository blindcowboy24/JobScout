namespace JobScout.Core.Model;

/// <summary>
/// Where the work happens. Captured from each source's structured signal where one exists
/// (Ashby/Lever/Google Jobs), inferred from text otherwise (Greenhouse/Adzuna). <see cref="Unknown"/>
/// means the source didn't tell us — distinct from a known on-site.
/// </summary>
public enum RemoteMode
{
    Unknown,
    Onsite,
    Hybrid,
    Remote,
}
