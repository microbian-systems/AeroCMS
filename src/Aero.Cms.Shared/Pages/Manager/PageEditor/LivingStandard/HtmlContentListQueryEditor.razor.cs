using System.ComponentModel.DataAnnotations;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Pages.Composition;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Edits one selected content-list scope without owning the PageEditor document state.
/// </summary>
public partial class HtmlContentListQueryEditor
{
    private PageContentListScope? _loadedScope;
    private string? _localError;

    [Parameter, EditorRequired]
    public PageContentListScope Scope { get; set; } = new();

    [Parameter]
    public IReadOnlyList<ContentFieldDefinition> Fields { get; set; } = [];

    [Parameter]
    public string? ErrorMessage { get; set; }

    [Parameter]
    public EventCallback<HtmlContentListSettingsRequest> SettingsChanged { get; set; }

    protected QueryForm Form { get; private set; } = new();

    protected bool IsSubmitting { get; private set; }

    protected bool IsVirtualProvider => !string.IsNullOrWhiteSpace(Scope.ContentEntryProvider);

    protected string VirtualSearchText { get; set; } = string.Empty;

    protected bool HasUnsupportedVirtualSettings { get; private set; }

    protected string? DisplayError => _localError ?? ErrorMessage;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_loadedScope, Scope))
        {
            return;
        }

        _loadedScope = Scope;
        _localError = null;
        Form = QueryForm.FromScope(Scope);
        if (IsVirtualProvider)
        {
            var filters = Scope.Query?.Filters ?? [];
            HasUnsupportedVirtualSettings = !string.IsNullOrWhiteSpace(Scope.Query?.SortField)
                || filters.Count > 1
                || filters.Any(filter => filter.Operator != PageContentFilterOperator.Contains
                    || !string.Equals(filter.FieldName, "$search", StringComparison.Ordinal));
            VirtualSearchText = HasUnsupportedVirtualSettings
                ? string.Empty
                : filters.SingleOrDefault()?.Value ?? string.Empty;
        }
        else
        {
            HasUnsupportedVirtualSettings = false;
            VirtualSearchText = string.Empty;
        }
    }

    protected void AddFilter()
    {
        _localError = null;
        if (Fields.Count == 0)
        {
            _localError = "Content-type fields are not available for this scope.";
            return;
        }

        if (Form.Filters.Count >= PageContentListQuery.MaximumFilterCount)
        {
            _localError = $"A content list can contain at most {PageContentListQuery.MaximumFilterCount} filters.";
            return;
        }

        Form.Filters.Add(new FilterForm
        {
            FieldName = Fields[0].Name,
            Operator = PageContentFilterOperator.Equals
        });
    }

    protected void RemoveFilter(long key)
    {
        var filter = Form.Filters.FirstOrDefault(candidate => candidate.Key == key);
        if (filter is not null)
        {
            Form.Filters.Remove(filter);
        }
    }

    protected async Task SaveAsync()
    {
        if (HasUnsupportedVirtualSettings)
        {
            _localError = "Clear unsupported provider-backed list settings before applying changes.";
            return;
        }

        _localError = ValidateFilters();
        if (_localError is not null)
        {
            return;
        }

        IsSubmitting = true;
        try
        {
            await SettingsChanged.InvokeAsync(new HtmlContentListSettingsRequest
            {
                ScopeNodeId = Scope.NodeId,
                Query = IsVirtualProvider
                    ? new PageContentListQuery
                    {
                        PageSize = Form.PageSize,
                        Filters = string.IsNullOrWhiteSpace(VirtualSearchText)
                            ? []
                            :
                            [
                                new PageContentFilter
                                {
                                    FieldName = "$search",
                                    Operator = PageContentFilterOperator.Contains,
                                    Value = VirtualSearchText.Trim()
                                }
                            ]
                    }
                    : new PageContentListQuery
                    {
                        PageSize = Form.PageSize,
                        SortField = NullIfWhiteSpace(Form.SortField),
                        SortDirection = Form.SortDirection,
                        Filters = Form.Filters.Select(filter => new PageContentFilter
                        {
                            FieldName = filter.FieldName,
                            Operator = filter.Operator,
                            Value = FilterRequiresValue(filter.Operator)
                                ? NullIfWhiteSpace(filter.Value)
                                : null
                        }).ToArray()
                    },
                EmptyState = Form.EmptyState
            });
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    protected static string FieldDisplayName(ContentFieldDefinition field) =>
        string.IsNullOrWhiteSpace(field.Label) ? field.Name : field.Label;

    protected static bool FilterRequiresValue(PageContentFilterOperator filterOperator) =>
        filterOperator is not PageContentFilterOperator.IsEmpty
            and not PageContentFilterOperator.IsNotEmpty;

    protected static string FilterOperatorLabel(PageContentFilterOperator filterOperator) =>
        filterOperator switch
        {
            PageContentFilterOperator.NotEquals => "Does not equal",
            PageContentFilterOperator.StartsWith => "Starts with",
            PageContentFilterOperator.EndsWith => "Ends with",
            PageContentFilterOperator.GreaterThan => "Greater than",
            PageContentFilterOperator.GreaterThanOrEqual => "Greater than or equal",
            PageContentFilterOperator.LessThan => "Less than",
            PageContentFilterOperator.LessThanOrEqual => "Less than or equal",
            PageContentFilterOperator.IsEmpty => "Is empty",
            PageContentFilterOperator.IsNotEmpty => "Is not empty",
            _ => filterOperator.ToString()
        };

    private string? ValidateFilters()
    {
        if (IsVirtualProvider)
        {
            return VirtualSearchText.Length > 256
                ? "Provider-backed list search text cannot exceed 256 characters."
                : null;
        }

        if (Form.Filters.Count > PageContentListQuery.MaximumFilterCount)
        {
            return $"A content list can contain at most {PageContentListQuery.MaximumFilterCount} filters.";
        }

        var knownFields = Fields.Select(field => field.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var filter in Form.Filters)
        {
            if (!knownFields.Contains(filter.FieldName))
            {
                return "Every filter must select a field from this content type.";
            }

            if (FilterRequiresValue(filter.Operator) && string.IsNullOrWhiteSpace(filter.Value))
            {
                return $"The {FieldLabel(filter.FieldName)} filter requires a comparison value.";
            }
        }

        return null;
    }

    private string FieldLabel(string fieldName) => Fields
        .Where(field => string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase))
        .Select(FieldDisplayName)
        .FirstOrDefault() ?? fieldName;

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    protected void ResetUnsupportedVirtualSettings()
    {
        HasUnsupportedVirtualSettings = false;
        Form.SortField = null;
        Form.SortDirection = PageContentSortDirection.Ascending;
        Form.Filters.Clear();
        VirtualSearchText = string.Empty;
        _localError = null;
    }

    protected sealed class QueryForm
    {
        [Range(PageContentListQuery.MinimumPageSize, PageContentListQuery.MaximumPageSize)]
        public int PageSize { get; set; } = 10;

        public string? SortField { get; set; }

        public PageContentSortDirection SortDirection { get; set; }

        public PageContentEmptyStateBehavior EmptyState { get; set; }

        public List<FilterForm> Filters { get; set; } = [];

        public static QueryForm FromScope(PageContentListScope scope) => new()
        {
            PageSize = scope.Query?.PageSize ?? 10,
            SortField = scope.Query?.SortField,
            SortDirection = scope.Query?.SortDirection ?? PageContentSortDirection.Ascending,
            EmptyState = scope.EmptyState,
            Filters = (scope.Query?.Filters ?? []).Select(filter => new FilterForm
            {
                FieldName = filter.FieldName,
                Operator = filter.Operator,
                Value = filter.Value
            }).ToList()
        };
    }

    protected sealed class FilterForm
    {
        public long Key { get; } = Interlocked.Increment(ref _nextKey);

        public string FieldName { get; set; } = string.Empty;

        public PageContentFilterOperator Operator { get; set; }

        public string? Value { get; set; }

        private static long _nextKey;
    }
}
