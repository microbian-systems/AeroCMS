using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Provides site-scoped Sable persistence operations for content type definitions.
/// </summary>
/// <remarks>
/// Cancellation, query, and commit failures are not converted to <see cref="AeroError"/>
/// values and may propagate to the caller.
/// </remarks>
public interface IContentTypeService
{
    /// <summary>Loads a content type by its stable identifier within a site.</summary>
    /// <param name="siteId">The owning site identifier.</param><param name="id">The stable content-type identifier.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns>The definition, or a failed result when the identifier is not found for the site.</returns>
    Task<Result<ContentTypeDefinition, AeroError>> GetByIdAsync(long siteId, long id, CancellationToken ct = default);

    /// <summary>Loads a content type by its site-scoped alias.</summary>
    /// <param name="siteId">The owning site identifier.</param><param name="alias">The content type alias.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns>The definition, or a failed result when the alias is not found for the site.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    Task<Result<ContentTypeDefinition, AeroError>> GetByAliasAsync(long siteId, string alias, CancellationToken ct = default);

    /// <summary>Lists the content type definitions for a site.</summary>
    /// <param name="siteId">The owning site identifier.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns>A successful result containing all definitions for the site.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    Task<Result<IReadOnlyList<ContentTypeDefinition>, AeroError>> GetAllAsync(long siteId, CancellationToken ct = default);

    /// <summary>Prepares, validates, stores, and commits a content type definition.</summary>
    /// <param name="definition">The definition to persist.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns>
    /// The caller-supplied definition on success; otherwise a failed result when the site
    /// already has a different definition with the same alias or template validation fails.
    /// </returns>
    /// <remarks>
    /// Saving may assign the definition's <c>Id</c> and replace
    /// <see cref="ContentTypeDefinition.ScribanTemplate"/> on the supplied instance before
    /// persistence. A blank template is generated from the registered field snippets.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    Task<Result<ContentTypeDefinition, AeroError>> SaveAsync(ContentTypeDefinition definition, CancellationToken ct = default);

    /// <summary>Deletes a content type definition by its site-scoped alias.</summary>
    /// <param name="siteId">The owning site identifier.</param><param name="alias">The content type alias.</param><param name="ct">A token that can cancel the operation.</param>
    /// <returns>
    /// A successful result containing <see langword="true"/> when a definition was committed
    /// as deleted, or <see langword="false"/> when no matching definition exists.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is canceled.</exception>
    Task<Result<bool, AeroError>> DeleteAsync(long siteId, string alias, CancellationToken ct = default);
}
