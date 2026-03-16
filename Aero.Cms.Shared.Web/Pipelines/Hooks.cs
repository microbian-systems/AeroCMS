using Aero.Cms.Shared.Pipelines;

namespace Aero.Cms.Shared.Pipelines;

public interface IPageReadHook
{
    int Order { get; }
    Task ExecuteAsync(PageReadContext ctx, CancellationToken ct);
}

public interface IPageSaveHook
{
    int Order { get; }
    Task ExecuteAsync(PageSaveContext ctx, CancellationToken ct);
}

public interface IBlockRenderHook
{
    int Order { get; }
    Task ExecuteAsync(BlockRenderContext ctx, CancellationToken ct);
}
