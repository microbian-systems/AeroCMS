using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.CustomComponents;

/// <summary>
/// Defines an interface for IPageCustomComponentService.
/// </summary>
public interface IPageCustomComponentService
{
        /// <summary>
    /// GetAllAsync method.
    /// </summary>
Task<Result<IReadOnlyList<PageCustomComponent>, AeroError>> GetAllAsync(
        CancellationToken cancellationToken = default);

        /// <summary>
    /// SaveAsync method.
    /// </summary>
Task<Result<PageCustomComponent, AeroError>> SaveAsync(
        SavePageCustomComponentRequest request,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// UpdateAsync method.
    /// </summary>
Task<Result<PageCustomComponent, AeroError>> UpdateAsync(
        long componentId,
        SavePageCustomComponentRequest request,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// CreateInstanceAsync method.
    /// </summary>
Task<Result<NeoPageNode, AeroError>> CreateInstanceAsync(
        long componentId,
        CancellationToken cancellationToken = default);

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
Task<Result<bool, AeroError>> DeleteAsync(
        long componentId,
        CancellationToken cancellationToken = default);
}
