using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Indexing;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using NSubstitute;
using System.Text.Json;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentLocalizationFenceTests
{
    [Test]
    public async Task First_group_projection_commits_in_the_content_unit_of_work()
    {
        var contributor = new ProjectionContributor(fail: false);
        await using var fixture = await CreateFixtureAsync(sourceTranslationGroupId: null, contributor: contributor);

        var result = await fixture.Handler.ForkAsync(
            fixture.Context,
            new ContentCultureForkCommand(
                fixture.SourceId,
                "fr-FR",
                "loup",
                ExpectedGroupStorageVersion: null,
                ExpectedSourceStorageVersion: 0));

        await Assert.That(result.IsSuccess).IsTrue().Because(result.ToString());
        await using var verify = await fixture.Harness.Store.QuerySessionAsync();
        var marker = await verify.LoadAsync<ProjectionMarker>(fixture.SourceId);
        await Assert.That(marker).IsNotNull();
        await Assert.That(marker!.SiteId).IsEqualTo(fixture.Context.SiteId);
        await Assert.That(marker.ContentTypeAlias).IsEqualTo("animal");
        await Assert.That(marker.Change).IsEqualTo(ContentTranslationGroupProjectionChange.Upsert);
        await Assert.That(await verify.LoadAsync<ContentTranslationGroupDocument>(fixture.SourceId)).IsNotNull();
    }

    [Test]
    public async Task Failed_first_group_projection_rolls_back_the_content_unit_of_work()
    {
        var contributor = new ProjectionContributor(fail: true);
        await using var fixture = await CreateFixtureAsync(sourceTranslationGroupId: null, contributor: contributor);

        var result = await fixture.Handler.ForkAsync(
            fixture.Context,
            new ContentCultureForkCommand(
                fixture.SourceId,
                "fr-FR",
                "loup",
                ExpectedGroupStorageVersion: null,
                ExpectedSourceStorageVersion: 0));

        await Assert.That(result.IsFailure).IsTrue();
        await using var verify = await fixture.Harness.Store.QuerySessionAsync();
        await Assert.That(await verify.LoadAsync<ProjectionMarker>(fixture.SourceId)).IsNull();
        await Assert.That(await verify.LoadAsync<ContentTranslationGroupDocument>(fixture.SourceId)).IsNull();
        await Assert.That(await verify.Query<ContentItem>()
                .Where(item => item.SiteId == fixture.Context.SiteId && item.Culture == "fr-FR")
                .AnyAsync())
            .IsFalse();
    }

    [Test]
    public async Task Shared_field_update_stages_the_host_projection_before_the_group_commit()
    {
        var contributor = new ProjectionContributor(fail: false);
        await using var fixture = await CreateFixtureAsync(sourceTranslationGroupId: 100, contributor: contributor);

        var result = await fixture.Handler.UpdateSharedFieldsAsync(
            fixture.Context,
            new UpdateContentTranslationSharedFieldsCommand(
                100,
                ExpectedGroupStorageVersion: 0,
                ExpectedGroupRevision: 0,
                new Dictionary<string, JsonElement>()));

        await Assert.That(result.IsSuccess).IsTrue().Because(result.ToString());
        await using var verify = await fixture.Harness.Store.QuerySessionAsync();
        var marker = await verify.LoadAsync<ProjectionMarker>(100);
        var group = await verify.LoadAsync<ContentTranslationGroupDocument>(100);
        await Assert.That(marker).IsNotNull();
        await Assert.That(marker!.Change).IsEqualTo(ContentTranslationGroupProjectionChange.Upsert);
        await Assert.That(group).IsNotNull();
        await Assert.That(group!.Revision).IsEqualTo(1);
    }

    [Test]
    public async Task Source_delete_stages_the_host_projection_with_the_group_delete()
    {
        var contributor = new ProjectionContributor(fail: false);
        await using var fixture = await CreateFixtureAsync(sourceTranslationGroupId: 100, contributor: contributor);

        var result = await fixture.Writable.DeleteAsync(fixture.Context.SiteId, fixture.SourceId);

        await Assert.That(result.IsSuccess).IsTrue().Because(result.ToString());
        await using var verify = await fixture.Harness.Store.QuerySessionAsync();
        var marker = await verify.LoadAsync<ProjectionMarker>(100);
        await Assert.That(marker).IsNotNull();
        await Assert.That(marker!.Change).IsEqualTo(ContentTranslationGroupProjectionChange.Delete);
        await Assert.That(await verify.LoadAsync<ContentTranslationGroupDocument>(100)).IsNull();
        await Assert.That(await verify.LoadAsync<ContentItem>(fixture.SourceId)).IsNull();
    }

    [Test]
    public async Task First_fork_uses_source_fence_and_insert_only_group_when_no_group_exists()
    {
        await using var fixture = await CreateFixtureAsync(sourceTranslationGroupId: null);

        var result = await fixture.Handler.ForkAsync(
            fixture.Context,
            new ContentCultureForkCommand(
                fixture.SourceId,
                "fr-FR",
                "loup",
                ExpectedGroupStorageVersion: null,
                ExpectedSourceStorageVersion: 0));

        await Assert.That(result.IsSuccess).IsTrue().Because(result.ToString());
        var success = (Result<ContentLocalizationOperationResult, AeroError>.Ok)result;
        await Assert.That(success.Value.SourceItemStorageVersion).IsEqualTo(1);
        await Assert.That(success.Value.TranslationGroupStorageVersion).IsEqualTo(0);

        await using var verify = await fixture.Harness.Store.QuerySessionAsync();
        var source = await verify.LoadAsync<ContentItem>(fixture.SourceId);
        var group = await verify.LoadAsync<ContentTranslationGroupDocument>(fixture.SourceId);
        var target = await verify.LoadAsync<ContentItem>(success.Value.ContentItemId);
        await Assert.That(source!.Version).IsEqualTo(success.Value.SourceItemStorageVersion);
        await Assert.That(group).IsNotNull();
        await Assert.That(group!.SourceItemId).IsEqualTo(fixture.SourceId);
        await Assert.That(target!.TranslationGroupId).IsEqualTo(group.Id);
    }

    [Test]
    public async Task Stale_source_or_group_token_fails_without_creating_a_variant()
    {
        await using var fixture = await CreateFixtureAsync(sourceTranslationGroupId: 100);

        var staleSource = await fixture.Handler.ForkAsync(
            fixture.Context,
            new ContentCultureForkCommand(
                fixture.SourceId,
                "fr-FR",
                "loup",
                ExpectedGroupStorageVersion: 0,
                ExpectedSourceStorageVersion: 1));
        var staleGroup = await fixture.Handler.ForkAsync(
            fixture.Context,
            new ContentCultureForkCommand(
                fixture.SourceId,
                "fr-FR",
                "loup",
                ExpectedGroupStorageVersion: 1,
                ExpectedSourceStorageVersion: 0));

        await Assert.That(staleSource.IsFailure).IsTrue();
        await Assert.That(staleGroup.IsFailure).IsTrue();
        await using var verify = await fixture.Harness.Store.QuerySessionAsync();
        await Assert.That(await verify.Query<ContentItem>()
                .Where(item => item.SiteId == fixture.Context.SiteId && item.Culture == "fr-FR")
                .AnyAsync())
            .IsFalse();
        var source = await verify.LoadAsync<ContentItem>(fixture.SourceId);
        var group = await verify.LoadAsync<ContentTranslationGroupDocument>(100);
        await Assert.That(source!.Version).IsEqualTo(0);
        await Assert.That(group!.Version).IsEqualTo(0);
    }

    [Test]
    public async Task Fork_rejects_a_source_from_another_site_before_any_fence_is_queued()
    {
        await using var fixture = await CreateFixtureAsync(sourceTranslationGroupId: null, sourceSiteId: 2);

        var result = await fixture.Handler.ForkAsync(
            fixture.Context,
            new ContentCultureForkCommand(
                fixture.SourceId,
                "fr-FR",
                "loup",
                ExpectedGroupStorageVersion: null,
                ExpectedSourceStorageVersion: 0));

        await Assert.That(result.IsFailure).IsTrue();
        await using var verify = await fixture.Harness.Store.QuerySessionAsync();
        var source = await verify.LoadAsync<ContentItem>(fixture.SourceId);
        await Assert.That(source!.Version).IsEqualTo(0);
    }

    [Test]
    public async Task Fork_maps_a_sable_concurrency_conflict_to_a_content_conflict()
    {
        var listener = new ConcurrencyConflictListener();
        await using var fixture = await CreateFixtureAsync(sourceTranslationGroupId: null, listener: listener);
        listener.Exception = new ConcurrencyException(typeof(ContentItem), fixture.SourceId, 0, 1);

        var result = await fixture.Handler.ForkAsync(
            fixture.Context,
            new ContentCultureForkCommand(
                fixture.SourceId,
                "fr-FR",
                "loup",
                ExpectedGroupStorageVersion: null,
                ExpectedSourceStorageVersion: 0));

        await Assert.That(result.IsFailure).IsTrue();
        await using var verify = await fixture.Harness.Store.QuerySessionAsync();
        await Assert.That(await verify.Query<ContentItem>()
                .Where(item => item.SiteId == fixture.Context.SiteId && item.Culture == "fr-FR")
                .AnyAsync())
            .IsFalse();
    }

    [Test]
    public async Task Fork_overwrite_commits_source_group_and_target_tokens_together()
    {
        await using var fixture = await CreateFixtureAsync(sourceTranslationGroupId: 100, includeTarget: true);

        var result = await fixture.Handler.ForkAsync(
            fixture.Context,
            new ContentCultureForkCommand(
                fixture.SourceId,
                "fr-FR",
                "loup-revised",
                OverwriteExisting: true,
                ExpectedGroupStorageVersion: 0,
                ExpectedTargetStorageVersion: 0,
                ExpectedSourceStorageVersion: 0));

        await Assert.That(result.IsSuccess).IsTrue().Because(result.ToString());
        var success = (Result<ContentLocalizationOperationResult, AeroError>.Ok)result;
        await Assert.That(success.Value.SourceItemStorageVersion).IsEqualTo(1);
        await Assert.That(success.Value.TranslationGroupStorageVersion).IsEqualTo(1);
        await Assert.That(success.Value.ContentItemStorageVersion).IsEqualTo(1);
    }

    [Test]
    public async Task Apply_translation_fences_the_source_and_group_with_the_target_update()
    {
        await using var fixture = await CreateFixtureAsync(sourceTranslationGroupId: 100, includeTarget: true);

        var result = await fixture.Handler.ApplyAiTranslationAsync(
            fixture.Context,
            new ApplyContentAiTranslationCommand(
                fixture.SourceId,
                SourceVersionNumber: 0,
                TargetItemId: fixture.TargetId,
                ExpectedTargetVersionNumber: 0,
                "en-US",
                "fr-FR",
                new Dictionary<string, JsonElement>(),
                "test-provider",
                "test-model",
                ExpectedSourceStorageVersion: 0,
                ExpectedTargetStorageVersion: 0,
                ExpectedGroupStorageVersion: 0));

        await Assert.That(result.IsSuccess).IsTrue().Because(result.ToString());
        var success = (Result<ContentLocalizationOperationResult, AeroError>.Ok)result;
        await Assert.That(success.Value.SourceItemStorageVersion).IsEqualTo(1);
        await Assert.That(success.Value.TranslationGroupStorageVersion).IsEqualTo(1);
        await Assert.That(success.Value.ContentItemStorageVersion).IsEqualTo(1);
    }

    [Test]
    public async Task Review_translation_fences_the_source_and_group_with_the_target_update()
    {
        await using var fixture = await CreateFixtureAsync(
            sourceTranslationGroupId: 100,
            includeTarget: true,
            sourceVersionNumber: 1,
            targetVersionNumber: 1,
            targetReview: ContentTranslationReview.Pending(),
            targetProvenance: new ContentTranslationProvenance(
                ContentTranslationOrigin.AiAssisted, "en-US", 1, DateTimeOffset.UtcNow));

        var result = await fixture.Handler.ReviewAsync(
            fixture.Context,
            new ReviewContentTranslationCommand(
                fixture.SourceId,
                SourceVersionNumber: 1,
                TargetItemId: fixture.TargetId,
                TargetVersionNumber: 1,
                Approved: true,
                ExpectedSourceStorageVersion: 0,
                ExpectedTargetStorageVersion: 0,
                ExpectedGroupStorageVersion: 0));

        await Assert.That(result.IsSuccess).IsTrue().Because(result.ToString());
        var success = (Result<ContentLocalizationOperationResult, AeroError>.Ok)result;
        await Assert.That(success.Value.SourceItemStorageVersion).IsEqualTo(1);
        await Assert.That(success.Value.TranslationGroupStorageVersion).IsEqualTo(1);
        await Assert.That(success.Value.ContentItemStorageVersion).IsEqualTo(1);
    }

    private static async Task<Fixture> CreateFixtureAsync(
        long? sourceTranslationGroupId,
        long sourceSiteId = 1,
        ConcurrencyConflictListener? listener = null,
        bool includeTarget = false,
        int sourceVersionNumber = 0,
        int targetVersionNumber = 0,
        ContentTranslationReview? targetReview = null,
        ContentTranslationProvenance? targetProvenance = null,
        IContentTranslationGroupProjectionContributor? contributor = null)
    {
        var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible)
            .WithSchema<ContentTypeDocument>(SchemaMode.Flexible)
            .WithSchema<ContentTranslationGroupDocument>(SchemaMode.Flexible)
            .WithSchema<ProjectionMarker>(SchemaMode.Flexible)
            .WithConfiguration(options =>
            {
                if (listener is not null)
                    options.Listeners.Add(listener);
            });
        await harness.InitializeAsync();

        const long sourceId = 100;
        const long targetId = 101;
        harness.Session.Store(new ContentTypeDocument
        {
            Id = 10,
            SiteId = 1,
            Alias = "animal",
            Name = "Animal",
            Structure = ContentStructure.Flat
        });
        harness.Session.Store(new ContentItem
        {
            Id = sourceId,
            SiteId = sourceSiteId,
            ContentTypeAlias = "animal",
            Culture = "en-US",
            Slug = "wolf",
            Title = "Wolf",
            TranslationGroupId = sourceTranslationGroupId,
            VersionNumber = sourceVersionNumber
        });
        if (sourceTranslationGroupId is { } groupId)
        {
            harness.Session.Store(new ContentTranslationGroupDocument
            {
                Id = groupId,
                SiteId = 1,
                ContentTypeAlias = "animal",
                SourceItemId = sourceId,
                SourceCulture = "en-US"
            });
        }
        if (includeTarget)
        {
            harness.Session.Store(new ContentItem
            {
                Id = targetId,
                SiteId = 1,
                ContentTypeAlias = "animal",
                Culture = "fr-FR",
                Slug = "loup",
                Title = "Wolf",
                TranslationGroupId = sourceTranslationGroupId,
                SourceItemId = sourceId,
                VersionNumber = targetVersionNumber,
                TranslationReview = targetReview ?? new ContentTranslationReview(),
                TranslationProvenance = targetProvenance
            });
        }
        await harness.Session.SaveChangesAsync();

        var typeService = Substitute.For<IContentTypeService>();
        typeService.GetByAliasAsync(1, "animal", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentTypeDefinition, AeroError>>(
                new Result<ContentTypeDefinition, AeroError>.Ok(new ContentTypeDefinition
                {
                    Id = 10,
                    SiteId = 1,
                    Alias = "animal",
                    Name = "Animal",
                    Structure = ContentStructure.Flat
                })));
        var contributors = contributor is null
            ? Array.Empty<IContentTranslationGroupProjectionContributor>()
            : [contributor];
        var writable = new AeroContentService(
            harness.Session,
            translationGroupProjectionContributors: contributors);
        var validation = new ContentValidationService(
            typeService,
            new ContentHierarchyValidator(harness.Session, typeService),
            [],
            []);
        return new Fixture(
            harness,
            new ContentLocalizationHandler(
                harness.Session,
                writable,
                writable,
                typeService,
                validation,
                contributors),
            writable,
            new ContentLocalizationContext(1, "en-US", ["en-US", "fr-FR"], ContentCultureFallbackPolicy.ExactOnly),
            sourceId,
            targetId);
    }

    private sealed record Fixture(
        SableTestHarness Harness,
        ContentLocalizationHandler Handler,
        AeroContentService Writable,
        ContentLocalizationContext Context,
        long SourceId,
        long TargetId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Harness.DisposeAsync();
    }

    private sealed class ConcurrencyConflictListener : IDocumentSessionListener
    {
        public ConcurrencyException? Exception { get; set; }

        public Task BeforeSaveChangesAsync(IDocumentSession session, CancellationToken ct) => Task.CompletedTask;

        public Task BeforeCommitAsync(IDocumentSession session, CancellationToken ct) =>
            Exception is null ? Task.CompletedTask : Task.FromException(Exception);
    }

    private sealed class ProjectionMarker : SableDocument
    {
        public long SiteId { get; set; }
        public string ContentTypeAlias { get; set; } = string.Empty;
        public ContentTranslationGroupProjectionChange Change { get; set; }
    }

    private sealed class ProjectionContributor(bool fail) : IContentTranslationGroupProjectionContributor
    {
        public Task<Result<NoneType, AeroError>> StageAsync(
            IDocumentSession session,
            ContentTranslationGroupProjectionContext context,
            CancellationToken cancellationToken = default)
        {
            session.Store(new ProjectionMarker
            {
                Id = context.TranslationGroupId,
                SiteId = context.SiteId,
                ContentTypeAlias = context.ContentTypeAlias,
                Change = context.Change
            });

            return Task.FromResult(fail
                ? Prelude.Fail<NoneType, AeroError>(AeroError.ValidationError(["Projection rejected the change."]))
                : Prelude.Ok<NoneType, AeroError>(default));
        }
    }
}
