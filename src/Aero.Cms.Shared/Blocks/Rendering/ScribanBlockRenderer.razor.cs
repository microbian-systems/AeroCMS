using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Core.Blocks.Dynamic;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Blocks.Rendering;

public partial class ScribanBlockRenderer
{
    private string? RenderedHtml;
    private string? ErrorMessage;

    [Parameter]
    public ScribanBlock? Block { get; set; }

    [Inject]
    public ISecureScribanRenderer ScribanRenderer { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        RenderedHtml = null;
        ErrorMessage = null;

        if (Block is null || string.IsNullOrWhiteSpace(Block.Template))
            return;

        var definition = new DynamicBlockDefinition
        {
            Id = Block.Id,
            Version = 1,
            IsPublished = true,
            ScribanTemplate = Block.Template
        };

        var renderResult = await ScribanRenderer.RenderAsync(definition, Block.Data);
        if (renderResult is Result<string, AeroError>.Failure failure)
        {
            ErrorMessage = failure.Error.ToString();
            return;
        }

        RenderedHtml = ((Result<string, AeroError>.Ok)renderResult).Value;
    }
}
