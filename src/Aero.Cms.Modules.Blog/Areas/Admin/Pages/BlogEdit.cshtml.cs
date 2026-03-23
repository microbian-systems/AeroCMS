using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Blocks.Editing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Blog.Areas.Admin.Pages;

public class EditModel(IBlogPostContentService blogService, BlockEditingService blockService)
    : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long? Id { get; set; }

    [BindProperty]
    public BlogPostDocument? BlogPost { get; set; }

    public bool IsNew { get; private set; }

    public string? ErrorMessage { get; private set; }

    // Block editing state
    public BlockBase? SelectedBlock { get; set; }

    public bool ShowBlockPicker { get; set; }

    public bool ShowBlockEditor { get; set; }

    // Handler for when a block type is selected from the picker
    public IActionResult OnPostAddBlockAsync(string blockTypeName)
    {
        if (BlogPost is null || string.IsNullOrEmpty(blockTypeName))
        {
            return Page();
        }

        var result = blockService.CreateBlock(blockTypeName, BlogPost.Content.Count);
        if (result is Result<string, BlockBase>.Ok(var block))
        {
            BlogPost.Content.Add(block);
            SelectedBlock = block;
            ShowBlockPicker = false;
            ShowBlockEditor = true;
        }
        else if (result is Result<string, BlockBase>.Failure(var error))
        {
            ErrorMessage = error;
        }

        return Page();
    }

    // Handler for saving a block's changes
    public IActionResult OnPostSaveBlockAsync()
    {
        if (SelectedBlock is not null)
        {
            // Block is already updated via binding, just close the editor
            ShowBlockEditor = false;
            SelectedBlock = null;
        }

        return Page();
    }

    // Handler for deleting a block
    public IActionResult OnPostDeleteBlockAsync(long blockId)
    {
        if (BlogPost is null)
        {
            return Page();
        }

        var block = BlogPost.Content.FirstOrDefault(b => b.Id == blockId);
        if (block is not null)
        {
            BlogPost.Content.Remove(block);
            blockService.ReorderBlocks(BlogPost.Content);
        }

        ShowBlockEditor = false;
        SelectedBlock = null;

        return Page();
    }

    // Handler for duplicating a block
    public IActionResult OnPostDuplicateBlockAsync(long blockId)
    {
        if (BlogPost is null)
        {
            return Page();
        }

        var sourceBlock = BlogPost.Content.FirstOrDefault(b => b.Id == blockId);
        if (sourceBlock is not null)
        {
            var result = blockService.DuplicateBlock(sourceBlock, BlogPost.Content.Count);
            if (result is Result<string, BlockBase>.Ok(var duplicate))
            {
                BlogPost.Content.Add(duplicate);
            }
        }

        ShowBlockEditor = false;
        SelectedBlock = null;

        return Page();
    }

    // Handler for moving a block up
    public IActionResult OnPostMoveBlockUpAsync(long blockId)
    {
        if (BlogPost is null)
        {
            return Page();
        }

        var block = BlogPost.Content.FirstOrDefault(b => b.Id == blockId);
        if (block is not null)
        {
            blockService.MoveBlockUp(block);
            blockService.ReorderBlocks(BlogPost.Content);
        }

        return Page();
    }

    // Handler for moving a block down
    public IActionResult OnPostMoveBlockDownAsync(long blockId)
    {
        if (BlogPost is null)
        {
            return Page();
        }

        var block = BlogPost.Content.FirstOrDefault(b => b.Id == blockId);
        if (block is not null)
        {
            blockService.MoveBlockDown(block);
            blockService.ReorderBlocks(BlogPost.Content);
        }

        return Page();
    }

    // Show the block picker
    public IActionResult OnGetShowBlockPicker()
    {
        ShowBlockPicker = true;
        ShowBlockEditor = false;
        return Page();
    }

    // Edit a specific block
    public IActionResult OnGetEditBlock(long blockId)
    {
        if (BlogPost is null)
        {
            return Page();
        }

        SelectedBlock = BlogPost.Content.FirstOrDefault(b => b.Id == blockId);
        ShowBlockEditor = SelectedBlock is not null;
        ShowBlockPicker = false;

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

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        if (Id.HasValue)
        {
            var result = await blogService.LoadAsync(Id.Value, cancellationToken);
            BlogPost = result.Match(
                post => post,
                _ => (BlogPostDocument?)null
            );

            if (BlogPost is null)
            {
                return NotFound();
            }

            IsNew = false;
        }
        else
        {
            // Create new blog post
            BlogPost = new BlogPostDocument();
            IsNew = true;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (BlogPost is null)
        {
            ModelState.AddModelError(string.Empty, "Blog post data is required");
            return Page();
        }

        var result = await blogService.SaveAsync(BlogPost, cancellationToken);

        if (((result is Result<string, BlogPostDocument>.Ok)))
        {
            return RedirectToPage("/Admin/Index");
        }

        if (result is Result<string, BlogPostDocument>.Failure(var error))
        {
            ErrorMessage = error;
        }
        else
        {
            ErrorMessage = "Unknown error occurred";
        }

        return Page();
    }
}