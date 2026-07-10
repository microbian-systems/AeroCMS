using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Neo.Styles;

/// <summary>
/// A normalized CSS color accepted by the visual editor.
/// </summary>
[JsonConverter(typeof(CssColorJsonConverter))]
public readonly record struct CssColor(string Value)
{
        /// <summary>
    /// ToString method.
    /// </summary>
public override string ToString() => Value;
}
