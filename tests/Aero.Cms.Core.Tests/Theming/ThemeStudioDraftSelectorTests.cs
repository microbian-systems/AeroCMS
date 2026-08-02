using Aero.Cms.Abstractions.Theming;
using Aero.Cms.Shared.Pages.Manager.Theming;

namespace Aero.Cms.Core.Tests.Theming;

public sealed class ThemeStudioDraftSelectorTests
{
    [Test]
    public async Task Finds_exact_assigned_draft_when_it_is_not_first()
    {
        ThemeDefinitionView[] drafts =
        [
            Draft(41, "First", "first"),
            Draft(73, "Assigned", "assigned"),
            Draft(99, "Last", "last")
        ];

        var selected = ThemeStudioDraftSelector.FindAssigned(
            12,
            "tenant-12-theme-73",
            drafts);

        await Assert.That(selected).IsSameReferenceAs(drafts[1]);
    }

    [Test]
    public async Task Does_not_match_a_draft_from_another_tenant()
    {
        ThemeDefinitionView[] drafts = [Draft(73, "Assigned", "assigned")];

        var selected = ThemeStudioDraftSelector.FindAssigned(
            12,
            "tenant-13-theme-73",
            drafts);

        await Assert.That(selected).IsNull();
    }

    private static ThemeDefinitionView Draft(long id, string name, string slug) =>
        new(id, name, slug, null, new ThemeTokenSet(), 1, false, []);
}
