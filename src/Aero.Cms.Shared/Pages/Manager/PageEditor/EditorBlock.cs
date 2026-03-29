using Aero.Cms.Core.Blocks.Common;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

/// <summary>
/// A flat "bag of properties" representing any block being edited
/// in the canvas. Keeps the editor free from coupling to the
/// backend block hierarchy until Save is called.
/// </summary>
public class EditorBlock
{
    // todo - switch block editor ids to long (snowflake)
    public string EditorId { get; set; } = Guid.NewGuid().ToString();
    public string Type     { get; set; } = string.Empty;

    // Hero / Aero Hero
    public string MainText        { get; set; } = string.Empty;
    public string SubText         { get; set; } = string.Empty;
    public string CtaText         { get; set; } = string.Empty;
    public string CtaUrl          { get; set; } = string.Empty;
    public string CtaText2        { get; set; } = string.Empty;
    public string CtaUrl2         { get; set; } = string.Empty;
    public string BackgroundImage { get; set; } = string.Empty;
    public string AeroLayout      { get; set; } = "side_image";
    public string Button1Style    { get; set; } = "primary";
    public string Button2Style    { get; set; } = "secondary";

    // Text / Quote / Markdown / Rich Text
    public string Content      { get; set; } = string.Empty;
    public string Author       { get; set; } = string.Empty;
    public string MarkdownView { get; set; } = "edit";  // "edit" | "preview"

    // Columns
    public int                ColumnCount   { get; set; } = 2;
    public int                Gap           { get; set; } = 16;
    public List<EditorColumn> EditorColumns { get; set; } = [];

    // Image / Audio / Nested
    public string Src     { get; set; } = string.Empty;
    public string Alt     { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;

    // Video
    public string Url { get; set; } = string.Empty;

    // Gallery / Features
    public List<GalleryImage>   GalleryImages { get; set; } = [];
    public List<AeroFeatureItem> FeatureItems  { get; set; } = [];

    // Reference blocks
    public string SelectedReferenceId { get; set; } = string.Empty;

    public EditorBlock DeepClone()
    {
        var copy = (EditorBlock)MemberwiseClone();
        copy.EditorId = Guid.NewGuid().ToString();
        copy.EditorColumns = EditorColumns
            .Select(c => new EditorColumn
            {
                ColId  = Guid.NewGuid().ToString(),
                Blocks = c.Blocks.Select(nb => nb.Clone()).ToList(),
            })
            .ToList();
        copy.GalleryImages = GalleryImages.Select(g => new GalleryImage { Src = g.Src, Alt = g.Alt }).ToList();
        copy.FeatureItems  = FeatureItems.Select(f => new AeroFeatureItem { Title = f.Title, Description = f.Description, Icon = f.Icon, ImageUrl = f.ImageUrl, LinkUrl = f.LinkUrl }).ToList();
        return copy;
    }
}