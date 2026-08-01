using Aero.Cms.Html;

namespace Aero.Cms.Abstractions.Interfaces;

/// <summary>
/// Resolves the validated style profile owned by a site.
/// </summary>
public interface ISiteStyleProfileResolver
{
    Task<Result<IStyleProfile, AeroError>> ResolveAsync(
        long siteId,
        CancellationToken cancellationToken = default);
}
