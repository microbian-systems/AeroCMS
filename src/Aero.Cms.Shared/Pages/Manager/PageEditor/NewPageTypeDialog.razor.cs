using Aero.Cms.Abstractions.Pages.Rendering;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

/// <summary>Selects the immutable renderer for a new page before editing begins.</summary>
public partial class NewPageTypeDialog
{
    private string _selectedRendererId = PageRendererIds.AeroComposition;

    [Inject]
    private DialogService DialogService { get; set; } = default!;

    /// <summary>Gets or sets the executable renderers advertised by the server.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<PageRendererDescriptor> Renderers { get; set; } = [];

    /// <summary>Gets or sets the renderer initially selected by the owner.</summary>
    [Parameter]
    public string? SelectedRendererId { get; set; }

    private PageRendererDescriptor? SelectedRenderer => Renderers.FirstOrDefault(
        renderer => string.Equals(renderer.Id, _selectedRendererId, StringComparison.Ordinal));

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _selectedRendererId = Renderers.Any(renderer =>
                string.Equals(renderer.Id, SelectedRendererId, StringComparison.Ordinal))
            ? SelectedRendererId!
            : Renderers.FirstOrDefault()?.Id ?? PageRendererIds.AeroComposition;
    }

    private void Confirm()
    {
        if (SelectedRenderer is not null)
        {
            DialogService.Close(new NewPageTypeDialogResult(SelectedRenderer.Id));
        }
    }

    private void Cancel() => DialogService.Close(null);

    private static string GetDisplayName(PageRendererDescriptor renderer) => renderer.Id switch
    {
        PageRendererIds.AeroComposition => "Aero page",
        PageRendererIds.Scriban => "Scriban template",
        PageRendererIds.SharpTs => "TypeScript page",
        PageRendererIds.Htmx => "HTMX page",
        _ => renderer.DisplayName
    };

    private static string GetDescription(PageRendererDescriptor renderer) => renderer.Id switch
    {
        PageRendererIds.AeroComposition => "Build visually with layouts, elements, and rendered fragments.",
        PageRendererIds.Scriban => "Write a full-page Scriban template in the code editor.",
        PageRendererIds.SharpTs => "Write a full-page SharpTS program in the code editor.",
        PageRendererIds.Htmx => "Write full-page HTML with HTMX behavior in the code editor.",
        _ => $"Build this page with the {renderer.DisplayName} renderer."
    };

    private static string GetEditorName(PageRendererDescriptor renderer)
        => string.Equals(renderer.EditorKind, PageEditorKinds.Source, StringComparison.Ordinal)
            ? "Code editor"
            : "Visual builder";
}

/// <summary>Result returned after a new page renderer is explicitly selected.</summary>
public sealed record NewPageTypeDialogResult(string RendererId);
