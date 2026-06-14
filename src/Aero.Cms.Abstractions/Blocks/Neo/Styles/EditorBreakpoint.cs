using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// Responsive breakpoint edited and previewed by the page editor.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EditorBreakpoint
{
    Desktop,
    Tablet,
    Mobile
}
