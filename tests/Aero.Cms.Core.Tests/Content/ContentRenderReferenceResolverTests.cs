using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Rendering;
using Aero.Cms.Core.Infrastructure;
using Aero.Core;
using Aero.Core.Railway;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentRenderReferenceResolverTests
{
    private static readonly ContentViewScope Scope = new(41, 84);

    [Test]
    public async Task Required_virtual_reference_projects_only_configured_fields_in_the_selected_site()
    {
        var key = new ContentEntryKey("view:species", "492B3");
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.FindAsync(Scope, key.StableId, Arg.Any<CancellationToken>()).Returns(new ContentEntry(
            key,
            Scope,
            new Dictionary<string, object?>
            {
                ["scientificName"] = "Okapia johnstoni",
                ["lineage"] = new Dictionary<string, object?> { ["family"] = "Giraffidae" },
                ["internalNote"] = "must not render"
            }));
        var catalog = Substitute.For<IContentEntrySourceProviderCatalog>();
        catalog.ResolveAsync(Scope, key.Provider, Arg.Any<CancellationToken>()).Returns(provider);
        var sites = Substitute.For<ISelectedSiteScopeResolver>();
        sites.ResolveAsync(Scope.SiteId, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(Scope.TenantId, Scope.SiteId));
        var resolver = new ContentRenderReferenceResolver(catalog, sites);

        var result = await resolver.ResolveAsync(Definition(required: true), Item(key));

        var ok = result as Result<JsonElement, AeroError>.Ok;
        ok.ShouldNotBeNull();
        var species = ok!.Value.GetProperty("species");
        species.GetProperty("provider").GetString().ShouldBe("view:species");
        species.GetProperty("stableId").GetString().ShouldBe("492B3");
        species.GetProperty("scientificName").GetString().ShouldBe("Okapia johnstoni");
        species.GetProperty("lineage").GetProperty("family").GetString().ShouldBe("Giraffidae");
        species.TryGetProperty("internalNote", out _).ShouldBeFalse();
    }

    [Test]
    public async Task Required_virtual_reference_fails_closed_when_provider_returns_another_scope()
    {
        var key = new ContentEntryKey("view:species", "492B3");
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.FindAsync(Scope, key.StableId, Arg.Any<CancellationToken>()).Returns(new ContentEntry(
            key,
            Scope with { SiteId = Scope.SiteId + 1 },
            new Dictionary<string, object?> { ["scientificName"] = "Okapia johnstoni" }));
        var catalog = Substitute.For<IContentEntrySourceProviderCatalog>();
        catalog.ResolveAsync(Scope, key.Provider, Arg.Any<CancellationToken>()).Returns(provider);
        var sites = Substitute.For<ISelectedSiteScopeResolver>();
        sites.ResolveAsync(Scope.SiteId, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(Scope.TenantId, Scope.SiteId));

        var result = await new ContentRenderReferenceResolver(catalog, sites)
            .ResolveAsync(Definition(required: true), Item(key));

        result.ShouldBeOfType<Result<JsonElement, AeroError>.Failure>();
    }

    [Test]
    public async Task Malformed_provider_allow_list_fails_closed_before_provider_resolution()
    {
        var key = new ContentEntryKey("view:species", "492B3");
        var definition = Definition(required: true);
        definition.Fields[0].Settings[ReferenceContentFieldSettings.AllowedProviders] =
            JsonSerializer.SerializeToElement("view:species");
        var catalog = Substitute.For<IContentEntrySourceProviderCatalog>();
        var sites = Substitute.For<ISelectedSiteScopeResolver>();
        sites.ResolveAsync(Scope.SiteId, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(Scope.TenantId, Scope.SiteId));

        var result = await new ContentRenderReferenceResolver(catalog, sites)
            .ResolveAsync(definition, Item(key));

        result.ShouldBeOfType<Result<JsonElement, AeroError>.Failure>();
        await catalog.DidNotReceiveWithAnyArgs().ResolveAsync(default, default!, default);
    }

    [Test]
    public async Task More_than_sixteen_virtual_references_fails_closed_before_provider_resolution()
    {
        var definition = Definition(required: false);
        definition.Fields = Enumerable.Range(1, 17)
            .Select(index => CloneField(definition.Fields[0], $"species{index}"))
            .ToList();
        var catalog = Substitute.For<IContentEntrySourceProviderCatalog>();
        var sites = Substitute.For<ISelectedSiteScopeResolver>();
        sites.ResolveAsync(Scope.SiteId, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(Scope.TenantId, Scope.SiteId));

        var result = await new ContentRenderReferenceResolver(catalog, sites)
            .ResolveAsync(definition, Item(new ContentEntryKey("view:species", "492B3")));

        result.ShouldBeOfType<Result<JsonElement, AeroError>.Failure>();
        await catalog.DidNotReceiveWithAnyArgs().ResolveAsync(default, default!, default);
    }

    [Test]
    public async Task Preview_fields_cannot_overwrite_authoritative_reference_identity()
    {
        var key = new ContentEntryKey("view:species", "492B3");
        var definition = Definition(required: true);
        definition.Fields[0].Settings[ReferenceContentFieldSettings.PreviewFields] =
            JsonSerializer.SerializeToElement(new[] { "provider", "stableId", "scientificName" });
        var provider = Substitute.For<IContentEntrySourceProvider>();
        provider.FindAsync(Scope, key.StableId, Arg.Any<CancellationToken>()).Returns(new ContentEntry(
            key,
            Scope,
            new Dictionary<string, object?>
            {
                ["provider"] = "view:poisoned",
                ["stableId"] = "wrong",
                ["scientificName"] = "Okapia johnstoni"
            }));
        var catalog = Substitute.For<IContentEntrySourceProviderCatalog>();
        catalog.ResolveAsync(Scope, key.Provider, Arg.Any<CancellationToken>()).Returns(provider);
        var sites = Substitute.For<ISelectedSiteScopeResolver>();
        sites.ResolveAsync(Scope.SiteId, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(Scope.TenantId, Scope.SiteId));

        var result = await new ContentRenderReferenceResolver(catalog, sites)
            .ResolveAsync(definition, Item(key));

        var species = result.ShouldBeOfType<Result<JsonElement, AeroError>.Ok>().Value.GetProperty("species");
        species.GetProperty("provider").GetString().ShouldBe("view:species");
        species.GetProperty("stableId").GetString().ShouldBe("492B3");
    }

    private static ContentTypeDefinition Definition(bool required) => new()
    {
        SiteId = Scope.SiteId,
        Alias = "animal",
        Fields =
        [
            new ContentFieldDefinition
            {
                Name = "species",
                Label = "Species",
                FieldType = ContentFieldTypes.Reference,
                Required = required,
                Settings = new Dictionary<string, JsonElement>
                {
                    [ReferenceContentFieldSettings.TargetKind] = JsonSerializer.SerializeToElement(
                        ReferenceContentFieldSettings.TargetKindContentEntry),
                    [ReferenceContentFieldSettings.AllowedProviders] = JsonSerializer.SerializeToElement(new[] { "view:species" }),
                    [ReferenceContentFieldSettings.PreviewFields] = JsonSerializer.SerializeToElement(new[] { "scientificName", "lineage" })
                }
            }
        ]
    };

    private static ContentItem Item(ContentEntryKey key) => new()
    {
        Id = 101,
        SiteId = Scope.SiteId,
        ContentTypeAlias = "animal",
        Fields = new Dictionary<string, JsonElement>
        {
            ["species"] = JsonSerializer.SerializeToElement(key, ContentJsonContext.Default.ContentEntryKey)
        }
    };

    private static ContentFieldDefinition CloneField(ContentFieldDefinition source, string name) => new()
    {
        Name = name,
        Label = name,
        FieldType = source.FieldType,
        Required = source.Required,
        Settings = source.Settings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
    };
}
