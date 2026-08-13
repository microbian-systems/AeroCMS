namespace Aero.Cms.Abstractions.Content.Localization;

/// <summary>Resolves server-owned site and content-type localization inputs.</summary>
/// <remarks>A null result denies the operation. Browser, route, and request culture values are never authoritative.</remarks>
public interface IContentLocalizationContextResolver
{
    /// <summary>Loads context for one enabled persisted site and its site-scoped content type.</summary>
    Task<ContentLocalizationContext?> ResolveAsync(
        long siteId,
        string contentTypeAlias,
        CancellationToken cancellationToken = default);
}
