using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

public sealed partial class VirtualContentEntrySelector
{
    [Parameter] public string Provider { get; set; } = string.Empty;
    [Parameter] public IReadOnlyList<ContentEntryProviderOption> Providers { get; set; } = [];
    [Parameter] public bool IsProvidersLoading { get; set; }
    [Parameter] public string? SelectedStableId { get; set; }
    [Parameter] public IReadOnlyList<VirtualContentEntryOption> Entries { get; set; } = [];
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool HasSearched { get; set; }
    [Parameter] public string? ErrorMessage { get; set; }
    [Parameter] public EventCallback<string> ProviderChanged { get; set; }
    [Parameter] public EventCallback<string> SearchRequested { get; set; }
    [Parameter] public EventCallback<string?> SelectedStableIdChanged { get; set; }
    [Parameter] public EventCallback<string?> AddRequested { get; set; }
    [Parameter] public string? RouteParameterName { get; set; }
    private string PendingQuery { get; set; } = string.Empty;
    private bool BindToRoute { get; set; }
    private VirtualContentEntryOption? SelectedEntry => Entries.FirstOrDefault(x => x.StableId == SelectedStableId && x.Provider == Provider);
    private Task OnProviderChangedAsync(ChangeEventArgs args) => ProviderChanged.InvokeAsync(args.Value?.ToString()?.Trim() ?? string.Empty);
    private void OnQueryInput(ChangeEventArgs args) => PendingQuery = args.Value?.ToString() ?? string.Empty;
    private Task SearchAsync() => SearchRequested.InvokeAsync(PendingQuery);
    private Task OnEntryChangedAsync(ChangeEventArgs args) => SelectedStableIdChanged.InvokeAsync(args.Value?.ToString());
    private Task AddAsync() => AddRequested.InvokeAsync(BindToRoute ? RouteParameterName : null);
    private void OnRouteBindingChanged(ChangeEventArgs args)
        => BindToRoute = args.Value is bool value && value;

    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(Provider) || string.IsNullOrWhiteSpace(RouteParameterName))
            BindToRoute = false;
    }
}
