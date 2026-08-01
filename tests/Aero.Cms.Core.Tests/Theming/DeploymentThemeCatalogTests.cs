using Aero.Cms.Abstractions.Theming;
using Aero.Cms.Modules.Theming;

namespace Aero.Cms.Core.Tests.Theming;

public sealed class DeploymentThemeCatalogTests
{
    [Test]
    public async Task Catalog_requires_unique_exact_identity_and_one_safe_default()
    {
        var manifest = CreateManifest("aero-safe", "1.0.0", true);

        var action = () => new DeploymentThemeCatalog([manifest, manifest with { IsSafeDefault = false }]);

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("https://example.test/theme.css")]
    [Arguments("/_content/Aero.Cms.Modules.Theming/themes/aero-safe/1.0.0/../secret.css")]
    [Arguments("/_content/Aero.Cms.Modules.Theming/themes/aero-safe/1.0.0/%2e%2e/secret.css")]
    [Arguments("/_content/Aero.Cms.Modules.Theming/themes/aero-safe/1.0.0/theme%00.css")]
    [Arguments("/_content/Aero.Cms.Modules.Theming/themes/aero-safe/1.0.0/\u0001theme.css")]
    [Arguments("/_content/Aero.Cms.Modules.Theming/themes/aero-safe/1.0.0/theme.scss")]
    public async Task Catalog_rejects_non_local_or_non_compiled_stylesheet_paths(string path)
    {
        var manifest = CreateManifest("aero-safe", "1.0.0", true) with
        {
            Stylesheets = [new ThemeStylesheetAsset(path, 0)]
        };

        var action = () => new DeploymentThemeCatalog([manifest]);

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Catalog_rejects_a_manifest_without_compiled_assets()
    {
        var manifest = CreateManifest("aero-safe", "1.0.0", true) with { Stylesheets = [] };

        var action = () => new DeploymentThemeCatalog([manifest]);

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Catalog_resolves_only_the_exact_installed_version()
    {
        var safe = CreateManifest("aero-safe", "1.0.0", true);
        var ocean = CreateManifest("ocean", "2.1.0", false);
        var catalog = new DeploymentThemeCatalog([safe, ocean]);

        var resolved = catalog.Find("ocean", "2.1.0");
        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Id).IsEqualTo(ocean.Id);
        await Assert.That(resolved.Version).IsEqualTo(ocean.Version);
        await Assert.That(resolved.Stylesheets).Count().IsEqualTo(1);
        await Assert.That(catalog.Find("ocean", "2.0.0")).IsNull();
    }

    [Test]
    public async Task Catalog_does_not_expose_mutable_manifest_or_asset_collections()
    {
        var catalog = new DeploymentThemeCatalog(
            [CreateManifest("aero-safe", "1.0.0", true)]);

        var manifests = (IList<InstalledThemeManifest>)catalog.GetAll();
        var assets = (IList<ThemeStylesheetAsset>)catalog.SafeDefault.Stylesheets;

        await Assert.That(() => manifests.Clear()).Throws<NotSupportedException>();
        await Assert.That(() => assets.Clear()).Throws<NotSupportedException>();
        await Assert.That(catalog.GetAll()).Count().IsEqualTo(1);
        await Assert.That(catalog.SafeDefault.Stylesheets).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Built_in_manifest_assets_exist_in_the_source_tree()
    {
        var repositoryRoot = FindRepositoryRoot();
        var assetRoot = Path.Combine(
            repositoryRoot,
            "src",
            "Aero.Cms.Modules.Theming",
            "wwwroot",
            "themes",
            "aero-safe",
            "1.0.0");

        await Assert.That(File.Exists(Path.Combine(assetRoot, "framework.css"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(assetRoot, "theme.css"))).IsTrue();
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Aero.Cms.Modules.Theming")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the AeroCMS repository root.");
    }

    internal static InstalledThemeManifest CreateManifest(string id, string version, bool isSafeDefault)
        => new(
            id,
            version,
            id,
            "Tests",
            "Test theme",
            ThemeAuthoringEngine.Css,
            [new ThemeStylesheetAsset(
                $"/_content/Aero.Cms.Modules.Theming/themes/{id}/{version}/theme.css",
                0)],
            IsSafeDefault: isSafeDefault);
}
