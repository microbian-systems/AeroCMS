namespace Aero.Cms.Abstractions.Blocks.Layout;

/// <summary>
/// A lightweight representation of a media item for selection in the UI.
/// </summary>
/// <param name="Id">Unique identifier (UI only usually).</param>
/// <param name="Src">Source URL or Base64 data.</param>
/// <param name="Alt">Alternative text.</param>
public record MediaItem(long Id, string Src, string Alt);
