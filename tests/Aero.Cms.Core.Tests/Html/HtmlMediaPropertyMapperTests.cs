using Aero.Cms.Html;
using Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

namespace Aero.Cms.Core.Tests.Html;

public sealed class HtmlMediaPropertyMapperTests
{
    [Test]
    public async Task Image_selection_sets_source_and_only_fills_missing_alternative_text()
    {
        var image = HtmlNode.CreateElement("img");
        image.Attributes["alt"] = string.Empty;

        var mapped = HtmlMediaPropertyMapper.Map(
            image,
            HtmlMediaTargetKind.ElementSource,
            "/media/landscape.jpg",
            "Mountain landscape");

        await Assert.That(mapped.Attributes["src"]).IsEqualTo("/media/landscape.jpg");
        await Assert.That(mapped.Attributes["alt"]).IsEqualTo("Mountain landscape");
        await Assert.That(image.Attributes.ContainsKey("src")).IsFalse();

        image.Attributes["alt"] = "Editorial description";
        var remapped = HtmlMediaPropertyMapper.Map(
            image,
            HtmlMediaTargetKind.ElementSource,
            "/media/replacement.jpg",
            "File name");

        await Assert.That(remapped.Attributes["alt"]).IsEqualTo("Editorial description");
    }

    [Test]
    public async Task Poster_and_background_selections_preserve_unrelated_properties()
    {
        var video = HtmlNode.CreateElement("video");
        video.Attributes["controls"] = string.Empty;
        var poster = HtmlMediaPropertyMapper.Map(
            video,
            HtmlMediaTargetKind.VideoPoster,
            "/media/poster.jpg");

        var section = HtmlNode.CreateElement("section");
        section.Style = new HtmlStyle { Display = CssDisplay.Grid, GridColumns = 2 };
        var background = HtmlMediaPropertyMapper.Map(
            section,
            HtmlMediaTargetKind.BackgroundImage,
            "/media/background.jpg");

        await Assert.That(poster.Attributes["controls"]).IsEqualTo(string.Empty);
        await Assert.That(poster.Attributes["poster"]).IsEqualTo("/media/poster.jpg");
        await Assert.That(background.Style!.Display).IsEqualTo(CssDisplay.Grid);
        await Assert.That(background.Style.GridColumns).IsEqualTo(2);
        await Assert.That(background.Style.Surface!.BackgroundImageUrl)
            .IsEqualTo("/media/background.jpg");
    }

    [Test]
    public async Task Responsive_picture_selection_maps_to_source_set()
    {
        var source = HtmlNode.CreateElement("source");

        var mapped = HtmlMediaPropertyMapper.Map(
            source,
            HtmlMediaTargetKind.ResponsiveSourceSet,
            "/media/landscape-wide.jpg");

        await Assert.That(mapped.Attributes["srcset"]).IsEqualTo("/media/landscape-wide.jpg");
        await Assert.That(source.Attributes.ContainsKey("srcset")).IsFalse();
    }
}
