using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Services;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Footer.Rendering;

/// <summary>
/// Holds the footer snapshot resolved for the current scoped rendering flow.
/// </summary>
public sealed class FooterContext
{
    /// <summary>Gets the most recent successfully resolved snapshot.</summary>
    public FooterSnapshot? Snapshot { get; private set; }

    /// <summary>Gets whether the context currently contains a snapshot.</summary>
    public bool HasSnapshot => Snapshot is not null;

    /// <summary>
    /// Resolves the snapshot for a site and stores the result in this context.
    /// </summary>
    /// <param name="siteId">The site whose published footer should be resolved.</param>
    /// <param name="footerService">The service used to resolve defaults and culture variants.</param>
    /// <param name="cancellationToken">A token forwarded to the service.</param>
    /// <returns>
    /// A successful result containing whether a snapshot was found, or the service failure unchanged.
    /// </returns>
    /// <remarks>
    /// On failure, the existing <see cref="Snapshot"/> value is preserved. On success, it is replaced,
    /// including with <see langword="null"/> when no published footer resolves.
    /// </remarks>
    public async Task<Result<bool, AeroError>> ResolveAsync(
        long siteId,
        IFooterService footerService,
        CancellationToken cancellationToken = default)
    {
        var result = await footerService.ResolveSnapshotAsync(siteId, cancellationToken);
        if (result is Result<FooterSnapshot?, AeroError>.Failure failure)
        {
            return Fail<bool, AeroError>(failure.Error);
        }

        Snapshot = ((Result<FooterSnapshot?, AeroError>.Ok)result).Value;
        return Ok<bool, AeroError>(Snapshot is not null);
    }
}
