using System.Reflection;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Blocks.Editing;
using Aero.Cms.Modules.Pages.Models;
using Aero.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Pages.Areas.Admin.Pages;

public class PagesEditModel(IPageContentService pageService, IDocumentSession session) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long? Id { get; set; }
    [BindProperty]
    public PageDocument? PageDocument { get; set; }
    public bool IsNew { get; private set; }
    public string? ErrorMessage { get; private set; }
    // Block editing state
    public BlockBase? SelectedBlock { get; set; }
    public bool ShowBlockPicker { get; set; }
    public bool ShowBlockEditor { get; set; }
    // Track which region/column we're editing
    public string EditingRegionName { get; set; } = "MainContent";
    public int EditingColumnIndex { get; set; }
    // EventCallbacks for Blazor components
    public EventCallback<BlockTypeInfo> OnBlockTypeSelected => EventCallback.Factory.Create<BlockTypeInfo>(this, HandleBlockTypeSelected);
    public EventCallback OnCancelBlockPicker => EventCallback.Factory.Create(this, HandleCancelBlockPicker);
    public EventCallback<BlockBase> OnBlockSave => EventCallback.Factory.Create<BlockBase>(this, HandleBlockSave);
    public EventCallback OnBlockCancel => EventCallback.Factory.Create(this, HandleBlockCancel);
    public EventCallback<BlockBase> OnBlockDelete => EventCallback.Factory.Create<BlockBase>(this, HandleBlockDelete);
    public EventCallback<BlockBase> OnBlockDuplicate => EventCallback.Factory.Create<BlockBase>(this, HandleBlockDuplicate);
    public EventCallback<BlockBase> OnBlockMoveUp => EventCallback.Factory.Create<BlockBase>(this, HandleBlockMoveUp);
    public EventCallback<BlockBase> OnBlockMoveDown => EventCallback.Factory.Create<BlockBase>(this, HandleBlockMoveDown);

    // Can move flags
    public bool CanMoveBlockUp => SelectedBlock?.Order > 0;
    public bool CanMoveBlockDown
    {
        get
        {
            if (PageDocument is null || SelectedBlock is null) return false;
            var region = PageDocument.LayoutRegions.FirstOrDefault(r => r.Name == EditingRegionName);
            if (region is null || region.Columns.Count <= EditingColumnIndex) return false;
            var column = region.Columns[EditingColumnIndex];
            return SelectedBlock.Order < column.Blocks.Count - 1;
        }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        if (Id.HasValue)
        {
            var result = await pageService.LoadAsync(Id.Value, cancellationToken);
            PageDocument = result switch
            {
                Result<string, PageDocument?>.Ok(var page) => page,
                Aero.Core.Railway.Result<string, PageDocument?>.Failure => null,
                _ => null
            };

            if (PageDocument is null)
            {
                return NotFound();
            }

            IsNew = false;
        }
        else
        {
            // Create new page with default layout structure
            PageDocument = new PageDocument
            {
                LayoutRegions = new List<LayoutRegion>
                {
                    new LayoutRegion
                    {
                        Name = "MainContent",
                        Order = 0,
                        Columns = new List<LayoutColumn>
                        {
                            new LayoutColumn { Width = 12, Order = 0, Blocks = new List<BlockPlacement>() }
                        }
                    }
                }
            };
            IsNew = true;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (PageDocument is null)
        {
            ModelState.AddModelError(string.Empty, "Page data is required");
            return Page();
        }

        var result = await pageService.SaveAsync(PageDocument, cancellationToken);

        if (((result is Result<string, PageDocument>.Ok)))
        {
            return RedirectToPage("/Admin/Index");
        }

        if (result is Result<string, PageDocument>.Failure(var error))
        {
            ErrorMessage = error;
        }
        else
        {
            ErrorMessage = "Unknown error occurred";
        }

        return Page();
    }

    // EventCallback handlers
    private Task HandleBlockTypeSelected(BlockTypeInfo blockTypeInfo)
    {
        // This would be called by the BlockPicker component
        // In prerendered mode, we rely on form posts instead
        return Task.CompletedTask;
    }

    private Task HandleCancelBlockPicker()
    {
        ShowBlockPicker = false;
        return Task.CompletedTask;
    }

    private Task HandleBlockSave(BlockBase block)
    {
        if (block is not null)
        {
            session.Store(block);
            ShowBlockEditor = false;
            ShowBlockPicker = false;
            SelectedBlock = null;
        }
        return Task.CompletedTask;
    }

    private Task HandleBlockCancel()
    {
        ShowBlockPicker = false;
        ShowBlockEditor = false;
        SelectedBlock = null;
        return Task.CompletedTask;
    }

    private Task HandleBlockDelete(BlockBase block)
    {
        if (block is not null && PageDocument is not null)
        {
            OnPostDeleteBlock(block.Id);
        }
        return Task.CompletedTask;
    }

    private Task HandleBlockDuplicate(BlockBase block)
    {
        if (block is not null)
        {
            OnPostDuplicateBlock(block.Id);
        }
        return Task.CompletedTask;
    }

    private Task HandleBlockMoveUp(BlockBase block)
    {
        if (block is not null)
        {
            OnPostMoveBlockUp(block.Id);
        }
        return Task.CompletedTask;
    }

    private Task HandleBlockMoveDown(BlockBase block)
    {
        if (block is not null)
        {
            OnPostMoveBlockDown(block.Id);
        }
        return Task.CompletedTask;
    }

    // Show the block picker
    public IActionResult OnGetShowBlockPicker(string regionName = "MainContent", int columnIndex = 0)
    {
        EditingRegionName = regionName;
        EditingColumnIndex = columnIndex;
        ShowBlockPicker = true;
        ShowBlockEditor = false;
        SelectedBlock = null;
        return Page();
    }

    // Edit a specific block
    public async Task<IActionResult> OnGetEditBlock(long blockId, string regionName = "MainContent", int columnIndex = 0)
    {
        EditingRegionName = regionName;
        EditingColumnIndex = columnIndex;
        
        if (PageDocument is null)
        {
            return Page();
        }

        // Find the block in the layout regions
        SelectedBlock = await FindBlockByIdAsync(blockId);
        ShowBlockEditor = SelectedBlock is not null;
        ShowBlockPicker = false;

        return Page();
    }

    // Handler for when a block type is selected from the picker
    public IActionResult OnPostAddBlock(string blockTypeName)
    {
        if (PageDocument is null || string.IsNullOrEmpty(blockTypeName))
        {
            return Page();
        }

        var region = PageDocument.LayoutRegions.FirstOrDefault(r => r.Name == EditingRegionName);
        if (region is null || region.Columns.Count <= EditingColumnIndex)
        {
            return Page();
        }

        var column = region.Columns[EditingColumnIndex];
        var order = column.Blocks.Count;

        var blockResult = CreateBlock(blockTypeName, order);
        if (blockResult is Result<string, BlockBase>.Ok(var block))
        {
            // Store the block in Marten
            session.Store(block);
            
            // Create a placement referencing the block
            var placement = new BlockPlacement
            {
                BlockId = block.Id,
                BlockType = block.BlockType,
                Order = order
            };
            
            column.Blocks.Add(placement);
            SelectedBlock = block;
            ShowBlockPicker = false;
            ShowBlockEditor = true;
        }
        else if (blockResult is Result<string, BlockBase>.Failure(var error))
        {
            ErrorMessage = error;
        }

        return Page();
    }

    // Handler for saving a block's changes
    public IActionResult OnPostSaveBlock()
    {
        if (SelectedBlock is not null)
        {
            // Block is already updated via binding, just close the editor
            session.Store(SelectedBlock);
            ShowBlockEditor = false;
            ShowBlockPicker = false;
            SelectedBlock = null;
        }

        return Page();
    }

    // Handler for deleting a block
    public async Task<IActionResult> OnPostDeleteBlock(long blockId)
    {
        if (PageDocument is null)
        {
            return Page();
        }

        // Find and remove the block placement
        foreach (var region in PageDocument.LayoutRegions)
        {
            foreach (var column in region.Columns)
            {
                var placement = column.Blocks.FirstOrDefault(b => b.BlockId == blockId);
                if (placement is not null)
                {
                    column.Blocks.Remove(placement);
                    await ReorderBlocksInColumnAsync(region.Name, column);
                    break;
                }
            }
        }

        ShowBlockEditor = false;
        ShowBlockPicker = false;
        SelectedBlock = null;

        return Page();
    }

    // Handler for duplicating a block
    public async Task<IActionResult> OnPostDuplicateBlock(long blockId)
    {
        if (PageDocument is null)
        {
            return Page();
        }

        var sourceBlock = await FindBlockByIdAsync(blockId);
        if (sourceBlock is null)
        {
            return Page();
        }

        var region = PageDocument.LayoutRegions.FirstOrDefault(r => r.Name == EditingRegionName);
        if (region is null || region.Columns.Count <= EditingColumnIndex)
        {
            return Page();
        }

        var column = region.Columns[EditingColumnIndex];
        var duplicateResult = DuplicateBlock(sourceBlock, column.Blocks.Count);
        
        if (duplicateResult is Result<string, BlockBase>.Ok(var duplicate))
        {
            session.Store(duplicate);
            var placement = new BlockPlacement
            {
                BlockId = duplicate.Id,
                BlockType = duplicate.BlockType,
                Order = column.Blocks.Count
            };
            column.Blocks.Add(placement);
        }

        ShowBlockPicker = false;
        ShowBlockEditor = false;
        SelectedBlock = null;

        return Page();
    }

    // Handler for moving a block up
    public async Task<IActionResult> OnPostMoveBlockUp(long blockId)
    {
        if (PageDocument is null)
        {
            return Page();
        }

        var region = PageDocument.LayoutRegions.FirstOrDefault(r => r.Name == EditingRegionName);
        if (region is null || region.Columns.Count <= EditingColumnIndex)
        {
            return Page();
        }

        var column = region.Columns[EditingColumnIndex];
        var block = await FindBlockByIdAsync(blockId);
        if (block is not null && block.Order > 0)
        {
            block.Order--;
            await ReorderBlocksInColumnAsync(region.Name, column);
        }

        return Page();
    }

    // Handler for moving a block down
    public async Task<IActionResult> OnPostMoveBlockDown(long blockId)
    {
        if (PageDocument is null)
        {
            return Page();
        }

        var region = PageDocument.LayoutRegions.FirstOrDefault(r => r.Name == EditingRegionName);
        if (region is null || region.Columns.Count <= EditingColumnIndex)
        {
            return Page();
        }

        var column = region.Columns[EditingColumnIndex];
        var block = await FindBlockByIdAsync(blockId);
        if (block is not null)
        {
            block.Order++;
            await ReorderBlocksInColumnAsync(region.Name, column);
        }

        return Page();
    }

    // Cancel block editing
    public IActionResult OnGetCancelBlockEditing()
    {
        ShowBlockPicker = false;
        ShowBlockEditor = false;
        SelectedBlock = null;
        return Page();
    }

    private async Task<BlockBase?> FindBlockByIdAsync(long blockId)
    {
        if (PageDocument is null)
        {
            return null;
        }

        // Search through all regions and columns to find the block
        foreach (var region in PageDocument.LayoutRegions)
        {
            foreach (var column in region.Columns)
            {
                var placement = column.Blocks.FirstOrDefault(b => b.BlockId == blockId);
                if (placement is not null)
                {
                    // Load the block from Marten
                    return await session.LoadAsync<BlockBase>(blockId);
                }
            }
        }

        return null;
    }

    private async Task ReorderBlocksInColumnAsync(string regionName, LayoutColumn column)
    {
        var blocks = column.Blocks
            .Select(b => new { Placement = b, Block = FindBlockByIdAsync(b.BlockId).GetAwaiter().GetResult() })
            .Where(x => x.Block is not null)
            .OrderBy(x => x.Block!.Order)
            .ToList();

        for (int i = 0; i < blocks.Count; i++)
        {
            blocks[i].Placement.Order = i;
            if (blocks[i].Block is not null)
            {
                blocks[i].Block!.Order = i;
            }
        }
    }

    private static Result<string, BlockBase> CreateBlock(string blockTypeName, int order)
    {
        var blockType = GetBlockType(blockTypeName);
        if (blockType is null)
        {
            return $"Block type '{blockTypeName}' not found.";
        }

        try
        {
            var instance = Activator.CreateInstance(blockType) as BlockBase;
            if (instance is null)
            {
                return $"Failed to create instance of {blockType.Name}.";
            }

            instance.Id = Snowflake.NewId();
            instance.Order = order;
            
            return instance;
        }
        catch (Exception ex)
        {
            return $"Error creating block: {ex.Message}";
        }
    }

    private Result<string, BlockBase> DuplicateBlock(BlockBase sourceBlock, int newOrder)
    {
        if (sourceBlock is null)
        {
            return "Source block cannot be null.";
        }

        var json = System.Text.Json.JsonSerializer.Serialize(sourceBlock, sourceBlock.GetType());
        var duplicate = System.Text.Json.JsonSerializer.Deserialize(json, sourceBlock.GetType()) as BlockBase;
        
        if (duplicate is null)
        {
            return "Failed to duplicate block.";
        }

        duplicate.Id = Snowflake.NewId();
        duplicate.Order = newOrder;
        
        return duplicate;
    }

    private static Type? GetBlockType(string blockTypeName)
    {
        var assembly = typeof(BlockBase).Assembly;
        return assembly.GetTypes()
            .FirstOrDefault(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(BlockBase)) && GetBlockTypeName(t) == blockTypeName);
    }

    private static string GetBlockTypeName(Type type)
    {
        // Check for JsonDerivedTypeAttribute which defines the block type name
        var jsonAttr = type.GetCustomAttribute<System.Text.Json.Serialization.JsonDerivedTypeAttribute>();
        if (jsonAttr is not null)
        {
            return jsonAttr.TypeDiscriminator?.ToString() ?? type.Name.ToLowerInvariant();
        }
        
        // Fallback: derive from class name
        return type.Name.ToLowerInvariant() switch
        {
            "richtextblock" => "rich_text",
            "headingblock" => "heading",
            "imageblock" => "image",
            "ctablock" => "cta",
            "quoteblock" => "quote",
            "embedblock" => "embed",
            _ => type.Name.ToLowerInvariant()
        };
    }
}
