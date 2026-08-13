using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Importing;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Infrastructure;
using Aero.Cms.Modules.Jobs;
using Aero.Core;
using Aero.Core.Railway;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content.Importing;

public sealed class ContentImportCoordinatorTests
{
    [Test]
    public async Task Stale_lease_has_no_provider_or_provisioning_side_effects()
    {
        var fixture = new Fixture();
        fixture.Jobs.LoadAsync(fixture.Lease.JobId, Arg.Any<CancellationToken>()).Returns(fixture.Job with { LeaseToken = "other" });

        var result = await fixture.Coordinator.ExecuteAsync(fixture.Lease);

        result.Succeeded.ShouldBeFalse();
        fixture.Provider.PlanCalls.ShouldBe(0);
        fixture.Provider.ImportCalls.ShouldBe(0);
        fixture.Types.SaveCalls.ShouldBe(0);
    }

    [Test]
    public async Task Manager_drift_is_terminal_and_prevents_import()
    {
        var fixture = new Fixture();
        var desired = Type("species");
        fixture.Provider.Plan = new ContentImportProvisioningPlan([desired], []);
        fixture.Types.Existing = new ContentTypeDefinition { SiteId = 7, Alias = "species", Name = "Manager edit" };

        var result = await fixture.Coordinator.ExecuteAsync(fixture.Lease);

        result.Succeeded.ShouldBeFalse();
        result.FailureDisposition.ShouldBe(ContentImportFailureDisposition.Terminal);
        fixture.Provider.ImportCalls.ShouldBe(0);
        fixture.Types.SaveCalls.ShouldBe(0);
    }

    [Test]
    public async Task Invalid_or_duplicate_plan_is_terminal_before_any_save()
    {
        var fixture = new Fixture();
        fixture.Provider.Plan = new ContentImportProvisioningPlan([Type(""), Type("")], []);

        var result = await fixture.Coordinator.ExecuteAsync(fixture.Lease);

        result.Succeeded.ShouldBeFalse();
        result.FailureDisposition.ShouldBe(ContentImportFailureDisposition.Terminal);
        fixture.Types.GetCalls.ShouldBe(0);
        fixture.Types.SaveCalls.ShouldBe(0);
        fixture.Provider.ImportCalls.ShouldBe(0);
    }

    [Test]
    public async Task Out_of_scope_plan_is_terminal_before_any_save()
    {
        var fixture = new Fixture();
        fixture.Provider.Plan = new ContentImportProvisioningPlan([new ContentTypeDefinition { SiteId = 99, Alias = "species", Name = "Species" }], []);

        var result = await fixture.Coordinator.ExecuteAsync(fixture.Lease);

        result.Succeeded.ShouldBeFalse();
        result.FailureDisposition.ShouldBe(ContentImportFailureDisposition.Terminal);
        fixture.Types.GetCalls.ShouldBe(0);
        fixture.Types.SaveCalls.ShouldBe(0);
        fixture.Provider.ImportCalls.ShouldBe(0);
    }

