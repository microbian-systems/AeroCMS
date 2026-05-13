using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Serialization;
using FluentAssertions;
using System.Text.Json;

namespace Aero.Cms.Modules.Navigation.Tests;

public sealed class NavMenuSnapshotTests
{
    [Test]
    public void Validate_AllowsSameKeyTextInDifferentAlignmentBucketsWhenKeysDiffer()
    {
        var snapshot = new NavMenuSnapshot(
            NavMenuLayout.Default,
            NavMenuResponsiveSettings.Default,
            NavMenuStyleSettings.Default,
            [
                new NavLink { Key = "home-left", Label = "Home", Href = "/", Alignment = NavAlignment.Left },
                new NavLink { Key = "home-right", Label = "Home", Href = "/search", Alignment = NavAlignment.Right }
            ]);

        var act = () => snapshot.Validate();

        act.Should().NotThrow();
    }

    [Test]
    public void Validate_RejectsDuplicateComponentKeys()
    {
        var snapshot = new NavMenuSnapshot(
            NavMenuLayout.Default,
            NavMenuResponsiveSettings.Default,
            NavMenuStyleSettings.Default,
            [
                new NavLink { Key = "home", Label = "Home", Href = "/", Alignment = NavAlignment.Left },
                new NavLink { Key = "home", Label = "About", Href = "/about", Alignment = NavAlignment.Left }
            ]);

        var act = () => snapshot.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Duplicate navigation component key 'home'*");
    }

    [Test]
    public void SiteLogoUrl_RoundTripsThroughNavigationJsonContext()
    {
        var snapshot = new NavMenuSnapshot(
            NavMenuLayout.Default,
            NavMenuResponsiveSettings.Default,
            NavMenuStyleSettings.Default,
            [
                new NavLink { Key = "home", Label = "Home", Href = "/", Alignment = NavAlignment.Left }
            ],
            "/img/site-logo.svg");

        var json = JsonSerializer.Serialize(snapshot, NavMenuJsonContext.Default.NavMenuSnapshot);
        var roundTripped = JsonSerializer.Deserialize(json, NavMenuJsonContext.Default.NavMenuSnapshot);

        roundTripped.Should().NotBeNull();
        roundTripped!.SiteLogoUrl.Should().Be("/img/site-logo.svg");
        roundTripped.Left.Should().ContainSingle()
            .Which.Should().BeOfType<NavLink>();
    }
}
