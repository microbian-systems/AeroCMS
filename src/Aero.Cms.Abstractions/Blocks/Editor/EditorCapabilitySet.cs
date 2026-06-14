namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Declares shared property editor groups supported by a catalog definition.
/// </summary>
[Flags]
public enum EditorCapabilitySet
{
    None = 0,
    Content = 1 << 0,
    Typography = 1 << 1,
    Spacing = 1 << 2,
    Dimensions = 1 << 3,
    Layout = 1 << 4,
    Alignment = 1 << 5,
    Foreground = 1 << 6,
    Background = 1 << 7,
    Border = 1 << 8,
    Effects = 1 << 9,
    Media = 1 << 10,
    Icon = 1 << 11,
    Link = 1 << 12,
    Visibility = 1 << 13,
    Direction = 1 << 14,
    Collection = 1 << 15
}
