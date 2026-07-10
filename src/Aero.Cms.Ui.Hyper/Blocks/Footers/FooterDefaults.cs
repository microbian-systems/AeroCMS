namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// Shared default data for Footer 2, 3, and 4 blocks.
/// </summary>
internal static class FooterDefaults
{
        /// <summary>
    /// DefaultLinkColumns4.
    /// </summary>
public static readonly List<FooterLinkColumn> DefaultLinkColumns4 =
    [
        new()
        {
            Title = "Services",
            Links =
            [
                new() { Text = "1on1 Coaching" },
                new() { Text = "Company Review" },
                new() { Text = "Accounts Review" },
                new() { Text = "HR Consulting" },
                new() { Text = "SEO Optimisation" }
            ]
        },
        new()
        {
            Title = "Company",
            Links =
            [
                new() { Text = "About" },
                new() { Text = "Meet the Team" },
                new() { Text = "Accounts Review" }
            ]
        },
        new()
        {
            Title = "Helpful Links",
            Links =
            [
                new() { Text = "Contact" },
                new() { Text = "FAQs" },
                new() { Text = "Live Chat" }
            ]
        },
        new()
        {
            Title = "Legal",
            Links =
            [
                new() { Text = "Accessibility" },
                new() { Text = "Returns Policy" },
                new() { Text = "Refund Policy" },
                new() { Text = "Hiring-3 Statistics" }
            ]
        }
    ];

        /// <summary>
    /// DefaultSocialLinks.
    /// </summary>
public static readonly List<FooterSocialLink> DefaultSocialLinks =
    [
        new() { Name = "Facebook" },
        new() { Name = "Instagram" },
        new() { Name = "Twitter" },
        new() { Name = "GitHub" },
        new() { Name = "Dribbble" }
    ];

        /// <summary>
    /// CloneColumn method.
    /// </summary>
public static FooterLinkColumn CloneColumn(FooterLinkColumn col) => new()
    {
        Title = col.Title,
        Links = col.Links.Select(l => new FooterLink { Text = l.Text, Url = l.Url }).ToList()
    };

        /// <summary>
    /// CloneSocialLink method.
    /// </summary>
public static FooterSocialLink CloneSocialLink(FooterSocialLink link) => new()
    {
        Name = link.Name,
        Url = link.Url
    };
}
