using Aero.Cms.Abstractions.Blocks.Common;

namespace Aero.Cms.Abstractions.Blocks;

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
    public string Title           { get; set; } = string.Empty;
    public string MainText        { get; set; } = string.Empty;
    public string SubText         { get; set; } = string.Empty;
    public string CtaText         { get; set; } = string.Empty;
    public string CtaUrl          { get; set; } = string.Empty;
    public string CtaText2        { get; set; } = string.Empty;
    public string CtaUrl2         { get; set; } = string.Empty;
    public string AlternativeLinkText { get; set; } = string.Empty;
    public string AlternativeLinkUrl  { get; set; } = string.Empty;
    public string BackgroundImage { get; set; } = string.Empty;
    public string AeroLayout      { get; set; } = "side_image";
    public string Button1Style    { get; set; } = "primary";
    public string Button2Style    { get; set; } = "secondary";

    // Generic Titles/Descriptions for Aero Blocks
    public string SectionTitle     { get; set; } = string.Empty;
    public string PageTitle        { get; set; } = string.Empty;
    public string Description      { get; set; } = string.Empty;
    public string PageDescription  { get; set; } = string.Empty;

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

    // Gallery / Features / Blog / Pricing / Teams / Testimonials
    public List<GalleryImage>   GalleryImages { get; set; } = [];
    public List<AeroFeatureItem> FeatureItems  { get; set; } = [];
    public List<AeroBlogItem>    BlogPosts     { get; set; } = [];
    public List<AeroPricingPlan> PricingPlans  { get; set; } = [];
    public List<AeroTeamMember> TeamMembers { get; set; } = new();
    public List<AeroTestimonialItem> Testimonials { get; set; } = new();
    public List<AeroFaqItem> FaqItems { get; set; } = new();
    public List<AeroPortfolioItem> PortfolioItems { get; set; } = new();
    public List<AeroContactDetail> ContactDetails { get; set; } = new();
    public List<AeroTableHeader> TableHeaders { get; set; } = new();
    public List<AeroTableRow> TableRows { get; set; } = new();

    // Reference blocks
    public string SelectedReferenceId { get; set; } = string.Empty;

    public EditorBlock DeepClone()
    {
        var copy = (EditorBlock)MemberwiseClone();
        copy.EditorId = Guid.NewGuid().ToString();
        copy.Title = Title;
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
        return copy;
    }
}