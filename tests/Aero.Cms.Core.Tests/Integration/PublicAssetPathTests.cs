namespace Aero.Cms.Core.Tests.Integration;

public sealed class PublicAssetPathTests
{
    [Test]
    public async Task Generated_public_styles_include_navigation_and_footer_utilities()
    {
        var repositoryRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var stylesheet = await File.ReadAllTextAsync(Path.Combine(
            repositoryRoot,
            "src",
            "Aero.Cms.Web",
            "wwwroot",
            "css",
            "aero.generated.css"));

        await Assert.That(stylesheet).Contains(".md\\:block");
        await Assert.That(stylesheet).Contains(".grid-cols-12");
        await Assert.That(stylesheet).Contains(".space-y-8");
        await Assert.That(stylesheet).Contains(".from-slate-900");
        await Assert.That(stylesheet).Contains(".to-slate-600");
    }

    [Test]
    public async Task Generated_public_styles_include_prefixed_Daisy_components_and_corporate_theme()
    {
        var repositoryRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var stylesheet = await File.ReadAllTextAsync(Path.Combine(
            repositoryRoot,
            "src",
            "Aero.Cms.Web",
            "wwwroot",
            "css",
            "aero.generated.css"));

        await Assert.That(stylesheet).Contains(".d-btn");
        await Assert.That(stylesheet).Contains(".d-card");
        await Assert.That(stylesheet).Contains("[data-theme=corporate]");
    }

    [Test]
    public async Task PublicRazorViews_UseRootRelativeSharedAssets()
    {
        var repositoryRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var files = new[]
        {
            Path.Combine(repositoryRoot, "src", "Aero.Cms.Web", "Views", "Shared", "_CmsLayout.cshtml"),
            Path.Combine(
                repositoryRoot,
                "src",
                "Aero.Cms.Modules.Navigation",
                "Views",
                "Shared",
                "Components",
                "AeroNavBar",
                "Default.cshtml")
        };

        foreach (var file in files)
        {
            var markup = await File.ReadAllTextAsync(file);

            await Assert.That(markup).DoesNotContain("href=\"_content/");
            await Assert.That(markup).DoesNotContain("src=\"_content/");
            await Assert.That(markup).DoesNotContain("srcset=\"_content/");
        }
    }
}
