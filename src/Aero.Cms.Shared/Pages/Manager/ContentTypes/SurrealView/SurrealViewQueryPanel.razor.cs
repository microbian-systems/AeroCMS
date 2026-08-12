using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes.SurrealView;

/// <summary>Edits a query-backed view's shape, cache policy, and SELECT statement.</summary>
public partial class SurrealViewQueryPanel
{
    [Parameter] public IReadOnlyList<ContentViewShapeOption> Shapes { get; set; } = [];
    [Parameter] public string SelectedShapeAlias { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> SelectedShapeAliasChanged { get; set; }
    [Parameter] public string SelectStatement { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> SelectStatementChanged { get; set; }
    [Parameter] public string IdentityField { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> IdentityFieldChanged { get; set; }
    [Parameter] public string? TitleField { get; set; }
    [Parameter] public EventCallback<string?> TitleFieldChanged { get; set; }
    [Parameter] public string EntrySelectStatement { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> EntrySelectStatementChanged { get; set; }
    [Parameter] public string SearchSelectStatement { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> SearchSelectStatementChanged { get; set; }
    [Parameter] public IReadOnlyList<string> DiscoveredOutputFields { get; set; } = [];
    [Parameter] public bool CacheEnabled { get; set; } = true;
    [Parameter] public EventCallback<bool> CacheEnabledChanged { get; set; }
    [Parameter] public int CacheDurationSeconds { get; set; } = 300;
    [Parameter] public EventCallback<int> CacheDurationSecondsChanged { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public string EditorKey { get; set; } = "new";

    private string EditorId => $"content-surreal-view-{SanitizeId(EditorKey)}";

    private ContentViewShapeOption? SelectedShape => Shapes.FirstOrDefault(
        shape => string.Equals(shape.Alias, SelectedShapeAlias, StringComparison.Ordinal));

    private Task OnShapeChangedAsync(ChangeEventArgs args)
        => SelectedShapeAliasChanged.InvokeAsync(args.Value?.ToString() ?? string.Empty);

    private Task OnCacheEnabledChangedAsync(ChangeEventArgs args)
        => CacheEnabledChanged.InvokeAsync(args.Value is true || bool.TryParse(args.Value?.ToString(), out var enabled) && enabled);

    private Task OnCacheDurationChangedAsync(ChangeEventArgs args)
    {
        var duration = int.TryParse(args.Value?.ToString(), out var parsed) ? Math.Clamp(parsed, 1, 86_400) : 300;
        return CacheDurationSecondsChanged.InvokeAsync(duration);
    }

    private Task OnIdentityFieldChangedAsync(ChangeEventArgs args)
        => IdentityFieldChanged.InvokeAsync(args.Value?.ToString() ?? string.Empty);

    private Task OnTitleFieldChangedAsync(ChangeEventArgs args)
        => TitleFieldChanged.InvokeAsync(args.Value?.ToString());

    private IReadOnlyList<string> SelectableOutputFields => DiscoveredOutputFields
        .Intersect(SelectedShape?.Fields.Select(shapeField => shapeField.Name) ?? [], StringComparer.Ordinal)
        .Append(IdentityField)
        .Append(TitleField ?? string.Empty)
        .Where(outputField => !string.IsNullOrWhiteSpace(outputField))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(outputField => outputField, StringComparer.Ordinal)
        .ToArray();

    private static IReadOnlyList<ShapeFieldRow> Flatten(ContentViewShapeOption shape)
    {
        var rows = new List<ShapeFieldRow>();
        foreach (var field in shape.Fields)
        {
            AddField(rows, field, field.Name);
        }

        return rows;
    }

    private static void AddField(ICollection<ShapeFieldRow> rows, ContentShapeField field, string path)
    {
        rows.Add(new ShapeFieldRow(path, field.Type, field.Required));
        if (field.Fields is not null)
        {
            foreach (var nested in field.Fields)
            {
                AddField(rows, nested, $"{path}.{nested.Name}");
            }
        }

        if (field.Item is not null)
        {
            AddField(rows, field.Item, $"{path}[]");
        }
    }

    private static string SanitizeId(string value)
        => new(value.Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());

    private sealed record ShapeFieldRow(string Path, ContentShapeFieldType Type, bool Required);
}
