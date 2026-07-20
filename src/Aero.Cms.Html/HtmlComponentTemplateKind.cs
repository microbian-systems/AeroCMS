namespace Aero.Cms.Html;

/// <summary>
/// Curated, editable page-building blocks exposed by the ordinary editor palette.
/// </summary>
public enum HtmlComponentTemplateKind
{
    /// <summary>A prominent introductory section.</summary>
    Hero,
    /// <summary>A two-part hero with copy and media.</summary>
    SplitHero,
    /// <summary>A grid of feature summaries.</summary>
    FeatureGrid,
    /// <summary>A vertically arranged feature list.</summary>
    FeatureList,
    /// <summary>A section focused on one action.</summary>
    CallToAction,
    /// <summary>A centered section focused on one action.</summary>
    CenteredCallToAction,
    /// <summary>A list of common questions and answers.</summary>
    FrequentlyAskedQuestions,
    /// <summary>A collapsible question-and-answer list using native disclosure elements.</summary>
    AccordionFaq,
    /// <summary>A customer or user quotation.</summary>
    Testimonial,
    /// <summary>A grid of highlighted numeric measures.</summary>
    Statistics,
    /// <summary>A paired image and copy section.</summary>
    ImageAndText,
    /// <summary>A contact form skeleton.</summary>
    ContactForm,
    /// <summary>An image gallery.</summary>
    Gallery,
    /// <summary>A site header with navigation links.</summary>
    NavigationHeader,
    /// <summary>A collection of partner or customer marks.</summary>
    LogoCloud,
    /// <summary>A grid of pricing options.</summary>
    PricingGrid,
    /// <summary>A grid of team-member profiles.</summary>
    TeamGrid,
    /// <summary>A site footer with grouped navigation.</summary>
    SiteFooter,
    /// <summary>An email subscription form.</summary>
    NewsletterSignup,
    /// <summary>A dismissible announcement region.</summary>
    AnnouncementBanner,
    /// <summary>A collection of recent article summaries.</summary>
    LatestArticles,
    /// <summary>An ordered sequence of process stages.</summary>
    ProcessSteps,
    /// <summary>A collection of featured work or products.</summary>
    ShowcaseCollection,
    /// <summary>A chronological sequence of milestones.</summary>
    MilestoneTimeline,
    /// <summary>A table comparing features across options.</summary>
    FeatureComparisonTable,
    /// <summary>A native disclosure list for supplementary details.</summary>
    DetailsList,
    /// <summary>A native dialog skeleton for confirmation flows.</summary>
    ConfirmationDialog
}
