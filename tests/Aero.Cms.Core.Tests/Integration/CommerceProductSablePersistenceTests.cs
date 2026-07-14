using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using AeroDB.Sable;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class CommerceProductSablePersistenceTests
{
    [Test]
    public async Task Commerce_module_defines_its_full_text_analyzer_before_product_persistence()
    {
        var module = new CommerceModule();
        await using var harness = new SableTestHarness()
            .WithConfiguration(module.Configure);
        await harness.InitializeAsync();

        harness.Session.Store(new ProductDocument
        {
            Id = 91_001,
            Name = "Starter Theme",
            Slug = "starter-theme",
            Description = "A polished starter theme",
            Price = 49m,
            IsPublished = true
        });

        await harness.Session.SaveChangesAsync();

        await using var verificationSession = await harness.OpenSessionAsync();
        var saved = (await verificationSession.Query<ProductDocument>().ToListAsync())
            .SingleOrDefault(product => product.Id == 91_001);
        saved.ShouldNotBeNull();
        saved.Name.ShouldBe("Starter Theme");
    }
}
