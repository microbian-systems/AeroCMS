using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Modules.Posts.Areas.Api.v1;
using Aero.Cms.Modules.Posts.Models;
using Aero.Core.Http;
using AeroDB.Sable;
using Alba;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public class CategoriesApiTests
{
    [Test]
    public async Task GetAllCategories_ShouldReturnOk()
    {
        // Setup real in-memory SurrealDB
        await using var harness = new SableTestHarness()
            .WithSchema<Category>();
        await harness.InitializeAsync();

        await using var host = AlbaHost.For(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Register the real SurrealDB session
                services.AddSingleton<IDocumentSession>(harness.Session);
                services.AddSingleton<IQuerySession>(harness.Session);

                // Stub dependencies required by the CategoriesApi handlers
                services.AddSingleton<IAeroCategoryActor>(new StubCategoryActor());
                services.AddSingleton<ISiteContext>(new StubSiteContext());

                services.AddLogging();
                services.AddRouting();
            });

            builder.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapCategoriesApi();
                });
            });
        });

        // Verify the route works with real in-memory data
        var result = await host.Scenario(s =>
        {
            s.Get.Url("/api/v1/admin/categories");
            s.StatusCodeShouldBe(200);
        });
    }

    /// <summary>
    /// Minimal ISiteContext stub for testing.
    /// </summary>
    private sealed class StubSiteContext : ISiteContext
    {
        public long SiteId => 0;
        public long TenantId => 0;
    }

    /// <summary>
    /// Minimal IAeroCategoryActor stub for testing.
    /// Only GetAllAsync is wired to return an empty list — all other methods
    /// throw NotSupportedException since they are not exercised by this test.
    /// </summary>
    private sealed class StubCategoryActor : IAeroCategoryActor
    {
        // IAeroCategoryActor
        public Task<List<CategoryViewModel>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(new List<CategoryViewModel>());

        // ICruddable<CategoryViewModel, long>
        public Task<AeroRequestResponse<CategoryViewModel>> GetByIdAsync(long id, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<AeroRequestResponse<CategoryViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<AeroRequestResponse<CategoryViewModel>> CreateAsync(Aero.Core.Commands.IRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<AeroRequestResponse<CategoryViewModel>> UpdateAsync(Aero.Core.Commands.IRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<AeroRequestResponse<CategoryViewModel>> DeleteAsync(Aero.Core.Commands.IRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        // ICanFindBySite<CategoryViewModel, long>
        public Task<AeroRequestResponse<CategoryViewModel>> GetBySiteIdAsync(long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
            => throw new NotSupportedException();

        // ICanFindBySlug<CategoryViewModel, long>
        public Task<AeroRequestResponse<CategoryViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct = default)
            => throw new NotSupportedException();

        // ICanFindBySlug<CategoryViewModel, string>
        public Task<AeroRequestResponse<CategoryViewModel>> GetBySlugAsync(string siteId, string slug, CancellationToken ct = default)
            => throw new NotSupportedException();

        // IHaveState<CategoryViewModel>
        public Task<CategoryViewModel> GetStateAsync(CancellationToken ct)
            => throw new NotSupportedException();
        public Task UpdateStateAsync(CategoryViewModel state, CancellationToken ct)
            => throw new NotSupportedException();

        // IGrainWithIntegerCompoundKey (from IAeroActor : IGrainWithIntegerCompoundKey)
        public long PrimaryKey => 0;
        public string KeyExtension => string.Empty;
    }
}
