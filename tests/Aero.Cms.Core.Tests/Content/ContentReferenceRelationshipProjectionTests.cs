using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Indexing;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Content.Views;
using Aero.Cms.Core.Infrastructure;
using Aero.Cms.Modules.Content;
using AeroDB.Sable;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentReferenceRelationshipProjectionTests
{
    [Test]
    public async Task Content_type_reference_materializes_replaces_and_removes_one_native_edge()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(new ContentModule().Configure);
        await harness.InitializeAsync();
        harness.Session.Store(
            new ContentTypeDocument { Id = 10, SiteId = 7, Alias = "animal", Name = "Animal" },
            new ContentTypeDocument { Id = 20, SiteId = 7, Alias = "species-profile", Name = "Species profile" });
        harness.Session.Store(
            new ContentTranslationGroupDocument
            {
                Id = 1000, SiteId = 7, ContentTypeAlias = "animal", SourceItemId = 100, SourceCulture = "en-US"
            },
            new ContentTranslationGroupDocument
            {
                Id = 2000, SiteId = 7, ContentTypeAlias = "species-profile", SourceItemId = 200, SourceCulture = "en-US"
            },
            new ContentTranslationGroupDocument
            {
                Id = 3000, SiteId = 7, ContentTypeAlias = "species-profile", SourceItemId = 300, SourceCulture = "en-US"
            });
        harness.Session.Store(
            Barrier(1000),
            Barrier(2000),
            Barrier(3000));
        harness.Session.Store(
            new ContentItem
            {
                Id = 200, SiteId = 7, ContentTypeAlias = "species-profile", Culture = "en-US", Slug = "wolf", Title = "Wolf", TranslationGroupId = 2000
            },
            new ContentItem
            {
                Id = 300, SiteId = 7, ContentTypeAlias = "species-profile", Culture = "en-US", Slug = "lynx", Title = "Lynx", TranslationGroupId = 3000
            });
        await harness.Session.SaveChangesAsync();

        var selectedSites = Substitute.For<ISelectedSiteScopeResolver>();
        selectedSites.ResolveAsync(7, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(5, 7));
        var barriers = new ContentRelationshipTargetBarrierCoordinator();
        var materializer = new ContentTypeReferenceRelationshipMaterializer(selectedSites, barriers);
        var (field, declaration) = Declaration();

        var first = await materializer.StageAsync(
            harness.Session,
            Context(field, declaration, JsonSerializer.SerializeToElement("200"), revision: 1));
        first.IsSuccess.ShouldBeTrue();
        await harness.Session.SaveChangesAsync();
        var stored = await harness.Session.Query<ContentReferenceRelation>().ToListAsync();
        stored.Count.ShouldBe(1);
        stored[0].RelationshipAlias.ShouldBe("animal_species_profile");
        stored[0].SourceTranslationGroupId.ShouldBe(1000);
        stored[0].TargetTranslationGroupId.ShouldBe(2000);

        var replacement = await materializer.StageAsync(
            harness.Session,
            Context(field, declaration, JsonSerializer.SerializeToElement("300"), revision: 2));
        replacement.IsSuccess.ShouldBeTrue();
        await harness.Session.SaveChangesAsync();
        stored = await harness.Session.Query<ContentReferenceRelation>().ToListAsync();
        stored.Count.ShouldBe(1);
        stored[0].TargetTranslationGroupId.ShouldBe(3000);
        stored[0].SourceTranslationGroupRevision.ShouldBe(2);

        var removed = await materializer.StageAsync(
            harness.Session,
            Context(field, declaration, null, revision: 3));
        removed.IsSuccess.ShouldBeTrue();
        await harness.Session.SaveChangesAsync();
        (await harness.Session.Query<ContentReferenceRelation>().ToListAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task Declared_catalog_emits_a_stable_derived_descriptor_for_the_content_field()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(new ContentModule().Configure);
        await harness.InitializeAsync();
        var (field, _) = Declaration();
        harness.Session.Store(
            new ContentTypeDocument { Id = 10, SiteId = 7, Alias = "animal", Name = "Animal", Fields = [field] },
            new ContentTypeDocument { Id = 20, SiteId = 7, Alias = "species-profile", Name = "Species profile" });
        await harness.Session.SaveChangesAsync();
        var catalog = new ContentReferenceRelationshipCatalog(
            harness.Session,
            [new ContentTypeReferenceRelationshipMaterializer()]);

        var first = (await catalog.ListAsync(new ContentViewScope(5, 7))).Single();
        var second = (await catalog.ListAsync(new ContentViewScope(5, 7))).Single();

        first.ShouldBe(second);
        first.Id.ShouldBeLessThan(0);
        first.OwnershipState.ShouldBe(ContentRelationshipOwnershipState.Derived);
        first.Kind.ShouldBe(ContentRelationshipKind.GraphEdge);
        first.SourceShapeAlias.ShouldBe("animal");
        first.TargetShapeAlias.ShouldBe("species-profile");
        first.SchemaFingerprint.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Scope_corrupted_edge_fails_closed_without_becoming_invisible_or_duplicated()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(new ContentModule().Configure);
        await harness.InitializeAsync();
        var (field, declaration) = Declaration();
        harness.Session.Store(
            new ContentTypeDocument { Id = 10, SiteId = 7, Alias = "animal", Name = "Animal", Fields = [field] },
            new ContentTypeDocument { Id = 20, SiteId = 7, Alias = "species-profile", Name = "Species profile" });
        harness.Session.Store(
            new ContentTranslationGroupDocument
            {
                Id = 1000, SiteId = 7, ContentTypeAlias = "animal", SourceItemId = 100, SourceCulture = "en-US"
            },
            new ContentTranslationGroupDocument
            {
                Id = 2000, SiteId = 7, ContentTypeAlias = "species-profile", SourceItemId = 200, SourceCulture = "en-US"
            });
        harness.Session.Store(Barrier(1000), Barrier(2000));
        harness.Session.Store(new ContentItem
        {
            Id = 200,
            SiteId = 7,
            ContentTypeAlias = "species-profile",
            Culture = "en-US",
            Slug = "wolf",
            Title = "Wolf",
            TranslationGroupId = 2000
        });
        await harness.Session.SaveChangesAsync();
        var selectedSites = Substitute.For<ISelectedSiteScopeResolver>();
        selectedSites.ResolveAsync(7, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(5, 7));
        var materializer = new ContentTypeReferenceRelationshipMaterializer(
            selectedSites,
            new ContentRelationshipTargetBarrierCoordinator());
        (await materializer.StageAsync(
            harness.Session,
            Context(field, declaration, JsonSerializer.SerializeToElement("200"), revision: 1)))
            .IsSuccess.ShouldBeTrue();
        await harness.Session.SaveChangesAsync();

        await using (var poison = await harness.Store.OpenSessionAsync(new SessionOptions()))
        {
            await poison.ExecuteSqlAsync(
                "UPDATE content_reference_relation SET tenant_id = 999;",
                new Dictionary<string, object?>());
        }

        await using var attempt = await harness.Store.OpenSessionAsync(new SessionOptions());
        var rejected = await materializer.StageAsync(
            attempt,
            Context(field, declaration, JsonSerializer.SerializeToElement("200"), revision: 2));

        rejected.IsFailure.ShouldBeTrue();
        await using var verify = await harness.Store.QuerySessionAsync();
        var stored = (await verify.Query<ContentReferenceRelation>().ToListAsync()).Single();
        stored.TargetTranslationGroupId.ShouldBe(2000);
        stored.TenantId.ShouldBe(999);
    }

    [Test]
    public async Task Adoption_is_idempotent_for_the_exact_descriptor_and_drifts_fail_closed()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(new ContentModule().Configure);
        await harness.InitializeAsync();
        var store = new SableContentRelationshipStore(harness.Session);
        var candidate = new ContentRelationshipDefinition(
            -7,
            new ContentViewScope(5, 7),
            "animal_species_profile",
            "animal",
            "species-profile",
            "content_translation_groups",
            "content_translation_groups",
            null,
            null,
            "content_reference_relation",
            ContentRelationshipKind.GraphEdge,
            ContentRelationshipCardinality.ManyToOne,
            ContentRelationshipOwnershipState.ExternalDiscovered,
            "SERVER-FINGERPRINT");

        var first = await store.AdoptAsync(candidate);
        var replay = await store.AdoptAsync(candidate);

        first.Id.ShouldBeGreaterThan(0);
        replay.ShouldBe(first);
        first.OwnershipState.ShouldBe(ContentRelationshipOwnershipState.Adopted);
        await store.MarkDriftedAsync(candidate.Scope, first.Id, candidate.SchemaFingerprint);
        (await store.LoadAsync(candidate.Scope, candidate.Alias))!.OwnershipState
            .ShouldBe(ContentRelationshipOwnershipState.Adopted);
        await store.MarkDriftedAsync(candidate.Scope, first.Id, "CHANGED-FINGERPRINT");
        (await store.LoadAsync(candidate.Scope, candidate.Alias))!.OwnershipState
            .ShouldBe(ContentRelationshipOwnershipState.Drifted);

        await Should.ThrowAsync<InvalidOperationException>(() => store.AdoptAsync(
            candidate with { TargetTable = "different_target" }));
    }

    [Test]
    public async Task Target_delete_is_blocked_until_the_source_relationship_is_removed()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(new ContentModule().Configure);
        await harness.InitializeAsync();
        var (field, declaration) = Declaration();
        harness.Session.Store(
            new ContentTypeDocument { Id = 10, SiteId = 7, Alias = "animal", Name = "Animal", Fields = [field] },
            new ContentTypeDocument { Id = 20, SiteId = 7, Alias = "species-profile", Name = "Species profile" });
        harness.Session.Store(
            new ContentTranslationGroupDocument
            {
                Id = 1000, SiteId = 7, ContentTypeAlias = "animal", SourceItemId = 100, SourceCulture = "en-US"
            },
            new ContentTranslationGroupDocument
            {
                Id = 2000, SiteId = 7, ContentTypeAlias = "species-profile", SourceItemId = 200, SourceCulture = "en-US"
            });
        harness.Session.Store(
            new ContentItem
            {
                Id = 100, SiteId = 7, ContentTypeAlias = "animal", Culture = "en-US", Slug = "animal", Title = "Animal", TranslationGroupId = 1000
            },
            new ContentItem
            {
                Id = 200, SiteId = 7, ContentTypeAlias = "species-profile", Culture = "en-US", Slug = "wolf", Title = "Wolf", TranslationGroupId = 2000
            });
        harness.Session.Store(
            Barrier(1000),
            Barrier(2000));
        await harness.Session.SaveChangesAsync();
        var selectedSites = Substitute.For<ISelectedSiteScopeResolver>();
        selectedSites.ResolveAsync(7, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(5, 7));
        var barriers = new ContentRelationshipTargetBarrierCoordinator();
        var materializer = new ContentTypeReferenceRelationshipMaterializer(selectedSites, barriers);
        (await materializer.StageAsync(
            harness.Session,
            Context(field, declaration, JsonSerializer.SerializeToElement("200"), revision: 1)))
            .IsSuccess.ShouldBeTrue();
        await harness.Session.SaveChangesAsync();

        await using (var blockedSession = await harness.Store.OpenSessionAsync(new SessionOptions()))
        {
            var contributor = new ContentReferenceRelationshipProjectionContributor([materializer], barriers);
            var service = new AeroContentService(blockedSession, null, [contributor]);
            var blocked = await service.DeleteAsync(7, 200);
            blocked.IsFailure.ShouldBeTrue();
            blocked.ToString().ShouldContain("referenced by another content entry");
        }

        await using (var sourceSession = await harness.Store.OpenSessionAsync(new SessionOptions()))
        {
            var contributor = new ContentReferenceRelationshipProjectionContributor([materializer], barriers);
            var service = new AeroContentService(sourceSession, null, [contributor]);
            (await service.DeleteAsync(7, 100)).IsSuccess.ShouldBeTrue();
        }

        await using (var targetSession = await harness.Store.OpenSessionAsync(new SessionOptions()))
        {
            var contributor = new ContentReferenceRelationshipProjectionContributor([materializer], barriers);
            var service = new AeroContentService(targetSession, null, [contributor]);
            (await service.DeleteAsync(7, 200)).IsSuccess.ShouldBeTrue();
        }
    }

    [Test]
    public async Task Target_barrier_fences_the_add_relationship_delete_race()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(new ContentModule().Configure);
        await harness.InitializeAsync();
        var (field, declaration) = Declaration();
        harness.Session.Store(
            new ContentTypeDocument { Id = 10, SiteId = 7, Alias = "animal", Name = "Animal", Fields = [field] },
            new ContentTypeDocument { Id = 20, SiteId = 7, Alias = "species-profile", Name = "Species profile" });
        harness.Session.Store(
            new ContentTranslationGroupDocument
            {
                Id = 1000, SiteId = 7, ContentTypeAlias = "animal", SourceItemId = 100, SourceCulture = "en-US"
            },
            new ContentTranslationGroupDocument
            {
                Id = 2000, SiteId = 7, ContentTypeAlias = "species-profile", SourceItemId = 200, SourceCulture = "en-US"
            });
        harness.Session.Store(
            new ContentItem
            {
                Id = 200, SiteId = 7, ContentTypeAlias = "species-profile", Culture = "en-US", Slug = "wolf", Title = "Wolf", TranslationGroupId = 2000
            });
        harness.Session.Store(
            Barrier(1000),
            Barrier(2000));
        await harness.Session.SaveChangesAsync();
        var selectedSites = Substitute.For<ISelectedSiteScopeResolver>();
        selectedSites.ResolveAsync(7, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(5, 7));
        var barriers = new ContentRelationshipTargetBarrierCoordinator();
        var materializer = new ContentTypeReferenceRelationshipMaterializer(selectedSites, barriers);

        await using var relationSession = await harness.Store.OpenSessionAsync(new SessionOptions());
        await using var deletionSession = await harness.Store.OpenSessionAsync(new SessionOptions());
        (await materializer.StageAsync(
            relationSession,
            Context(field, declaration, JsonSerializer.SerializeToElement("200"), revision: 1)))
            .IsSuccess.ShouldBeTrue();
        var deletionContext = new ContentTranslationGroupProjectionContext(
            7, "species-profile", 2000, 200, 0,
            new Dictionary<string, JsonElement>(),
            ContentTranslationGroupProjectionChange.Delete);
        (await barriers.StageSourceLifecycleAsync(deletionSession, deletionContext)).IsSuccess.ShouldBeTrue();

        await relationSession.SaveChangesAsync();
        await Should.ThrowAsync<ConcurrencyException>(() => deletionSession.SaveChangesAsync());

        await using var verify = await harness.Store.QuerySessionAsync();
        (await verify.Query<ContentReferenceRelation>().ToListAsync()).Count.ShouldBe(1);
        (await verify.LoadAsync<ContentTranslationGroupDocument>(2000)).ShouldNotBeNull();
    }

    [Test]
    public async Task Target_delete_committing_first_fences_out_a_staged_relationship_add()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(new ContentModule().Configure);
        await harness.InitializeAsync();
        var (field, declaration) = Declaration();
        harness.Session.Store(
            new ContentTypeDocument { Id = 10, SiteId = 7, Alias = "animal", Name = "Animal", Fields = [field] },
            new ContentTypeDocument { Id = 20, SiteId = 7, Alias = "species-profile", Name = "Species profile" });
        harness.Session.Store(
            new ContentTranslationGroupDocument
            {
                Id = 1000, SiteId = 7, ContentTypeAlias = "animal", SourceItemId = 100, SourceCulture = "en-US"
            },
            new ContentTranslationGroupDocument
            {
                Id = 2000, SiteId = 7, ContentTypeAlias = "species-profile", SourceItemId = 200, SourceCulture = "en-US"
            });
        harness.Session.Store(new ContentItem
        {
            Id = 200, SiteId = 7, ContentTypeAlias = "species-profile", Culture = "en-US", Slug = "wolf", Title = "Wolf", TranslationGroupId = 2000
        });
        harness.Session.Store(Barrier(1000), Barrier(2000));
        await harness.Session.SaveChangesAsync();
        var selectedSites = Substitute.For<ISelectedSiteScopeResolver>();
        selectedSites.ResolveAsync(7, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(5, 7));
        var barriers = new ContentRelationshipTargetBarrierCoordinator();
        var materializer = new ContentTypeReferenceRelationshipMaterializer(selectedSites, barriers);

        await using var relationSession = await harness.Store.OpenSessionAsync(new SessionOptions());
        (await materializer.StageAsync(
            relationSession,
            Context(field, declaration, JsonSerializer.SerializeToElement("200"), revision: 1)))
            .IsSuccess.ShouldBeTrue();

        await using (var deletionSession = await harness.Store.OpenSessionAsync(new SessionOptions()))
        {
            var contributor = new ContentReferenceRelationshipProjectionContributor([materializer], barriers);
            var service = new AeroContentService(deletionSession, null, [contributor]);
            (await service.DeleteAsync(7, 200)).IsSuccess.ShouldBeTrue();
        }

        await Should.ThrowAsync<ConcurrencyException>(() => relationSession.SaveChangesAsync());
        await using var verify = await harness.Store.QuerySessionAsync();
        (await verify.LoadAsync<ContentTranslationGroupDocument>(2000)).ShouldBeNull();
        (await verify.Query<ContentReferenceRelation>().ToListAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task Self_reference_is_removed_atomically_when_its_only_group_is_deleted()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(new ContentModule().Configure);
        await harness.InitializeAsync();
        var field = new ContentFieldDefinition
        {
            Name = "parentTopic",
            Label = "Parent topic",
            FieldType = ContentFieldTypes.Reference,
            LocalizationMode = ContentFieldLocalizationMode.Shared,
            Settings = new Dictionary<string, JsonElement>
            {
                [ReferenceContentFieldSettings.TargetContentTypeId] = JsonSerializer.SerializeToElement("10"),
                [ReferenceContentFieldSettings.RelationshipAlias] = JsonSerializer.SerializeToElement("topic_parent")
            }
        };
        ContentReferenceRelationshipDeclaration.TryCreate("topic", field, out var declaration).ShouldBeTrue();
        harness.Session.Store(new ContentTypeDocument
        {
            Id = 10, SiteId = 7, Alias = "topic", Name = "Topic", Fields = [field]
        });
        harness.Session.Store(
            new ContentTranslationGroupDocument
            {
                Id = 1000, SiteId = 7, ContentTypeAlias = "topic", SourceItemId = 100, SourceCulture = "en-US"
            });
        harness.Session.Store(new ContentItem
        {
            Id = 100, SiteId = 7, ContentTypeAlias = "topic", Culture = "en-US", Slug = "root", Title = "Root", TranslationGroupId = 1000
        });
        harness.Session.Store(Barrier(1000));
        await harness.Session.SaveChangesAsync();
        var selectedSites = Substitute.For<ISelectedSiteScopeResolver>();
        selectedSites.ResolveAsync(7, Arg.Any<CancellationToken>())
            .Returns(new SelectedSiteScope(5, 7));
        var barriers = new ContentRelationshipTargetBarrierCoordinator();
        var materializer = new ContentTypeReferenceRelationshipMaterializer(selectedSites, barriers);
        var context = new ContentReferenceRelationshipProjectionContext(
            new ContentTranslationGroupProjectionContext(
                7, "topic", 1000, 100, 1,
                new Dictionary<string, JsonElement>(),
                ContentTranslationGroupProjectionChange.Upsert),
            declaration!,
            field,
            JsonSerializer.SerializeToElement("100"));
        (await materializer.StageAsync(harness.Session, context)).IsSuccess.ShouldBeTrue();
        await harness.Session.SaveChangesAsync();

        await using var deletionSession = await harness.Store.OpenSessionAsync(new SessionOptions());
        var contributor = new ContentReferenceRelationshipProjectionContributor([materializer], barriers);
        var service = new AeroContentService(deletionSession, null, [contributor]);
        (await service.DeleteAsync(7, 100)).IsSuccess.ShouldBeTrue();

        await using var verify = await harness.Store.QuerySessionAsync();
        (await verify.Query<ContentReferenceRelation>().ToListAsync()).ShouldBeEmpty();
        (await verify.LoadAsync<ContentTranslationGroupDocument>(1000)).ShouldBeNull();
    }

    private static ContentReferenceRelationshipProjectionContext Context(
        ContentFieldDefinition field,
        ContentReferenceRelationshipDeclaration declaration,
        JsonElement? value,
        int revision) => new(
        new ContentTranslationGroupProjectionContext(
            7,
            "animal",
            1000,
            100,
            revision,
            new Dictionary<string, JsonElement>(),
            ContentTranslationGroupProjectionChange.Upsert),
        declaration,
        field,
        value);

    private static (ContentFieldDefinition Field, ContentReferenceRelationshipDeclaration Declaration) Declaration()
    {
        var field = new ContentFieldDefinition
        {
            Name = "speciesProfile",
            Label = "Species profile",
            FieldType = ContentFieldTypes.Reference,
            LocalizationMode = ContentFieldLocalizationMode.Shared,
            Settings = new Dictionary<string, JsonElement>
            {
                [ReferenceContentFieldSettings.TargetContentTypeId] = JsonSerializer.SerializeToElement("20"),
                [ReferenceContentFieldSettings.RelationshipAlias] = JsonSerializer.SerializeToElement("animal_species_profile")
            }
        };
        ContentReferenceRelationshipDeclaration.TryCreate("animal", field, out var declaration).ShouldBeTrue();
        return (field, declaration!);
    }

    private static ContentRelationshipTargetBarrier Barrier(long translationGroupId) => new()
    {
        Id = translationGroupId,
        SiteId = 7,
        TranslationGroupId = translationGroupId
    };
}
