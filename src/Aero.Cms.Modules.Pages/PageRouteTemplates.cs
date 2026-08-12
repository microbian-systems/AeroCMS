using System.Globalization;
using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Entities;
using Aero.Cms.Shared.Localization;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Pages;

/// <summary>A published page-template match and its single bounded route value.</summary>
public sealed record PageRouteTemplateMatch(
    long PageId,
    string Culture,
    string ResolvedPath,
    IReadOnlyDictionary<string, string> RouteValues);

public interface IPageRouteTemplateResolver
{
    Task<Result<PageRouteTemplateMatch?, AeroError>> ResolveAsync(
        long siteId,
        string culture,
        string path,
        CancellationToken ct = default);
}

public interface IPageRouteTemplateValidator
{
    Task<Result<bool, AeroError>> ValidateDraftAsync(PageDocument page, CancellationToken ct = default);
}

/// <summary>
/// Parses the intentionally narrow page-template grammar: absolute literal segments
/// plus exactly one named segment such as <c>/catalog/{entryId}</c>.
/// </summary>
public sealed record PageRouteTemplate(
    string Template,
    IReadOnlyList<string> Segments,
    int ParameterIndex,
    string ParameterName)
{
    public const int MaximumTemplateLength = 256;
    public const int MaximumRouteValueLength = 256;
    private static readonly Regex LiteralPattern = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex ParameterPattern = new(
        "^[A-Za-z][A-Za-z0-9_]{0,63}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex RouteValuePattern = new(
        "^[A-Za-z0-9._~:-]+$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public int Specificity => Segments.Count - 1;

    public static Result<PageRouteTemplate, AeroError> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return AeroError.ValidationError(["A route template is required."]);

        var template = value.Trim();
        if (template.Length > MaximumTemplateLength
            || !template.StartsWith("/", StringComparison.Ordinal)
            || template.EndsWith("/", StringComparison.Ordinal)
            || template.Contains("//", StringComparison.Ordinal)
            || template.Contains('?')
            || template.Contains('#'))
        {
            return AeroError.ValidationError(
                ["Route templates must be absolute, bounded paths without trailing slashes, query strings, or fragments."]);
        }

        var segments = template[1..].Split('/', StringSplitOptions.None);
        var parameterIndex = -1;
        var parameterName = string.Empty;
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (segment.StartsWith('{') && segment.EndsWith('}'))
            {
                if (parameterIndex >= 0 || segment.Contains('*') || segment.Contains(':')
                    || segment.Contains('?') || segment.Contains('='))
                    return AeroError.ValidationError(["A route template must contain exactly one simple named parameter."]);

                parameterName = segment[1..^1];
                if (!ParameterPattern.IsMatch(parameterName))
                    return AeroError.ValidationError(["The route parameter name is invalid."]);
                parameterIndex = index;
            }
            else if (!LiteralPattern.IsMatch(segment))
            {
                return AeroError.ValidationError(
                    ["Route-template literals may contain only lowercase letters, numbers, and single hyphens."]);
            }
        }

        return parameterIndex < 0
            ? AeroError.ValidationError(["A route template must contain exactly one named parameter."])
            : new PageRouteTemplate(template, segments, parameterIndex, parameterName);
    }

    public bool TryMatch(string path, out string stableId)
    {
        stableId = string.Empty;
        var normalized = path.Trim().Trim('/');
        var values = normalized.Split('/', StringSplitOptions.None);
        if (values.Length != Segments.Count) return false;

        for (var index = 0; index < values.Length; index++)
        {
            if (index == ParameterIndex)
            {
                var candidate = values[index];
                if (candidate.Length is 0 or > MaximumRouteValueLength
                    || candidate is "." or ".."
                    || !RouteValuePattern.IsMatch(candidate)) return false;
                stableId = candidate;
            }
            else if (!string.Equals(values[index], Segments[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public bool Overlaps(PageRouteTemplate other)
    {
        if (Segments.Count != other.Segments.Count) return false;
        for (var index = 0; index < Segments.Count; index++)
        {
            if (index != ParameterIndex && index != other.ParameterIndex
                && !string.Equals(Segments[index], other.Segments[index], StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    public static Result<bool, AeroError> ValidateCompositionBindings(
        string? routeTemplate,
        PageCompositionDocument? composition)
    {
        var routeBound = (composition?.ContentItems ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.StableIdRouteParameter))
            .ToArray();
        if (routeBound.Length == 0) return true;

        var parsed = Parse(routeTemplate);
        if (parsed is Result<PageRouteTemplate, AeroError>.Failure failure)
            return failure.Error;
        var template = ((Result<PageRouteTemplate, AeroError>.Ok)parsed).Value;
        var errors = routeBound
            .Where(item => !string.Equals(
                item.StableIdRouteParameter,
                template.ParameterName,
                StringComparison.Ordinal))
            .Select(item =>
                $"Virtual content scope '{item.NodeId}' binds route parameter '{item.StableIdRouteParameter}', but the page template declares '{template.ParameterName}'.")
            .ToArray();
        return errors.Length == 0 ? true : AeroError.ValidationError(errors);
    }
}

public sealed class PageRouteTemplateService(
    IDocumentSession session,
    ISiteContext siteContext) : IPageRouteTemplateResolver, IPageRouteTemplateValidator
{
    public const int MaximumTemplateCandidates = 100;

    public async Task<Result<PageRouteTemplateMatch?, AeroError>> ResolveAsync(
        long siteId,
        string culture,
        string path,
        CancellationToken ct = default)
    {
        if (siteId <= 0 || siteContext.SiteId != siteId)
            return AeroError.NotFoundError("The requested page route was not found.");

        var normalizedCulture = ContentSlugDocument.NormalizeCulture(culture);
        var requested = await ResolveCultureAsync(siteId, normalizedCulture, path, ct);
        if (requested is Result<PageRouteTemplateMatch?, AeroError>.Failure) return requested;
        if (((Result<PageRouteTemplateMatch?, AeroError>.Ok)requested).Value is not null) return requested;

        var site = await session.LoadAsync<SitesModel>(siteId, ct);
        var defaultCulture = ContentSlugDocument.NormalizeCulture(
            site?.DefaultCulture ?? SitesModel.DefaultCultureName);
        return string.Equals(normalizedCulture, defaultCulture, StringComparison.OrdinalIgnoreCase)
            ? requested
            : await ResolveCultureAsync(siteId, defaultCulture, path, ct);
    }

    public async Task<Result<bool, AeroError>> ValidateDraftAsync(
        PageDocument page,
        CancellationToken ct = default)
    {
        if (page.SiteId <= 0 || page.SiteId != siteContext.SiteId)
            return AeroError.NotFoundError("The requested page route was not found.");
        if (string.IsNullOrWhiteSpace(page.DraftRouteTemplate)) return true;
        var parsed = PageRouteTemplate.Parse(page.DraftRouteTemplate);
        if (parsed is Result<PageRouteTemplate, AeroError>.Failure failure) return failure.Error;
        var template = ((Result<PageRouteTemplate, AeroError>.Ok)parsed).Value;

        var candidates = await session.Query<PageDocument>()
            .Where(candidate => candidate.SiteId == page.SiteId
                && candidate.Culture == page.Culture
                && candidate.Deleted == false
                && (candidate.DraftRouteTemplate != null
                    || (candidate.PublicationState == ContentPublicationState.Published
                        && candidate.PublishedRouteTemplate != null)))
            .Take(MaximumTemplateCandidates + 1)
            .ToListAsync(ct);
        if (candidates.Count > MaximumTemplateCandidates)
            return AeroError.ValidationError(["The page route-template candidate limit was exceeded."]);

        foreach (var candidate in candidates.Where(candidate => candidate.Id != page.Id))
        {
            var otherTemplates = new[]
            {
                candidate.DraftRouteTemplate,
                candidate.PublicationState == ContentPublicationState.Published
                    ? candidate.PublishedRouteTemplate
                    : null
            };
            foreach (var otherTemplate in otherTemplates
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var candidateParsed = PageRouteTemplate.Parse(otherTemplate);
                if (candidateParsed is Result<PageRouteTemplate, AeroError>.Ok ok
                    && template.Overlaps(ok.Value))
                {
                    return AeroError.ConflictError(
                        $"Route template '{template.Template}' overlaps '{ok.Value.Template}' in culture '{page.Culture}'.");
                }
            }
        }
        return true;
    }

    private async Task<Result<PageRouteTemplateMatch?, AeroError>> ResolveCultureAsync(
        long siteId,
        string culture,
        string path,
        CancellationToken ct)
    {
        var candidates = await session.Query<PageDocument>()
            .Where(candidate => candidate.SiteId == siteId
                && candidate.PublicationState == ContentPublicationState.Published
                && candidate.Deleted == false
                && candidate.Culture == culture
                && candidate.PublishedRouteTemplate != null)
            .Take(MaximumTemplateCandidates + 1)
            .ToListAsync(ct);
        if (candidates.Count > MaximumTemplateCandidates)
            return AeroError.ValidationError(["The published page route-template candidate limit was exceeded."]);

        return SelectPublishedMatch(siteId, culture, path, candidates);
    }

    /// <summary>Selects one deterministic match from an already site/culture-scoped bounded set.</summary>
    public static Result<PageRouteTemplateMatch?, AeroError> SelectPublishedMatch(
        long siteId,
        string culture,
        string path,
        IReadOnlyList<PageDocument> candidates)
    {
        if (candidates.Count > MaximumTemplateCandidates)
            return AeroError.ValidationError(["The published page route-template candidate limit was exceeded."]);
        var matches = new List<(PageDocument Page, PageRouteTemplate Template, string Value)>();
        foreach (var page in candidates.Where(page =>
                     page.SiteId == siteId
                     && page.PublicationState == ContentPublicationState.Published
                     && !page.Deleted
                     && string.Equals(page.Culture, culture, StringComparison.OrdinalIgnoreCase)))
        {
            var parsed = PageRouteTemplate.Parse(page.PublishedRouteTemplate);
            if (parsed is Result<PageRouteTemplate, AeroError>.Ok ok
                && ok.Value.TryMatch(path, out var value))
                matches.Add((page, ok.Value, value));
        }

        if (matches.Count == 0) return Prelude.Ok<PageRouteTemplateMatch?, AeroError>(null);
        var ordered = matches
            .OrderByDescending(match => match.Template.Specificity)
            .ThenBy(match => match.Template.Template, StringComparer.Ordinal)
            .ThenBy(match => match.Page.Id)
            .ToArray();
        if (ordered.Length > 1 && ordered[0].Template.Specificity == ordered[1].Template.Specificity)
            return AeroError.ConflictError("Published page route templates are ambiguous.");

        var winner = ordered[0];
        return new PageRouteTemplateMatch(
            winner.Page.Id,
            winner.Page.Culture,
            "/" + path.Trim().Trim('/'),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [winner.Template.ParameterName] = winner.Value
            });
    }
}
