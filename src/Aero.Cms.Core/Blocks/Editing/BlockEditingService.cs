using System.Reflection;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Blocks.Editing;

/// <summary>
/// Service for block editing operations, providing methods to create, update, delete, and manage CMS blocks.
/// </summary>
public sealed class BlockEditingService
{
    private static readonly List<BlockTypeInfo> BlockTypes;

    static BlockEditingService()
    {
        BlockTypes = ScanBlockTypes();
    }

    /// <summary>
    /// Gets all available block type information for the block picker UI.
    /// </summary>
    /// <returns>A collection of block type information.</returns>
    public IEnumerable<BlockTypeInfo> GetAvailableBlockTypes()
    {
        return BlockTypes.OrderBy(b => b.SortOrder).ThenBy(b => b.DisplayName);
    }

    /// <summary>
    /// Gets block type information by the block type name.
    /// </summary>
    /// <param name="blockTypeName">The name of the block type.</param>
    /// <returns>An Option containing the block type info if found.</returns>
    public Option<BlockTypeInfo> GetBlockTypeInfo(string blockTypeName)
    {
        var info = BlockTypes.FirstOrDefault(b => b.Name == blockTypeName);
        return info is not null ? info : new Option<BlockTypeInfo>.None();
    }

    /// <summary>
    /// Creates a new block instance of the specified type.
    /// </summary>
    /// <param name="blockTypeName">The name of the block type to create.</param>
    /// <param name="order">The display order for the new block.</param>
    /// <returns>A Result containing the created block or an error message.</returns>
    public Result<string, BlockBase> CreateBlock(string blockTypeName, int order = 0)
    {
        var blockTypeInfo = GetBlockTypeInfo(blockTypeName);
        
        return blockTypeInfo switch
        {
            Option<BlockTypeInfo>.Some(var info) => CreateBlockInstance(info.Type, order),
            _ => $"Block type '{blockTypeName}' not found."
        };
    }

    /// <summary>
    /// Creates a copy of an existing block with a new ID.
    /// </summary>
    /// <param name="sourceBlock">The block to duplicate.</param>
    /// <param name="newOrder">The display order for the new block.</param>
    /// <returns>A Result containing the duplicated block or an error message.</returns>
    public Result<string, BlockBase> DuplicateBlock(BlockBase sourceBlock, int newOrder)
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

    /// <summary>
    /// Updates the order of a block within its container.
    /// </summary>
    /// <param name="block">The block to update.</param>
    /// <param name="newOrder">The new order value.</param>
    public void UpdateBlockOrder(BlockBase block, int newOrder)
    {
        if (block is not null)
        {
            block.Order = newOrder;
        }
    }

    /// <summary>
    /// Moves a block up in the order (decreases order value).
    /// </summary>
    /// <param name="block">The block to move.</param>
    public void MoveBlockUp(BlockBase block)
    {
        if (block is not null && block.Order > 0)
        {
            block.Order--;
        }
    }

    /// <summary>
    /// Moves a block down in the order (increases order value).
    /// </summary>
    /// <param name="block">The block to move.</param>
    public void MoveBlockDown(BlockBase block)
    {
        if (block is not null)
        {
            block.Order++;
        }
    }

