namespace Aero.Cms.Core.Tests.Integration;

public sealed class ManagerHeaderSiteSelectionTests
{
    [Test]
    public async Task Empty_site_state_routes_the_visible_header_control_to_the_site_picker()
    {
        var root = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var markup = await File.ReadAllTextAsync(
            Path.Combine(root, "src", "Aero.Cms.Shared", "Layout", "ManagerHeader.razor"));

        await Assert.That(markup).Contains("@onclick=\"HandleHeaderSiteControlAsync\"");
        await Assert.That(markup).Contains("if (currentSite is null)");
        await Assert.That(markup).Contains("await ShowSitePickerAsync();");
        await Assert.That(markup).Contains("aria-expanded=\"@(currentSite is null ? _showSitePicker : _showNavDropdown)\"");
    }
}
