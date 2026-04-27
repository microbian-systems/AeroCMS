using Aero.Cms.Abstractions.Blocks;

namespace Aero.Cms.Web.Core.Pipelines;


using Aero.Cms.Core.Pipelines;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

public class PageReadContext : PipelineContext
{
    public required string Slug { get; init; }
    public required string Culture { get; init; }
    public long? TenantId { get; init; }
    public bool IncludeDraft { get; init; }
    public object? Page { get; set; } // TODO: Replace with Page model when defined
    public Dictionary<string, object> Metadata { get; } = new();
}

public class PageSaveContext : PipelineContext
{
    public required object Page { get; set; } // TODO: Replace with Page model when defined
    public required string Operation { get; init; } // TODO: Replace with enum
    public List<string> ValidationErrors { get; } = [];
    public bool HasValidationErrors => ValidationErrors.Count > 0;
}

public class BlockRenderContext : PipelineContext
{
    public required BlockBase Block { get; init; }
    public required ViewContext ViewContext { get; init; }
    public IHtmlContent? Output { get; set; }
    public Dictionary<string, object> RenderData { get; } = new();
}
