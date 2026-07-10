using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Services;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Footer.Rendering;

/// <summary>
/// Represents a class for FooterContext.
/// </summary>
public sealed class FooterContext
{
        /// <summary>
    /// Gets or sets the Snapshot.
    /// </summary>
public FooterSnapshot? Snapshot { get; private set; }
        /// <summary>
    /// Gets or sets the Has Snapshot.
    /// </summary>
public bool HasSnapshot => Snapshot is not null;

        /// <summary>
    /// ResolveAsync method.
    /// </summary>
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
