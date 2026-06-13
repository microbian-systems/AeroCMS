using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Services;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Navigation.Rendering;

public sealed class NavMenuContext
{
    public NavMenuSnapshot? Snapshot { get; private set; }
    public bool HasSnapshot => Snapshot is not null;

    public IReadOnlyList<INavMenuComponent> Left => Snapshot?.Left ?? [];
    public IReadOnlyList<INavMenuComponent> Center => Snapshot?.Center ?? [];
    public IReadOnlyList<INavMenuComponent> Right => Snapshot?.Right ?? [];
    public IReadOnlyList<NavCanvasRow> Rows => Snapshot?.Rows ?? [];
    public string? SiteLogoUrl => Snapshot?.SiteLogoUrl;

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
