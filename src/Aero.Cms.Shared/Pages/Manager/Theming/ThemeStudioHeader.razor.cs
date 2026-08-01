using Aero.Cms.Abstractions.Theming;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Aero.Cms.Shared.Pages.Manager.Theming;

public partial class ThemeStudioHeader
{
    [Parameter] public string SiteName { get; set; } = "Site";
    [Parameter] public IReadOnlyList<ThemeDefinitionView> Drafts { get; set; } = [];
    [Parameter] public long SelectedDraftId { get; set; }
    [Parameter] public string Name { get; set; } = string.Empty;
    [Parameter] public string Slug { get; set; } = string.Empty;
    [Parameter] public string? Description { get; set; }
    [Parameter] public long Revision { get; set; }
    [Parameter] public bool IsDirty { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public bool HasBlockingProblems { get; set; }
    [Parameter] public string BusyLabel { get; set; } = "Working";
    [Parameter] public EventCallback Back { get; set; }
    [Parameter] public EventCallback<long> DraftSelected { get; set; }
    [Parameter] public EventCallback<string> NameChanged { get; set; }
    [Parameter] public EventCallback<string> SlugChanged { get; set; }
    [Parameter] public EventCallback<string> DescriptionChanged { get; set; }
    [Parameter] public EventCallback Save { get; set; }
    [Parameter] public EventCallback Preview { get; set; }
    [Parameter] public EventCallback Publish { get; set; }
    [Parameter] public EventCallback Export { get; set; }
    [Parameter] public EventCallback<InputFileChangeEventArgs> Import { get; set; }

    private Task BackAsync() => Back.InvokeAsync();
    private Task SaveAsync() => Save.InvokeAsync();
    private Task PreviewAsync() => Preview.InvokeAsync();
    private Task PublishAsync() => Publish.InvokeAsync();
    private Task ExportAsync() => Export.InvokeAsync();
    private Task ImportAsync(InputFileChangeEventArgs args) => Import.InvokeAsync(args);
    private Task NameInputAsync(ChangeEventArgs args) => NameChanged.InvokeAsync(args.Value?.ToString() ?? string.Empty);
    private Task SlugInputAsync(ChangeEventArgs args) => SlugChanged.InvokeAsync(args.Value?.ToString() ?? string.Empty);
    private Task DescriptionInputAsync(ChangeEventArgs args) => DescriptionChanged.InvokeAsync(args.Value?.ToString() ?? string.Empty);
    private Task DraftSelectionChangedAsync(ChangeEventArgs args) => long.TryParse(args.Value?.ToString(), out var id) ? DraftSelected.InvokeAsync(id) : Task.CompletedTask;
}
