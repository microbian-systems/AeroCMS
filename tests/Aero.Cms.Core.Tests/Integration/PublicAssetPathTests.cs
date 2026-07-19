namespace Aero.Cms.Core.Tests.Integration;

public sealed class PublicAssetPathTests
{
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
