using System.Collections.Immutable;
using System.Globalization;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>
/// Revalidates persisted declarations and resolves them with authoritative page
/// request context before renderer dispatch.
/// </summary>
public sealed class PageContentQueryResolver(
    IContentHierarchyQueryService hierarchyQueryService) : IPageContentQueryResolver
{
    /// <inheritdoc />
    public async Task<Result<PageContentQueryResolution>> ResolveAsync(
        long siteId,
        string culture,
        IReadOnlyList<ContentQueryDefinition>? definitions,
        bool includeDrafts,
        CancellationToken cancellationToken = default)
    {
        var declarations = definitions ?? [];
        if (declarations.Count == 0)
        {
            return PageContentQueryResolution.Empty;
        }

        var validationError = Validate(siteId, culture, declarations);
        if (validationError is not null)
        {
            return validationError;
        }

        var normalizedCulture = CultureInfo.GetCultureInfo(culture).Name;
        var results = ImmutableDictionary.CreateBuilder<string, ContentQueryResult>(
            StringComparer.OrdinalIgnoreCase);
        var aliases = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in declarations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = declaration.CreateSnapshot();
            var result = await hierarchyQueryService.QueryAsync(
                new ContentQueryRequest(
                    snapshot.Name,
                    siteId,
                    snapshot.ContentTypeId,
                    snapshot.ContentTypeAlias,
                    normalizedCulture,
                    snapshot.Traversal,
                    snapshot.RootId,
                    snapshot.MaximumDepth,
                    snapshot.MaximumItems,
                    snapshot.Projection,
                    includeDrafts),
                cancellationToken);
            if (result is Result<ContentQueryResult>.Failure failure)
            {
                return failure.Error;
            }

            var value = ((Result<ContentQueryResult>.Ok)result).Value;
            results.Add(snapshot.Name, value);
            aliases.Add(value.ContentTypeAlias);
            foreach (var nodeAlias in EnumerateAliases(value.Roots))
            {
                aliases.Add(nodeAlias);
            }
        }

        return new PageContentQueryResolution
        {
            Results = results.ToImmutable(),
            ContentTypeAliases = aliases
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray()
        };
    }

    private static AeroError? Validate(
        long siteId,
        string culture,
        IReadOnlyList<ContentQueryDefinition> definitions)
    {
        var errors = new List<string>();
        if (siteId <= 0)
        {
            errors.Add("Content queries require an authoritative current site.");
        }

        try
        {
            _ = CultureInfo.GetCultureInfo(culture);
        }
        catch (CultureNotFoundException)
        {
            errors.Add("Content queries require a valid current culture.");
        }

        errors.AddRange(ContentQueryDefinition.ValidateDefinitions(definitions));

        return errors.Count == 0
            ? null
            : AeroError.ValidationError(errors.Distinct(StringComparer.Ordinal));
    }

    private static IEnumerable<string> EnumerateAliases(IEnumerable<ContentNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node.ContentType;
            foreach (var alias in EnumerateAliases(node.Children))
            {
                yield return alias;
            }
        }
    }
}