    /// <summary>
    /// Validates a block's properties.
    /// </summary>
    /// <param name="block">The block to validate.</param>
    /// <returns>A Result indicating success or containing validation errors.</returns>
    public Result<string[], bool> ValidateBlock(BlockBase block)
    {
        if (block is null)
        {
            return new[] { "Block cannot be null." };
        }

        var errors = new List<string>();

        // Validate based on block type
        switch (block)
        {
            case RichTextBlock richText:
                if (string.IsNullOrWhiteSpace(richText.Content))
                {
                    errors.Add("Rich text content cannot be empty.");
                }
                break;

            case HeadingBlock heading:
                if (string.IsNullOrWhiteSpace(heading.Text))
                {
                    errors.Add("Heading text cannot be empty.");
                }
                if (heading.Level is < 1 or > 6)
                {
                    errors.Add("Heading level must be between 1 and 6.");
                }
                break;

            case ImageBlock image:
                if (image.MediaId <= 0)
                {
                    errors.Add("Please select an image.");
                }
                break;

            case CtaBlock cta:
                if (string.IsNullOrWhiteSpace(cta.Text))
                {
                    errors.Add("Call-to-action text cannot be empty.");
                }
                if (string.IsNullOrWhiteSpace(cta.Url))
                {
                    errors.Add("Call-to-action URL cannot be empty.");
                }
                break;

            case QuoteBlock quote:
                if (string.IsNullOrWhiteSpace(quote.Content))
                {
                    errors.Add("Quote content cannot be empty.");
                }
                break;

            case EmbedBlock embed:
                if (string.IsNullOrWhiteSpace(embed.EmbedType))
                {
                    errors.Add("Embed type must be specified.");
                }
                if (string.IsNullOrWhiteSpace(embed.SourceUrl))
                {
                    errors.Add("Source URL cannot be empty.");
                }
                break;
        }

        return errors.Count == 0 ? true : errors.ToArray();
    }

