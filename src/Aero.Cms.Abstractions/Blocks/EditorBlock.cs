using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;

namespace Aero.Cms.Abstractions.Blocks;

/// <summary>
/// A flat "bag of properties" representing any block being edited
/// in the canvas. Keeps the editor free from coupling to the
/// backend block hierarchy until Save is called.
/// </summary>
public class EditorBlock
{
    // Note: EditorId is a client-side in-memory identifier for the editor canvas.
    // Guid is sufficient here — avoids AOT issues with Process.GetCurrentProcess() in Snowflake.
        /// <summary>
    /// Gets or sets the Editor Id.
    /// </summary>
public string EditorId { get; set; } = Guid.NewGuid().ToString();
        /// <summary>
    /// Gets or sets the Type.
    /// </summary>
public string Type     { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Style.
    /// </summary>
public ResponsiveNodeStyle Style { get; set; } = new();

    // Hero / Aero Hero
        /// <summary>
    /// Gets or sets the Title.
    /// </summary>
public string Title           { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Main Text.
    /// </summary>
public string MainText        { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Sub Text.
    /// </summary>
public string SubText         { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Cta Text.
    /// </summary>
public string CtaText         { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Cta Url.
    /// </summary>
public string CtaUrl          { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Cta Text2.
    /// </summary>
public string CtaText2        { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Cta Url2.
    /// </summary>
public string CtaUrl2         { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Eyebrow.
    /// </summary>
public string Eyebrow         { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Highlight.
    /// </summary>
public string Highlight       { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Trust Markers.
    /// </summary>
public List<string> TrustMarkers { get; set; } = [];
        /// <summary>
    /// Gets or sets the Alternative Link Text.
    /// </summary>
public string AlternativeLinkText { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Alternative Link Url.
    /// </summary>
public string AlternativeLinkUrl  { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Background Image.
    /// </summary>
public string BackgroundImage { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Height.
    /// </summary>
public int    Height          { get; set; } = 512;
        /// <summary>
    /// Gets or sets the Full Screen.
    /// </summary>
public bool   FullScreen      { get; set; }
        /// <summary>
    /// Gets or sets the Full Width.
    /// </summary>
public bool   FullWidth       { get; set; }
        /// <summary>
    /// Gets or sets the Aero Layout.
    /// </summary>
public string AeroLayout      { get; set; } = "side_image";
        /// <summary>
    /// Gets or sets the Button1Style.
    /// </summary>
public string Button1Style    { get; set; } = "primary";
        /// <summary>
    /// Gets or sets the Button2Style.
    /// </summary>
public string Button2Style    { get; set; } = "secondary";

    // Generic Titles/Descriptions for Aero Blocks
        /// <summary>
    /// Gets or sets the Section Title.
    /// </summary>
public string SectionTitle     { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Page Title.
    /// </summary>
public string PageTitle        { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string Description      { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Page Description.
    /// </summary>
public string PageDescription  { get; set; } = string.Empty;

    // Text / Quote / Markdown / Rich Text
        /// <summary>
    /// Gets or sets the Content.
    /// </summary>
public string Content      { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public string Author       { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Markdown View.
    /// </summary>
public string MarkdownView { get; set; } = "edit";  // "edit" | "preview"
        /// <summary>
    /// Gets or sets the Scriban Template.
    /// </summary>
public string ScribanTemplate { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Scriban Data Json.
    /// </summary>
public string ScribanDataJson { get; set; } = "{}";
        /// <summary>
    /// Gets or sets the Scriban View.
    /// </summary>
public string ScribanView     { get; set; } = "code";  // "code" | "preview"

    // Columns
        /// <summary>
    /// Gets or sets the Column Count.
    /// </summary>
public int                ColumnCount   { get; set; } = 2;
        /// <summary>
    /// Gets or sets the Row Count.
    /// </summary>
public int                RowCount      { get; set; } = 1;
        /// <summary>
    /// Gets or sets the Gap.
    /// </summary>
public int                Gap           { get; set; } = 16;
        /// <summary>
    /// Gets or sets the Editor Columns.
    /// </summary>
public List<EditorColumn> EditorColumns { get; set; } = [];

    // Image / Audio / Nested
        /// <summary>
    /// Gets or sets the Src.
    /// </summary>
public string Src     { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Alt.
    /// </summary>
public string Alt     { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Caption.
    /// </summary>
public string Caption { get; set; } = string.Empty;

    // Video
        /// <summary>
    /// Gets or sets the Url.
    /// </summary>
public string Url { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Auto Play.
    /// </summary>
public bool AutoPlay { get; set; }

    // Carousel
        /// <summary>
    /// Gets or sets the Show Arrows.
    /// </summary>
public bool   ShowArrows      { get; set; } = true;
        /// <summary>
    /// Gets or sets the Carousel Interval.
    /// </summary>
public int    CarouselInterval { get; set; } = 5000;
        /// <summary>
    /// Gets or sets the Control Location.
    /// </summary>
public string ControlLocation  { get; set; } = "bottom";

    // Gallery / Features / Blog / Pricing / Teams / Testimonials
        /// <summary>
    /// Gets or sets the Gallery Images.
    /// </summary>
public List<GalleryImage>   GalleryImages { get; set; } = [];
        /// <summary>
    /// Gets or sets the Feature Items.
    /// </summary>
public List<AeroFeatureItem> FeatureItems  { get; set; } = [];
        /// <summary>
    /// Gets or sets the Blog Posts.
    /// </summary>
public List<AeroBlogItem>    BlogPosts     { get; set; } = [];
        /// <summary>
    /// Gets or sets the Pricing Plans.
    /// </summary>
public List<AeroPricingPlan> PricingPlans  { get; set; } = [];
        /// <summary>
    /// Gets or sets the Team Members.
    /// </summary>
public List<AeroTeamMember> TeamMembers { get; set; } = new();
        /// <summary>
    /// Gets or sets the Testimonials.
    /// </summary>
public List<AeroTestimonialItem> Testimonials { get; set; } = new();
        /// <summary>
    /// Gets or sets the Faq Items.
    /// </summary>
public List<AeroFaqItem> FaqItems { get; set; } = new();
        /// <summary>
    /// Gets or sets the Portfolio Items.
    /// </summary>
public List<AeroPortfolioItem> PortfolioItems { get; set; } = new();
        /// <summary>
    /// Gets or sets the Contact Details.
    /// </summary>
public List<AeroContactDetail> ContactDetails { get; set; } = new();
        /// <summary>
    /// Gets or sets the Table Headers.
    /// </summary>
public List<AeroTableHeader> TableHeaders { get; set; } = new();
        /// <summary>
    /// Gets or sets the Table Rows.
    /// </summary>
public List<AeroTableRow> TableRows { get; set; } = new();

    // Reference blocks
        /// <summary>
    /// Gets or sets the Selected Reference Id.
    /// </summary>
public string SelectedReferenceId { get; set; } = string.Empty;

    /// <summary>
    /// Node-native composition content transported by the legacy top-level canvas.
    /// Remove this bridge when the canvas stores NeoPageNode trees directly.
    /// </summary>
    public List<NeoPageNode> CompositionNodes { get; set; } = [];

        /// <summary>
    /// DeepClone method.
    /// </summary>
public EditorBlock DeepClone()
    {
        var copy = (EditorBlock)MemberwiseClone();
        copy.EditorId = Guid.NewGuid().ToString();
        copy.Style = Style.DeepClone();
        copy.Title = Title;
        copy.TrustMarkers = TrustMarkers.ToList();
        copy.AlternativeLinkText = AlternativeLinkText;
        copy.AlternativeLinkUrl = AlternativeLinkUrl;
        copy.EditorColumns = EditorColumns
            .Select(c => new EditorColumn
            {
                ColId  = Guid.NewGuid().ToString(),
                Blocks = c.Blocks.Select(nb => nb.Clone()).ToList(),
            })
            .ToList();
        copy.GalleryImages = GalleryImages.Select(g => new GalleryImage { Src = g.Src, Alt = g.Alt }).ToList();
        copy.FeatureItems  = FeatureItems.Select(f => new AeroFeatureItem { Title = f.Title, Description = f.Description, Icon = f.Icon, ImageUrl = f.ImageUrl, LinkUrl = f.LinkUrl }).ToList();
        copy.BlogPosts     = BlogPosts.Select(p => new AeroBlogItem { Title = p.Title, Description = p.Description, ImageUrl = p.ImageUrl, AuthorName = p.AuthorName, PublishedAt = p.PublishedAt, Category = p.Category, PostUrl = p.PostUrl }).ToList();
        copy.PricingPlans  = PricingPlans.Select(p => new AeroPricingPlan { Name = p.Name, Price = p.Price, Period = p.Period, Description = p.Description, Features = p.Features.ToList(), CtaText = p.CtaText, CtaUrl = p.CtaUrl, IsPopular = p.IsPopular }).ToList();
        copy.TeamMembers   = TeamMembers.Select(m => new AeroTeamMember { Name = m.Name, Role = m.Role, AvatarUrl = m.AvatarUrl, Description = m.Description, LinkedInUrl = m.LinkedInUrl }).ToList();
        copy.Testimonials  = Testimonials.Select(t => new AeroTestimonialItem { AuthorName = t.AuthorName, AuthorRole = t.AuthorRole, AuthorImage = t.AuthorImage, Content = t.Content, StarRating = t.StarRating, CompanyName = t.CompanyName }).ToList();
        copy.FaqItems = FaqItems.Select(f => new AeroFaqItem { Question = f.Question, Answer = f.Answer }).ToList();
        copy.PortfolioItems = PortfolioItems.Select(p => new AeroPortfolioItem { ProjectTitle = p.ProjectTitle, ProjectDescription = p.ProjectDescription, ProjectImageUrl = p.ProjectImageUrl, ProjectUrl = p.ProjectUrl, ProjectCategory = p.ProjectCategory }).ToList();
        copy.ContactDetails = ContactDetails.Select(c => new AeroContactDetail { Label = c.Label, Value = c.Value, Icon = c.Icon }).ToList();
        copy.TableHeaders = TableHeaders.Select(h => new AeroTableHeader { Label = h.Label }).ToList();
        copy.TableRows = TableRows.Select(r => new AeroTableRow { Cells = r.Cells.ToList() }).ToList();
        copy.CompositionNodes = CompositionNodes
            .Select(node => EditorNodeMemento.Capture(node).Restore())
            .ToList();
        return copy;
    }

        /// <summary>
    /// CreateClipboardClone method.
    /// </summary>
public EditorBlock CreateClipboardClone()
    {
        var copy = DeepClone();
        copy.CompositionNodes = CompositionNodes
            .Select(CustomComponentTemplate.CreateInstance)
            .ToList();
        return copy;
    }
}
