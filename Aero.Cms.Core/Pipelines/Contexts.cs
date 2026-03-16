using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aero.Cms.Core.Pipelines;

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
    public required object Block { get; init; } // TODO: Replace with BlockBase when defined
    public required ViewContext ViewContext { get; init; }
    public IHtmlContent? Output { get; set; }
    public Dictionary<string, object> RenderData { get; } = new();
}