    /// <summary>
    /// Gets the default property values for a block type as a dictionary.
    /// Used to initialize the block editor form.
    /// </summary>
    /// <param name="blockTypeName">The name of the block type.</param>
    /// <returns>A dictionary of property names and their default values.</returns>
    public Dictionary<string, object> GetDefaultProperties(string blockTypeName)
    {
        return blockTypeName switch
        {
            "rich_text" => new Dictionary<string, object> { ["Content"] = string.Empty },
            "heading" => new Dictionary<string, object> { ["Level"] = 1, ["Text"] = string.Empty },
            "image" => new Dictionary<string, object> { ["MediaId"] = 0L, ["AltText"] = string.Empty, ["Caption"] = string.Empty },
            "cta" => new Dictionary<string, object> { ["Text"] = string.Empty, ["Url"] = string.Empty, ["Style"] = string.Empty },
            "quote" => new Dictionary<string, object> { ["Content"] = string.Empty, ["Author"] = string.Empty, ["Citation"] = string.Empty },
            "embed" => new Dictionary<string, object> { ["EmbedType"] = string.Empty, ["SourceUrl"] = string.Empty, ["ThumbnailUrl"] = string.Empty },
            _ => new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Gets the properties of a block instance as a dictionary.
    /// </summary>
    /// <param name="block">The block instance.</param>
    /// <returns>A dictionary of property names and their values.</returns>
    public Dictionary<string, object> GetBlockProperties(BlockBase block)
    {
        if (block is null)
        {
            return new Dictionary<string, object>();
        }

        return block switch
        {
            RichTextBlock richText => new Dictionary<string, object> { ["Content"] = richText.Content },
            HeadingBlock heading => new Dictionary<string, object> { ["Level"] = heading.Level, ["Text"] = heading.Text },
            ImageBlock image => new Dictionary<string, object> { ["MediaId"] = image.MediaId, ["AltText"] = image.AltText ?? string.Empty, ["Caption"] = image.Caption ?? string.Empty },
            CtaBlock cta => new Dictionary<string, object> { ["Text"] = cta.Text, ["Url"] = cta.Url, ["Style"] = cta.Style ?? string.Empty },
            QuoteBlock quote => new Dictionary<string, object> { ["Content"] = quote.Content, ["Author"] = quote.Author ?? string.Empty, ["Citation"] = quote.Citation ?? string.Empty },
            EmbedBlock embed => new Dictionary<string, object> { ["EmbedType"] = embed.EmbedType, ["SourceUrl"] = embed.SourceUrl, ["ThumbnailUrl"] = embed.ThumbnailUrl ?? string.Empty },
            _ => new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Applies property values to a block instance.
    /// </summary>
    /// <param name="block">The block to update.</param>
    /// <param name="properties">The properties to apply.</param>
    public void ApplyBlockProperties(BlockBase block, Dictionary<string, object> properties)
    {
        if (block is null || properties is null)
        {
            return;
        }

        switch (block)
        {
            case RichTextBlock richText:
                if (properties.TryGetValue("Content", out var content))
                    richText.Content = content?.ToString() ?? string.Empty;
                break;

            case HeadingBlock heading:
                if (properties.TryGetValue("Level", out var level))
                    heading.Level = Convert.ToInt32(level);
                if (properties.TryGetValue("Text", out var text))
                    heading.Text = text?.ToString() ?? string.Empty;
                break;

            case ImageBlock image:
                if (properties.TryGetValue("MediaId", out var mediaId))
                    image.MediaId = Convert.ToInt64(mediaId);
                if (properties.TryGetValue("AltText", out var altText))
                    image.AltText = altText?.ToString();
                if (properties.TryGetValue("Caption", out var caption))
                    image.Caption = caption?.ToString();
                break;

            case CtaBlock cta:
                if (properties.TryGetValue("Text", out var ctaText))
                    cta.Text = ctaText?.ToString() ?? string.Empty;
                if (properties.TryGetValue("Url", out var url))
                    cta.Url = url?.ToString() ?? string.Empty;
                if (properties.TryGetValue("Style", out var style))
                    cta.Style = style?.ToString();
                break;

            case QuoteBlock quote:
                if (properties.TryGetValue("Content", out var quoteContent))
                    quote.Content = quoteContent?.ToString() ?? string.Empty;
                if (properties.TryGetValue("Author", out var author))
                    quote.Author = author?.ToString();
                if (properties.TryGetValue("Citation", out var citation))
                    quote.Citation = citation?.ToString();
                break;

            case EmbedBlock embed:
                if (properties.TryGetValue("EmbedType", out var embedType))
                    embed.EmbedType = embedType?.ToString() ?? string.Empty;
                if (properties.TryGetValue("SourceUrl", out var sourceUrl))
                    embed.SourceUrl = sourceUrl?.ToString() ?? string.Empty;
                if (properties.TryGetValue("ThumbnailUrl", out var thumbnailUrl))
                    embed.ThumbnailUrl = thumbnailUrl?.ToString();
                break;
        }
    }

    /// <summary>
    /// Reorders a collection of blocks to ensure sequential ordering without gaps.
    /// </summary>
    /// <param name="blocks">The blocks to reorder.</param>
    public void ReorderBlocks(IEnumerable<BlockBase> blocks)
    {
        var orderedBlocks = blocks?.OrderBy(b => b.Order).ToList();
        if (orderedBlocks is null)
        {
            return;
        }

        for (int i = 0; i < orderedBlocks.Count; i++)
        {
            orderedBlocks[i].Order = i;
        }
    }

    private static List<BlockTypeInfo> ScanBlockTypes()
    {
        var types = new List<BlockTypeInfo>();
        var assembly = typeof(BlockBase).Assembly;

        var blockTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(BlockBase)));

        foreach (var type in blockTypes)
        {
            var metadata = type.GetCustomAttribute<BlockMetadataAttribute>();
            if (metadata is not null)
            {
                types.Add(new BlockTypeInfo
                {
                    Name = metadata.Name,
                    DisplayName = metadata.DisplayName,
                    Description = metadata.Description,
                    Category = metadata.Category ?? "General",
                    Icon = metadata.Icon,
                    SortOrder = metadata.SortOrder,
                    Type = type
                });
            }
        }

        return types;
    }

    private static Result<string, BlockBase> CreateBlockInstance(Type blockType, int order)
    {
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
}

/// <summary>
/// Information about a registered block type for UI display.
/// </summary>
public sealed class BlockTypeInfo
{
    /// <summary>
    /// Gets the unique name of the block type.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display name of the block type.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the description of the block type.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the category of the block type.
    /// </summary>
    public string Category { get; init; } = "General";

    /// <summary>
    /// Gets the icon identifier for the block type.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Gets the sort order for the block type in UI listings.
    /// </summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// Gets the Type of the block.
    /// </summary>
    public Type Type { get; init; } = typeof(BlockBase);
}
