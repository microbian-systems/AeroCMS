using Aero.Cms.Abstractions.Models;
using Aero.Cms.Html;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Projects persisted native style settings into the manager-facing contract.
/// </summary>
internal static class SiteStyleProfileMapper
{
    /// <summary>
    /// Copies the revision, breakpoint, and ordered color tokens into a detached view model.
    /// </summary>
    /// <param name="settings">The normalized persisted settings to project.</param>
    /// <returns>A view model with newly allocated color-token entries.</returns>
    public static SiteStyleProfileViewModel ToViewModel(StyleProfileSettings settings)
    {
        return new SiteStyleProfileViewModel
        {
            Revision = settings.Revision,
            SmallScreenBreakpointRem = settings.SmallScreenBreakpointRem,
            ColorTokens = settings.ColorTokens
                .Select(static token => new SiteStyleColorTokenViewModel
                {
                    Name = token.Name,
                    HexValue = token.HexValue
                })
                .ToList()
        };
    }
}
