using Aero.Cms.Abstractions.Blocks;
using System.Globalization;

namespace Aero.Cms.Web.Core.Pipelines;


using Aero.Cms.Core.Pipelines;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

/// <summary>
/// Represents a class for PageReadContext.
/// </summary>
public class PageReadContext : PipelineContext
{
        /// <summary>
    /// Gets or sets the Slug.
    /// </summary>
public required string Slug { get; init; }
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
public required string Culture { get; init; }
        /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
public long? TenantId { get; init; }
        /// <summary>
    /// Gets or sets the Include Draft.
    /// </summary>
public bool IncludeDraft { get; init; }
        /// <summary>
    /// Gets or sets the Page.
    /// </summary>
public object? Page { get; set; } // TODO: Replace with Page model when defined
        /// <summary>
    /// Gets or sets the Metadata.
    /// </summary>
public Dictionary<string, object> Metadata { get; } = new();
}

/// <summary>
/// Represents a class for PageSaveContext.
/// </summary>
public class PageSaveContext : PipelineContext
{
        /// <summary>
    /// Gets or sets the Page.
    /// </summary>
public required object Page { get; set; } // TODO: Replace with Page model when defined
        /// <summary>
    /// Gets or sets the Operation.
    /// </summary>
public required string Operation { get; init; } // TODO: Replace with enum
        /// <summary>
    /// Gets or sets the Validation Errors.
    /// </summary>
public List<string> ValidationErrors { get; } = [];
        /// <summary>
    /// Gets or sets the Has Validation Errors.
    /// </summary>
public bool HasValidationErrors => ValidationErrors.Count > 0;
}

/// <summary>
/// Represents a class for BlockRenderContext.
/// </summary>
public class BlockRenderContext : PipelineContext
{
        /// <summary>
    /// Gets or sets the Block.
    /// </summary>
public required BlockBase Block { get; init; }
        /// <summary>
    /// Gets or sets the View Context.
    /// </summary>
public required ViewContext ViewContext { get; init; }
        /// <summary>
    /// Gets or sets the Culture.
    /// </summary>
public CultureInfo Culture { get; init; } = CultureInfo.CurrentCulture;
        /// <summary>
    /// Gets or sets the Output.
    /// </summary>
public IHtmlContent? Output { get; set; }
        /// <summary>
    /// Gets or sets the Render Data.
    /// </summary>
public Dictionary<string, object> RenderData { get; } = new();
}
