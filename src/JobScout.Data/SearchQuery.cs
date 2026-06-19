using System.Linq.Expressions;
using System.Reflection;
using JobScout.Data.Entities;

namespace JobScout.Data;

/// <summary>
/// Turns a free-text search box into an EF-translatable predicate over a posting's title,
/// company, location, and description. A small, deliberate query grammar:
/// <list type="bullet">
///   <item><c>/</c> and <c>,</c> separate <b>alternatives</b> (OR) — e.g. <c>.net / c#</c> matches either.</item>
///   <item>whitespace separates <b>required</b> terms within an alternative (AND) — e.g. <c>senior .net</c>.</item>
/// </list>
/// Each term matches as a case-insensitive substring of any searched field — and because the
/// description is searched, a query like <c>.net</c> finds roles whose <em>body</em> mentions the
/// stack even though the title never does.
/// </summary>
internal static class SearchQuery
{
    private static readonly MethodInfo ToLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly MethodInfo Contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    private static readonly string[] OrSeparators = ["/", ","];

    /// <summary>
    /// Parses the raw box into OR-groups of AND-terms (lowercased). Shared by the SQL predicate
    /// and the in-memory relevance check so they always agree on what "matches" means.
    /// </summary>
    public static IReadOnlyList<string[]> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        return raw.Split(OrSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(group => group.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLowerInvariant())
                .ToArray())
            .Where(terms => terms.Length > 0)
            .ToArray();
    }

    /// <summary>True if <paramref name="text"/> satisfies the parsed query (any group, all its terms).</summary>
    public static bool Matches(string? text, IReadOnlyList<string[]> groups)
    {
        if (groups.Count == 0) return true;
        var h = (text ?? "").ToLowerInvariant();
        return groups.Any(terms => terms.All(h.Contains));
    }

    /// <summary>Builds the predicate, or null when the query is empty (meaning "no filter").</summary>
    public static Expression<Func<TrackedPosting, bool>>? Build(string? raw)
    {
        var groups = Parse(raw);
        if (groups.Count == 0) return null;

        var p = Expression.Parameter(typeof(TrackedPosting), "p");

        // OR over alternative groups; within each, AND over required terms.
        Expression? any = null;
        foreach (var terms in groups)
        {
            Expression? all = null;
            foreach (var term in terms)
            {
                var matchesTerm = AnyFieldContains(p, term);
                all = all is null ? matchesTerm : Expression.AndAlso(all, matchesTerm);
            }

            any = any is null ? all! : Expression.OrElse(any, all!);
        }

        return Expression.Lambda<Func<TrackedPosting, bool>>(any!, p);
    }

    // (Title|Company|Location|Description) — any non-null field whose lowercased value contains the term.
    private static Expression AnyFieldContains(ParameterExpression p, string term)
    {
        var literal = Expression.Constant(term);

        Expression Field(string name, bool nullable)
        {
            Expression access = Expression.Property(p, name);
            var contains = Expression.Call(Expression.Call(access, ToLower), Contains, literal);
            return nullable
                ? Expression.AndAlso(Expression.NotEqual(access, Expression.Constant(null, typeof(string))), contains)
                : contains;
        }

        return Expression.OrElse(
            Expression.OrElse(Field(nameof(TrackedPosting.Title), nullable: false),
                              Field(nameof(TrackedPosting.Company), nullable: false)),
            Expression.OrElse(Field(nameof(TrackedPosting.Location), nullable: true),
                              Field(nameof(TrackedPosting.Description), nullable: true)));
    }
}
