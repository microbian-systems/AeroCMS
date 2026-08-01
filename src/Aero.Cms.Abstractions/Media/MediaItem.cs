namespace Aero.Cms.Abstractions.Media;

/// <summary>
/// A lightweight media-library selection item shared by manager editors.
/// </summary>
/// <param name="Id">The media asset identifier.</param>
/// <param name="Src">The media source URL.</param>
/// <param name="Alt">The alternative text or display name.</param>
public sealed record MediaItem(long Id, string Src, string Alt);
