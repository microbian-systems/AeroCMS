namespace Aero.Cms.Abstractions.Pages.Composition;

/// <summary>
/// Defines one allowlisted comparison against a content field.
/// </summary>
public sealed record PageContentFilter
{
    /// <summary>Gets the content-field name.</summary>
    public string FieldName { get; init; } = string.Empty;

    /// <summary>Gets the comparison operator.</summary>
    public PageContentFilterOperator Operator { get; init; }

    /// <summary>
    /// Gets the invariant comparison value. Empty-state operators do not require a value.
    /// </summary>
    public string? Value { get; init; }
}
