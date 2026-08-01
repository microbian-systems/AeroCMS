using Aero.Cms.Abstractions.Theming;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Theming;

/// <summary>Read-only composition of deployment themes and immutable tenant publications.</summary>
public sealed class CompositeThemeLibrary(IThemeCatalog deployment, IQuerySession session) : IThemeLibrary
{
    public async ValueTask<IReadOnlyList<ResolvedThemeManifest>> GetAvailableAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
        var generated = await session.Query<ThemeVersionDocument>().Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken);
        return deployment.GetAll().Select(ToResolved).Concat(generated.Select(ToResolved)).OrderBy(x => x.ThemeId, StringComparer.Ordinal).ThenBy(x => x.ThemeVersion, StringComparer.Ordinal).ToArray();
    }

    public async ValueTask<ResolvedThemeManifest?> ResolveAsync(long tenantId, string themeId, string themeVersion, CancellationToken cancellationToken = default)
    {
        var installed = deployment.Find(themeId, themeVersion);
        if (installed is not null) return ToResolved(installed);
        if (tenantId <= 0) return null;
        var generated = await session.Query<ThemeVersionDocument>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ThemeId == themeId && x.Version == themeVersion, cancellationToken);
        return generated is null ? null : ToResolved(generated);
    }

    private static ResolvedThemeManifest ToResolved(InstalledThemeManifest theme) => new(theme.Id, theme.Version, BuiltInThemeDefaults.ComponentThemeName, ThemeSource.Deployment, theme.Stylesheets);
    private static ResolvedThemeManifest ToResolved(ThemeVersionDocument theme) => new(theme.ThemeId, theme.Version, theme.DataThemeName, ThemeSource.Generated, [new ThemeStylesheetAsset($"/_cms/themes/{theme.TenantId}/{theme.ThemeId}/{theme.Version}/{theme.CssSha256}.css", 100)]);
}
