using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Services;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Navigation.Rendering;

/// <summary>
/// Represents a class for NavMenuContext.
/// </summary>
public sealed class NavMenuContext
{
        /// <summary>
    /// Gets or sets the Snapshot.
    /// </summary>
public NavMenuSnapshot? Snapshot { get; private set; }
        /// <summary>
    /// Gets or sets the Has Snapshot.
    /// </summary>
public bool HasSnapshot => Snapshot is not null;

        /// <summary>
    /// Gets or sets the Left.
    /// </summary>
public IReadOnlyList<INavMenuComponent> Left => Snapshot?.Left ?? [];
        /// <summary>
    /// Gets or sets the Center.
    /// </summary>
public IReadOnlyList<INavMenuComponent> Center => Snapshot?.Center ?? [];
        /// <summary>
    /// Gets or sets the Right.
    /// </summary>
public IReadOnlyList<INavMenuComponent> Right => Snapshot?.Right ?? [];
        /// <summary>
    /// Gets or sets the Rows.
    /// </summary>
public IReadOnlyList<NavCanvasRow> Rows => Snapshot?.Rows ?? [];
        /// <summary>
    /// Gets or sets the Site Logo Url.
    /// </summary>
public string? SiteLogoUrl => Snapshot?.SiteLogoUrl;

        /// <summary>
    /// ResolveAsync method.
    /// </summary>
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
