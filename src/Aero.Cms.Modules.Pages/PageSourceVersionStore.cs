using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Describes an exact source snapshot to stage in the current Pages unit of work.
/// </summary>
/// <param name="SiteId">The owning site identifier.</param>
/// <param name="PageId">The owning page identifier.</param>
/// <param name="RendererId">The stable renderer identifier.</param>
/// <param name="Source">The exact source text to preserve.</param>
/// <param name="CreatedOn">The snapshot creation timestamp.</param>
/// <param name="CreatedBy">The creating actor, when available.</param>
public sealed record PageSourceVersionWriteRequest(
    long SiteId,
    long PageId,
    string RendererId,
    string Source,
    DateTimeOffset CreatedOn,
    string? CreatedBy = null);

/// <summary>
/// An immutable source version returned to a page renderer.
/// </summary>
/// <param name="Id">The source-version identifier.</param>
/// <param name="SiteId">The owning site identifier.</param>
/// <param name="PageId">The owning page identifier.</param>
/// <param name="RendererId">The stable renderer identifier.</param>
/// <param name="Source">The exact persisted source text.</param>
/// <param name="SourceHash">The lowercase hexadecimal SHA-256 source hash.</param>
/// <param name="CreatedOn">The snapshot creation timestamp.</param>
/// <param name="CreatedBy">The creating actor, when available.</param>
public sealed record PageSourceVersionSnapshot(
    long Id,
    long SiteId,
    long PageId,
    string RendererId,
    string Source,
    string SourceHash,
    DateTimeOffset CreatedOn,
    string? CreatedBy);

/// <summary>
/// Stages and resolves append-only inline page source versions.
/// </summary>
public interface IPageSourceVersionStore
{
    /// <summary>
    /// Stages a new source version in the current Sable session without committing it.
    /// </summary>
    Result<PageSourceVersionSnapshot> Stage(PageSourceVersionWriteRequest request);

    /// <summary>
    /// Resolves an owned source version, returning a successful absence for an empty pointer.
    /// </summary>
    Task<Result<PageSourceVersionSnapshot?>> LoadAsync(
        long? sourceVersionId,
        long siteId,
        long pageId,
        string rendererId,
        CancellationToken ct = default);
}

/// <summary>
/// Pages-owned inline Sable implementation of <see cref="IPageSourceVersionStore"/>.
/// </summary>
public sealed class PageSourceVersionStore(IDocumentSession session) : IPageSourceVersionStore
{
    /// <inheritdoc />
    public Result<PageSourceVersionSnapshot> Stage(PageSourceVersionWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = ValidateOwnership(request.SiteId, request.PageId, request.RendererId);
        if (validation is Result<string>.Failure ownershipFailure)
        {
            return ownershipFailure.Error;
        }

        if (request.Source is null)
        {
            return AeroError.ValidationError(["Page source cannot be null."]);
        }

        var rendererId = ((Result<string>.Ok)validation).Value;
        var version = new PageSourceVersion
        {
            SiteId = request.SiteId,
            PageId = request.PageId,
            RendererId = rendererId,
            Source = request.Source,
            SourceHash = ComputeHash(request.Source),
            CreatedOn = request.CreatedOn,
            CreatedBy = request.CreatedBy
        };

        try
        {
            session.Store(version);
            return ToSnapshot(version);
        }
        catch (Exception exception)
        {
            return AeroError.DatabaseError(exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result<PageSourceVersionSnapshot?>> LoadAsync(
        long? sourceVersionId,
        long siteId,
        long pageId,
        string rendererId,
        CancellationToken ct = default)
    {
        if (sourceVersionId is null)
        {
            return new Result<PageSourceVersionSnapshot?>.Ok(null);
        }

        var validation = ValidateOwnership(siteId, pageId, rendererId);
        if (validation is Result<string>.Failure ownershipFailure)
        {
            return ownershipFailure.Error;
        }

        if (sourceVersionId <= 0)
        {
            return NotFound();
        }

        try
        {
            var version = await session.LoadAsync<PageSourceVersion>(sourceVersionId.Value, ct);
            var expectedRendererId = ((Result<string>.Ok)validation).Value;

            if (version is null
                || version.SiteId != siteId
                || version.PageId != pageId
                || !string.Equals(version.RendererId, expectedRendererId, StringComparison.Ordinal))
            {
                return NotFound();
            }

            return ToSnapshot(version);
        }
        catch (Exception exception)
        {
            return AeroError.DatabaseError(exception.Message);
        }
    }

    private static Result<string> ValidateOwnership(long siteId, long pageId, string? rendererId)
    {
        var errors = new List<string>();
        if (siteId <= 0)
        {
            errors.Add("The page source site identifier must be positive.");
        }

        if (pageId <= 0)
        {
            errors.Add("The page source page identifier must be positive.");
        }

        var normalizedRendererId = rendererId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!PageRendererIds.IsValid(normalizedRendererId))
        {
            errors.Add("The page source renderer identifier is invalid.");
        }

        return errors.Count == 0
            ? normalizedRendererId
            : AeroError.ValidationError(errors);
    }

    private static string ComputeHash(string source)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))
            .ToLowerInvariant();

    private static PageSourceVersionSnapshot ToSnapshot(PageSourceVersion version)
        => new(
            version.Id,
            version.SiteId,
            version.PageId,
            version.RendererId,
            version.Source,
            version.SourceHash,
            version.CreatedOn,
            version.CreatedBy);

    private static AeroError NotFound()
        => AeroError.NotFoundError("Page source version not found or access denied.");
}
