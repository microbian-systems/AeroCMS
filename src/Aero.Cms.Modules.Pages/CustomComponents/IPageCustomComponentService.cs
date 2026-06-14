using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.CustomComponents;

public interface IPageCustomComponentService
{
    Task<Result<IReadOnlyList<PageCustomComponent>, AeroError>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Result<PageCustomComponent, AeroError>> SaveAsync(
        SavePageCustomComponentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PageCustomComponent, AeroError>> UpdateAsync(
        long componentId,
        SavePageCustomComponentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<NeoPageNode, AeroError>> CreateInstanceAsync(
        long componentId,
        CancellationToken cancellationToken = default);

    Task<Result<bool, AeroError>> DeleteAsync(
        long componentId,
        CancellationToken cancellationToken = default);
}
