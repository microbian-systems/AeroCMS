using System.Text.Json;
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes.SurrealView;

/// <summary>Displays all bounded query-preview states and discovered output fields.</summary>
public partial class SurrealViewPreviewPanel
{
    [Parameter] public ContentViewPreviewResponse? Result { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool CanPreview { get; set; }
    [Parameter] public string? ErrorMessage { get; set; }
    [Parameter] public EventCallback PreviewRequested { get; set; }

    private static string FormatValue(IReadOnlyDictionary<string, JsonElement> row, string field)
    {
        if (!row.TryGetValue(field, out var value)) return "—";
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => "null",
            _ => value.GetRawText()
        };
    }
}
