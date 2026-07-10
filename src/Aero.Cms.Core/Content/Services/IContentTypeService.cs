using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Defines an interface for IContentTypeService.
/// </summary>
public interface IContentTypeService
{
        /// <summary>
    /// GetByAliasAsync method.
    /// </summary>
Task<Result<ContentTypeDefinition, AeroError>> GetByAliasAsync(long siteId, string alias, CancellationToken ct = default);
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<Result<IReadOnlyList<ContentTypeDefinition>, AeroError>> GetAllAsync(long siteId, CancellationToken ct = default);
        /// <summary>
    /// SaveAsync method.
    /// </summary>
Task<Result<ContentTypeDefinition, AeroError>> SaveAsync(ContentTypeDefinition definition, CancellationToken ct = default);
        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<Result<bool, AeroError>> DeleteAsync(long siteId, string alias, CancellationToken ct = default);
}
