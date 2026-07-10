using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Core.Blocks.Dynamic;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Represents a class for DynamicTemplateBlockRenderer.
/// </summary>
public partial class DynamicTemplateBlockRenderer
{
    private string? renderedHtml;
    private string? errorMessage;

        /// <summary>
    /// Gets or sets the Block.
    /// </summary>
[Parameter]
    public DynamicTemplateBlock? Block { get; set; }

        /// <summary>
    /// Gets or sets the Definition Service.
    /// </summary>
[Inject]
    public IDynamicBlockDefinitionService DefinitionService { get; set; } = default!;

        /// <summary>
    /// Gets or sets the Scriban Renderer.
    /// </summary>
[Inject]
    public ISecureScribanRenderer ScribanRenderer { get; set; } = default!;

        /// <summary>
    /// OnParametersSetAsync method.
    /// </summary>
protected override async Task OnParametersSetAsync()
    {
        renderedHtml = null;
        errorMessage = null;

        if (Block is null)
        {
            errorMessage = "Dynamic template block is missing.";
            return;
        }

        var definitionResult = await GetDefinitionAsync(Block);
        if (definitionResult is Result<DynamicBlockDefinition, AeroError>.Failure definitionFailure)
        {
            errorMessage = definitionFailure.Error.ToString();
            return;
        }

        var definition = ((Result<DynamicBlockDefinition, AeroError>.Ok)definitionResult).Value;
        var renderResult = await ScribanRenderer.RenderAsync(definition, Block.Data);
        if (renderResult is Result<string, AeroError>.Failure renderFailure)
        {
            errorMessage = renderFailure.Error.ToString();
            return;
        }

        renderedHtml = ((Result<string, AeroError>.Ok)renderResult).Value;
    }

    private Task<Result<DynamicBlockDefinition, AeroError>> GetDefinitionAsync(DynamicTemplateBlock block)
    {
        if (!string.IsNullOrWhiteSpace(block.InlineTemplate))
        {
            var definition = new DynamicBlockDefinition
            {
                Id = block.DefinitionId,
                Version = block.DefinitionVersion,
                IsPublished = true,
                ScribanTemplate = block.InlineTemplate
            };

            return Task.FromResult<Result<DynamicBlockDefinition, AeroError>>(definition);
        }

        return DefinitionService.GetAsync(block.DefinitionId, block.DefinitionVersion);
    }
}
