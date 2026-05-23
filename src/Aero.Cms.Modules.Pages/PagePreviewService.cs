using Aero.Cms.Abstractions.Blocks;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Builds a transient render manifest from the current editor state.
/// Follows the preview pipeline: load PageEditorState → load PageDocument metadata →
/// batch-load BlockBase → build via IPageLayoutManifestBuilder.
/// Never writes to PageDocument.LayoutRegions.
/// </summary>
internal sealed class PagePreviewService : IPagePreviewService
{
    private readonly IDocumentSession _session;
    private readonly IPageLayoutManifestBuilder _builder;
    private readonly IBlockService _blockService;
    private readonly ILogger<PagePreviewService> _logger;

    public PagePreviewService(
        IDocumentSession session,
        IPageLayoutManifestBuilder builder,
        IBlockService blockService,
        ILogger<PagePreviewService> logger)
    {
        _session = session;
        _builder = builder;
        _blockService = blockService;
        _logger = logger;
    }

    public async Task<Result<PreviewRenderModel, Aero.Core.AeroError>> BuildPreviewAsync(
        long pageId,
        CancellationToken ct = default)
    {
        try
        {
            // 1. Load PageDocument (metadata only)
            var page = await _session.LoadAsync<PageDocument>(pageId, ct);
            if (page is null)
            {
                return Fail<PreviewRenderModel, Aero.Core.AeroError>(
                    Aero.Core.AeroError.NotFoundError($"Page {pageId} not found."));
            }

            // 2. Load PageEditorState (may not exist yet)
            var editor = await _session.LoadAsync<PageEditorState>(pageId, ct);

            // 3. Load BlockBase documents for all resolved BlockIds
            // N+1 REMEDY: Previously used foreach + GetByIdAsync (one DB query
            // per block). Now uses GetByIdsAsync which issues a single batched
            // Marten LoadManyAsync (WHERE Id = ANY(@ids)). For a page with 40
            // blocks, this reduces 40 round-trips to 1.
            var blockIds = editor?.Blocks
                .Where(p => p.BlockId.HasValue)
                .Select(p => p.BlockId!.Value)
                .Distinct()
                .ToList() ?? [];

            var blockIndex = blockIds.Count > 0
                ? new Dictionary<long, BlockBase>(
                    await _blockService.GetByIdsAsync(blockIds, ct))
                : new Dictionary<long, BlockBase>();

            // 4. Build layout via shared builder
            var previewLayout = await _builder.BuildAsync(editor, blockIndex, ct);

            return Ok<PreviewRenderModel, Aero.Core.AeroError>(new PreviewRenderModel
            {
                PageMeta = page,
                PreviewLayout = previewLayout
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build preview for page {PageId}", pageId);
            return Fail<PreviewRenderModel, Aero.Core.AeroError>(
                Aero.Core.AeroError.DatabaseError(ex.Message));
        }
    }
}
