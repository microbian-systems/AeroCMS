using System.Collections.Immutable;
using Aero.Cms.Abstractions.Content;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Pages.Composition;

/// <summary>Contains eagerly resolved named content data and dependency aliases.</summary>
public sealed record PageContentQueryResolution
{
    /// <summary>Gets a resolution with no query results or dependencies.</summary>
    public static PageContentQueryResolution Empty { get; } = new();

    /// <summary>Gets immutable results keyed by normalized binding name.</summary>
    public ImmutableDictionary<string, ContentQueryResult> Results { get; init; } =
        ImmutableDictionary.Create<string, ContentQueryResult>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets all declared content-type aliases, including empty-result queries.</summary>
    public ImmutableArray<string> ContentTypeAliases { get; init; } = [];
}

/// <summary>
/// Resolves persisted declarations using authoritative request context before
/// any page or fragment renderer executes.
/// </summary>
public interface IPageContentQueryResolver
{
    /// <summary>Resolves one page's bounded declarations sequentially.</summary>
    Task<Result<PageContentQueryResolution>> ResolveAsync(
        long siteId,
        string culture,
        IReadOnlyList<ContentQueryDefinition>? definitions,
        bool includeDrafts,
        CancellationToken cancellationToken = default);
}