    [Test]
    public async Task Generated_template_reference_index_and_default_cache_duration_replay_without_drift()
    {
        var fixture = new Fixture();
        var expected = Type("species");
        expected.Fields = [new ContentFieldDefinition { Name = "source", FieldType = "reference", Indexed = false }];
        var actual = Type("species");
        actual.ScribanTemplate = "server-generated";
        actual.Fields = [new ContentFieldDefinition { Name = "source", FieldType = "reference", Indexed = true }];
        var expectedView = View("species");
        var actualView = expectedView with { CacheDuration = TimeSpan.FromMinutes(5), CacheGeneration = 22, PublicExecutionEligible = true, PublicExecutionIneligibilityReason = null };
        fixture.Provider.Plan = new ContentImportProvisioningPlan([expected], [expectedView]);
        fixture.Types.Existing = actual;
        fixture.Views.LoadPublishedAsync(new ContentViewScope(3, 7), "species", Arg.Any<CancellationToken>()).Returns(Task.FromResult<ContentSurrealViewRevision?>(actualView));

        var result = await fixture.Coordinator.ExecuteAsync(fixture.Lease);

        result.Succeeded.ShouldBeTrue();
        fixture.Types.SaveCalls.ShouldBe(0);
        fixture.Provider.ImportCalls.ShouldBe(1);
        await fixture.Views.Received(1).InvalidateAsync(new ContentViewScope(3, 7), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Retryable_provider_failure_preserves_its_checkpoint_and_progress_for_worker_finalization()
    {
        var fixture = new Fixture();
        fixture.Provider.Import = new ContentImportProviderResult(false, "row-20", 20, 40, "temporary", ContentImportFailureDisposition.Retryable);

        var result = await fixture.Coordinator.ExecuteAsync(fixture.Lease);

        result.Succeeded.ShouldBeFalse();
        result.FailureDisposition.ShouldBe(ContentImportFailureDisposition.Retryable);
        result.Checkpoint.ShouldBe("row-20");
        result.ProgressCurrent.ShouldBe(20);
        result.ProgressTotal.ShouldBe(40);
    }

    [Test]
    public async Task Successful_provider_checkpoint_and_progress_are_forwarded_to_the_durable_job()
    {
        var fixture = new Fixture();

        var result = await fixture.Coordinator.ExecuteAsync(fixture.Lease);

        result.Succeeded.ShouldBeTrue();
        result.Checkpoint.ShouldBe("done");
        result.ProgressCurrent.ShouldBe(1);
        result.ProgressTotal.ShouldBe(1);
        await fixture.Jobs.Received(1).ReportAsync(fixture.Lease, "done", 1, 1, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Replay_without_progress_preserves_durable_progress_and_accepts_its_checkpoint()
    {
        var fixture = new Fixture(progressCurrent: 109);
        fixture.Provider.Import = ContentImportProviderResult.Success("catalogue-of-life:completed");

        var result = await fixture.Coordinator.ExecuteAsync(fixture.Lease);

        result.Succeeded.ShouldBeTrue();
        result.Checkpoint.ShouldBe("catalogue-of-life:completed");
        result.ProgressCurrent.ShouldBe(109);
        result.ProgressTotal.ShouldBeNull();
        await fixture.Jobs.Received(1).ReportAsync(fixture.Lease, "catalogue-of-life:completed", 109, null, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Activation_without_progress_preserves_imported_progress_and_uses_its_checkpoint()
    {
        var fixture = new Fixture(activate: true);
        fixture.Provider.Import = ContentImportProviderResult.Success("catalogue-of-life:completed", 109, 200);
        fixture.Provider.Activation = ContentImportProviderResult.Success("catalogue-of-life:active");

        var result = await fixture.Coordinator.ExecuteAsync(fixture.Lease);

        result.Succeeded.ShouldBeTrue();
        result.Checkpoint.ShouldBe("catalogue-of-life:active");
        result.ProgressCurrent.ShouldBe(109);
        result.ProgressTotal.ShouldBe(200);
        await fixture.Jobs.Received(1).ReportAsync(fixture.Lease, "catalogue-of-life:active", 109, 200, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Activation_with_explicit_progress_supersedes_imported_progress()
    {
        var fixture = new Fixture(activate: true);
        fixture.Provider.Import = ContentImportProviderResult.Success("catalogue-of-life:completed", 109, 200);
        fixture.Provider.Activation = ContentImportProviderResult.Success("catalogue-of-life:active", 110, 200);

        var result = await fixture.Coordinator.ExecuteAsync(fixture.Lease);

        result.Succeeded.ShouldBeTrue();
        result.Checkpoint.ShouldBe("catalogue-of-life:active");
        result.ProgressCurrent.ShouldBe(110);
        result.ProgressTotal.ShouldBe(200);
        await fixture.Jobs.Received(1).ReportAsync(fixture.Lease, "catalogue-of-life:active", 110, 200, Arg.Any<CancellationToken>());
    }

    private static ContentTypeDefinition Type(string alias) => new() { SiteId = 7, Alias = alias, Name = "Catalog item", ScribanTemplate = null };

    private static ContentSurrealViewRevision View(string alias) => new(
        11, new ContentViewScope(3, 7), alias, "catalog_read", "shape", "SELECT * FROM catalog_read LIMIT 50",
        "external_id", "display_name", 2, ContentViewPublicationState.Published, DateTimeOffset.UnixEpoch,
        CacheEnabled: true, CacheDuration: null, CacheGeneration: 0,
        EntrySelectStatement: "SELECT * FROM catalog_read WHERE external_id = $id LIMIT 1",
        SearchSelectStatement: "SELECT * FROM catalog_read WHERE display_name CONTAINS $search LIMIT 50");

    private sealed class Fixture
    {
        public ContentImportLease Lease { get; } = new(5, "lease", 2, DateTimeOffset.UtcNow.AddMinutes(1));
        public ContentImportJob Job { get; }
        public IContentImportJobStore Jobs { get; } = Substitute.For<IContentImportJobStore>();
        public FakeContentTypes Types { get; } = new();
        public IContentSurrealViewService Views { get; } = Substitute.For<IContentSurrealViewService>();
        public TestImporter Provider { get; } = new();
        public IContentImportCoordinator Coordinator { get; }

        public Fixture(bool activate = false, long progressCurrent = 0, long? progressTotal = null)
        {
            Job = new ContentImportJob(5, "identity", 3, Request(activate), ContentImportJobState.Running, 1, null, progressCurrent, progressTotal, null, "lease", 2, DateTimeOffset.UtcNow.AddMinutes(1), null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            Jobs.LoadAsync(5, Arg.Any<CancellationToken>()).Returns(Job);
            Jobs.ReportAsync(Arg.Any<ContentImportLease>(), Arg.Any<string?>(), Arg.Any<long>(), Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(true);
            var sites = Substitute.For<ISelectedSiteScopeResolver>();
            sites.ResolveAsync(7, Arg.Any<CancellationToken>()).Returns(new SelectedSiteScope(3, 7));
            Coordinator = new ContentImportCoordinator([Provider], Jobs, sites, Types, Views);
        }

        private static ContentImportRequest Request(bool activate) => new(7, "test", "1", "source", "selection", "{}", "system:test", activate);
    }

    private sealed class FakeContentTypes : IContentTypeService
    {
        public ContentTypeDefinition? Existing { get; set; }
        public int GetCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public Task<Result<ContentTypeDefinition, AeroError>> GetByIdAsync(long siteId, long id, CancellationToken ct = default) => GetByAliasAsync(siteId, string.Empty, ct);
        public Task<Result<ContentTypeDefinition, AeroError>> GetByAliasAsync(long siteId, string alias, CancellationToken ct = default)
        {
            GetCalls++;
            return Task.FromResult<Result<ContentTypeDefinition, AeroError>>(Existing is null
                ? new Result<ContentTypeDefinition>.Failure(AeroError.NotFoundError("missing"))
                : new Result<ContentTypeDefinition, AeroError>.Ok(Existing));
        }
        public Task<Result<IReadOnlyList<ContentTypeDefinition>, AeroError>> GetAllAsync(long siteId, CancellationToken ct = default)
            => Task.FromResult<Result<IReadOnlyList<ContentTypeDefinition>, AeroError>>(new Result<IReadOnlyList<ContentTypeDefinition>, AeroError>.Ok([]));
        public Task<Result<ContentTypeDefinition, AeroError>> SaveAsync(ContentTypeDefinition definition, CancellationToken ct = default)
        {
            SaveCalls++;
            return Task.FromResult<Result<ContentTypeDefinition, AeroError>>(new Result<ContentTypeDefinition, AeroError>.Ok(definition));
        }
        public Task<Result<bool, AeroError>> DeleteAsync(long siteId, string alias, CancellationToken ct = default)
            => Task.FromResult<Result<bool, AeroError>>(new Result<bool, AeroError>.Ok(true));
    }

    private sealed class TestImporter : IContentTypeImporter
    {
        public ContentTypeImporterDescriptor Descriptor { get; } = new("test", "Test", "1");
        public ContentImportProvisioningPlan Plan { get; set; } = ContentImportProvisioningPlan.Empty;
        public ContentImportProviderResult Import { get; set; } = ContentImportProviderResult.Success("done", 1, 1);
        public ContentImportProviderResult Activation { get; set; } = ContentImportProviderResult.Success();
        public int PlanCalls { get; private set; }
        public int ImportCalls { get; private set; }
        public Task<ContentImportProvisioningPlan> PlanAsync(ContentImportContext context, CancellationToken ct = default) { PlanCalls++; return Task.FromResult(Plan); }
        public Task<ContentImportProviderResult> ImportAsync(ContentImportExecutionContext context, CancellationToken ct = default) { ImportCalls++; return Task.FromResult(Import); }
        public Task<ContentImportProviderResult> ActivateAsync(ContentImportExecutionContext context, CancellationToken ct = default) => Task.FromResult(Activation);
    }
}
