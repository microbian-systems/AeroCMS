namespace Aero.Cms.Abstractions.Pages.Composition;

/// <summary>
/// Maps one field in a content scope to an explicit HTML output target.
/// </summary>
public sealed record PageFieldBinding
{
    /// <summary>Gets the HTML node receiving the projected value.</summary>
    public long NodeId { get; init; }

    /// <summary>Gets the owning content-scope node.</summary>
    public long ScopeNodeId { get; init; }

    /// <summary>Gets the content-field name.</summary>
    public string FieldName { get; init; } = string.Empty;

    /// <summary>Gets the allowlisted HTML output target.</summary>
    public PageFieldBindingTarget Target { get; init; }
}
