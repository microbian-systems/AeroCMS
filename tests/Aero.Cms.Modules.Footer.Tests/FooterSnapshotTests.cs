using System.Text.Json;
using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Serialization;
using Shouldly;

namespace Aero.Cms.Modules.Footer.Tests;

public sealed class FooterSnapshotTests
{
    [Test]
    public void Validate_RejectsDuplicateComponentKeys()
    {
        var snapshot = new FooterSnapshot
        {
            Sections =
            [
                new FooterLinkGroup { Key = "company", Title = "Company" },
                new FooterTextBlock { Key = "company", Text = "Duplicate" }
            ]
        };

        var ex = Should.Throw<InvalidOperationException>(() => snapshot.Validate());
        ex.Message.ShouldContain("Duplicate footer component key 'company'");
    }

    [Test]
    public void Validate_RejectsUnsafeBackgroundImageUrl()
    {
        var snapshot = new FooterSnapshot
        {
            Style = FooterStyleSettings.Default with { BackgroundImageUrl = "javascript:alert(1)" }
        };

        var ex = Should.Throw<InvalidOperationException>(() => snapshot.Validate());
        ex.Message.ShouldContain("background image URL must be a relative URL or absolute HTTP/HTTPS URL");
    }

    [Test]
    public void Snapshot_RoundTripsConcreteComponentTypesThroughFooterJsonContext()
    {
        var snapshot = new FooterSnapshot
        {
            Brand = new FooterBrandSettings
            {
                CompanyName = "Aero CMS",
                LogoUrl = "/img/logo.svg",
                Tagline = "Composable .NET CMS"
            },
            Style = FooterStyleSettings.Default with { BackgroundImageUrl = "/media/footer.jpg" },
            Sections =
            [
                new FooterLinkGroup
                {
                    Key = "company",
                    Title = "Company",
                    Links = [new FooterLink("About", "/about")]
                },
                new FooterTextBlock { Key = "tagline", Text = "Built with Aero." },
                new FooterSocialLinks { Key = "social", Links = [new FooterSocialLink("GitHub", "https://github.com/example")] }
            ]
        };

        var json = JsonSerializer.Serialize(snapshot, FooterJsonContext.Default.FooterSnapshot);
        var roundTripped = JsonSerializer.Deserialize(json, FooterJsonContext.Default.FooterSnapshot);

        roundTripped.ShouldNotBeNull();
        roundTripped!.Style.BackgroundImageUrl.ShouldBe("/media/footer.jpg");
        roundTripped.Sections.ShouldContain(x => x is FooterLinkGroup);
        roundTripped.Sections.ShouldContain(x => x is FooterTextBlock);
        roundTripped.Sections.ShouldContain(x => x is FooterSocialLinks);
    }
}
