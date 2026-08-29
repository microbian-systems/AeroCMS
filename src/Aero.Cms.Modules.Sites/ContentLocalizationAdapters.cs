using System.Globalization;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Infrastructure;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Sites;

/// <summary>Resolves localization settings exclusively from persisted enabled-site and type records.</summary>
public sealed class ContentLocalizationContextResolver(
    IQuerySession session,
    IContentTypeService contentTypes) : IContentLocalizationContextResolver
{
    /// <inheritdoc />
    public async Task<ContentLocalizationContext?> ResolveAsync(long siteId, string contentTypeAlias, CancellationToken cancellationToken = default)
    {
        if (siteId <= 0 || string.IsNullOrWhiteSpace(contentTypeAlias)) return null;
        var site = await session.LoadAsync<SitesModel>(siteId, cancellationToken);
        if (site is not { Id: > 0, IsEnabled: true } || site.Id != siteId) return null;

        var type = await contentTypes.GetByAliasAsync(siteId, contentTypeAlias, cancellationToken);
        if (type is not Result<ContentTypeDefinition, AeroError>.Ok typeOk) return null;

        var cultures = Normalize(site.SupportedCultures);
        var defaultCulture = Canonical(site.DefaultCulture);
        if (defaultCulture is null || cultures.Count == 0 || !cultures.Contains(defaultCulture, StringComparer.OrdinalIgnoreCase)) return null;

        return new ContentLocalizationContext(siteId, defaultCulture, cultures, typeOk.Value.Localization.CultureFallbackPolicy);
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string>? values) => (values ?? [])
        .Select(Canonical).Where(value => value is not null).Select(value => value!)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static string? Canonical(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return CultureInfo.GetCultureInfo(value.Trim()).Name; }
        catch (CultureNotFoundException) { return null; }
    }
}

/// <summary>Requires the request's selected persisted site and its existing <c>site:update</c> authorization.</summary>
public sealed class SelectedSiteContentTranslationAuthorizer(
    IHttpContextAccessor httpContextAccessor,
    ISiteContext siteContext,
    ISelectedSiteScopeResolver selectedSites,
    IAuthorizationService authorizationService) : IContentTranslationSiteAuthorizer
{
    /// <inheritdoc />
    public async Task<Result<NoneType, AeroError>> AuthorizeAsync(long siteId, CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (siteId <= 0 || httpContext is null || siteContext.SiteId != siteId)
            return AeroError.ForbiddenError("The requested site is not the selected site.");

        var selected = await selectedSites.ResolveAsync(siteId, cancellationToken);
        if (selected is not { IsValid: true } || selected.Value.SiteId != siteId)
            return AeroError.ForbiddenError("The selected site could not be resolved.");

        var authorization = await authorizationService.AuthorizeAsync(httpContext.User, null, "site:update");
        return authorization.Succeeded
            ? Prelude.Ok<NoneType, AeroError>(default)
            : AeroError.ForbiddenError("The selected site does not permit content updates.");
    }
}

/// <summary>Loads the AI translation source and target exclusively from site-scoped content persistence.</summary>
public sealed class ContentAiTranslationSnapshotResolver(
    IContentService content,
    IContentTypeService contentTypes,
    IContentLocalizationContextResolver localizationContexts) : IContentAiTranslationSnapshotResolver
{
    /// <inheritdoc />
    public async Task<Result<ContentAiTranslationGenerationSnapshot>> ResolveAsync(long siteId, long sourceItemId, long targetItemId, CancellationToken cancellationToken = default)
    {
        if (siteId <= 0 || sourceItemId <= 0 || targetItemId <= 0 || sourceItemId == targetItemId)
            return AeroError.ValidationError(["Source and target identifiers must be distinct positive values."]);

        var sourceResult = await content.LoadAsync(siteId, sourceItemId, cancellationToken);
        var targetResult = await content.LoadAsync(siteId, targetItemId, cancellationToken);
        if (sourceResult is not Result<ContentItem, AeroError>.Ok sourceOk || targetResult is not Result<ContentItem, AeroError>.Ok targetOk)
            return AeroError.NotFoundError("The requested translation variants were not found in the selected site.");

        var source = sourceOk.Value;
        var target = targetOk.Value;
        if (string.IsNullOrWhiteSpace(source.ContentTypeAlias)
            || !string.Equals(source.ContentTypeAlias, target.ContentTypeAlias, StringComparison.OrdinalIgnoreCase)
            || source.TranslationGroupId is not { } groupId || groupId <= 0 || target.TranslationGroupId != groupId
            || source.VersionNumber <= 0 || target.VersionNumber <= 0)
            return AeroError.ValidationError(["Source and target must be versioned variants in the same content type and translation group."]);

        var typeResult = await contentTypes.GetByAliasAsync(siteId, source.ContentTypeAlias, cancellationToken);
        var context = await localizationContexts.ResolveAsync(siteId, source.ContentTypeAlias, cancellationToken);
        if (typeResult is not Result<ContentTypeDefinition, AeroError>.Ok typeOk || context is null)
            return AeroError.NotFoundError("The selected site content type is not available for localization.");

        return new ContentAiTranslationGenerationSnapshot(
            typeOk.Value,
            context,
            new ContentTranslationSource(source.Id, source.SiteId, source.ContentTypeAlias, groupId, source.VersionNumber, source.Culture, source.Fields),
            new ContentTranslationTarget(target.Id, target.SiteId, target.ContentTypeAlias, groupId, target.VersionNumber, target.Culture));
    }
}
