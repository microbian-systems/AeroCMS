namespace Aero.Cms.Abstractions.Blocks.Layout;

/// <summary>
/// A simple reference item for picking pages, posts, categories, etc. in the UI.
/// </summary>
/// <param name="Id">Unique identifier as string (handles long and other types).</param>
/// <param name="Title">Primary display label (e.g. Page Title).</param>
/// <param name="Name">Secondary display label (e.g. Author Name).</param>
public record ReferenceItem(string Id, string? Title = null, string? Name = null);
