using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Services;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Navigation.Rendering;

/// <summary>
/// Holds the navigation snapshot resolved for one scoped rendering request.
/// </summary>
/// <remarks>
/// Register this type as scoped. Reusing an instance across requests would leak the previously
/// resolved site's navigation state.
/// </remarks>
public sealed class NavMenuContext
{
    /// <summary>
    /// Gets the snapshot most recently resolved into this context.
    /// </summary>
public NavMenuSnapshot? Snapshot { get; private set; }
    /// <summary>
    /// Gets whether a published navigation snapshot was resolved.
    /// </summary>
public bool HasSnapshot => Snapshot is not null;

    /// <summary>
    /// Gets the legacy left-aligned components, or an empty list when unresolved.
    /// </summary>
public IReadOnlyList<INavMenuComponent> Left => Snapshot?.Left ?? [];
    /// <summary>
    /// Gets the legacy center-aligned components, or an empty list when unresolved.
    /// </summary>
public IReadOnlyList<INavMenuComponent> Center => Snapshot?.Center ?? [];
    /// <summary>
    /// Gets the legacy right-aligned components, or an empty list when unresolved.
    /// </summary>
public IReadOnlyList<INavMenuComponent> Right => Snapshot?.Right ?? [];
    /// <summary>
    /// Gets the row-based navigation canvas, or an empty list when unresolved.
    /// </summary>
public IReadOnlyList<NavCanvasRow> Rows => Snapshot?.Rows ?? [];
    /// <summary>
    /// Gets the optional site logo URL from the resolved snapshot.
    /// </summary>
public string? SiteLogoUrl => Snapshot?.SiteLogoUrl;

    /// <summary>
    /// Resolves and stores a culture-aware published snapshot for the request.
    /// </summary>
    /// <param name="siteId">The site used for default-menu resolution.</param>
    /// <param name="pageOverrideId">An optional page-configured menu identifier.</param>
    /// <param name="navMenuService">The navigation service used for persistence reads.</param>
    /// <param name="cancellationToken">The token used through resolution.</param>
    /// <returns>
    /// A success containing whether a snapshot was found, or the service failure. On failure,
    /// the existing <see cref="Snapshot"/> value is left unchanged.
    /// </returns>
    /// <remarks>
    /// The service contract requires trusted callers to ensure that a page override belongs
    /// to <paramref name="siteId"/>.
    /// </remarks>
public async Task<Result<bool, AeroError>> ResolveAsync(
        long siteId,
        long? pageOverrideId,
        INavMenuService navMenuService,
        CancellationToken cancellationToken = default)
    {
        var result = await navMenuService.ResolveSnapshotAsync(siteId, pageOverrideId, cancellationToken);
        if (result is Result<NavMenuSnapshot?, AeroError>.Failure failure)
        {
            return Fail<bool, AeroError>(failure.Error);
        }

        Snapshot = ((Result<NavMenuSnapshot?, AeroError>.Ok)result).Value;
        return Ok<bool, AeroError>(Snapshot is not null);
    }
}
