using System.Reflection;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages.Areas.Api.v1;
using TUnit.Core;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class PagesApiTests
{
    [Test]
    public async Task MapToDetail_UsesCreatedOnWhenModifiedOnIsMissing()
    {
        var createdOn = new DateTimeOffset(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);
        var page = new PageDocument
        {
            Id = 1501703887826436096,
            SiteId = 1501703887469527040,
            Title = "Seeded page",
            Slug = "seeded-page",
            CreatedOn = createdOn,
            ModifiedOn = null,
            PublicationState = ContentPublicationState.Published
        };

        var mapper = typeof(PagesAdminApi).GetMethod("MapToDetail", BindingFlags.NonPublic | BindingFlags.Static);

        await Assert.That(mapper).IsNotNull();

        var detail = (PageDetail)mapper!.Invoke(null, [page])!;

        await Assert.That(detail.UpdatedAt).IsEqualTo(createdOn.DateTime);
    }
}
