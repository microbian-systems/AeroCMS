using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Services;
using System.Text.Json;

namespace Aero.Cms.Core.Content.Views;

/// <summary>Resolves published query views as provider-qualified virtual entries.</summary>
public sealed class ContentSurrealViewEntryProviderCatalog(
    IContentSurrealViewStore store,
    IContentSurrealViewService service) : IContentEntrySourceProviderCatalog
{
    public async Task<IReadOnlyList<string>> ListProviderKeysAsync(ContentViewScope scope, CancellationToken ct = default)
    {
        if (!scope.IsValid) return [];
        return (await store.ListPublishedAsync(scope, ct))
            .Where(view => view.HasEntryIdentity && view.EntrySelectStatement is not null && view.SearchSelectStatement is not null)
            .Select(view => $"view:{view.Alias}").OrderBy(key => key, StringComparer.Ordinal).ToArray();
    }

    public async Task<IContentEntrySourceProvider?> ResolveAsync(ContentViewScope scope, string provider, CancellationToken ct = default)
    {
        if (!scope.IsValid || !provider.StartsWith("view:", StringComparison.Ordinal) || provider.Length == "view:".Length) return null;
        var alias = provider["view:".Length..];
        var view = await store.LoadAsync(scope, alias, ContentViewPublicationState.Published, ct);
        return view is { HasEntryIdentity: true, EntrySelectStatement: not null, SearchSelectStatement: not null }
            ? new ContentSurrealViewEntryProvider(view, service)
            : null;
    }
}

/// <summary>Executes a published view's dedicated exact-entry and search statements only.</summary>
public sealed class ContentSurrealViewEntryProvider(ContentSurrealViewRevision view, IContentSurrealViewService service) : IContentEntrySourceProvider
{
    public string Provider => $"view:{view.Alias}";

    public async Task<ContentEntry?> FindAsync(ContentViewScope scope, string stableId, CancellationToken ct = default)
    {
        if (scope != view.Scope || string.IsNullOrWhiteSpace(stableId) || stableId.Length > 256) return null;
        var result = await service.ExecuteEntryAsync(view, scope, stableId, ct);
        if (result?.Rows.Count != 1 || Map(result.Rows[0]) is not { } entry)
            return null;
        return string.Equals(entry.Key.StableId, stableId, StringComparison.Ordinal)
            ? entry
            : null;
    }

    public async Task<IReadOnlyList<ContentEntry>> SearchAsync(ContentViewScope scope, string? culture, string? query, int take, CancellationToken ct = default)
    {
        if (scope != view.Scope || take <= 0) return [];
        var result = string.IsNullOrWhiteSpace(query)
            ? await service.ExecutePublicAsync(scope, view.Alias, new Dictionary<string, object?>(), take, ct)
            : await service.SearchEntriesAsync(view, scope, query, take, ct);
        if (result is null) return [];
        var mapped = result.Rows.Select(Map).ToArray();
        if (mapped.Any(entry => entry is null)) return [];
        var entries = mapped.Cast<ContentEntry>().ToArray();
        return entries.Select(entry => entry.Key.StableId).Distinct(StringComparer.Ordinal).Count() == entries.Length
            ? entries
            : [];
    }

    private ContentEntry? Map(IReadOnlyDictionary<string, object?> row)
    {
        if (!row.TryGetValue(view.IdentityField, out var id) || !TryReadText(id, out var stableId)
            || string.IsNullOrWhiteSpace(stableId) || stableId.Length > 256) return null;
        var values = new Dictionary<string, object?>(row, StringComparer.Ordinal);
        values[view.IdentityField] = stableId;
        if (!values.ContainsKey("title") && !string.IsNullOrWhiteSpace(view.TitleField)
            && row.TryGetValue(view.TitleField, out var title))
            values["title"] = TryReadText(title, out var displayTitle) ? displayTitle : title;
        return new ContentEntry(new ContentEntryKey(Provider, stableId), view.Scope, values);
    }

    private static bool TryReadText(object? value, out string text)
    {
        text = value switch
        {
            string scalar => scalar,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString() ?? string.Empty,
            _ => string.Empty
        };
        return value is string or JsonElement { ValueKind: JsonValueKind.String };
    }
}
