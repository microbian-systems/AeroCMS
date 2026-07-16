using Aero.Cms.Abstractions.Models;
using Aero.Cms.Html;

namespace Aero.Cms.Modules.Sites;

internal static class SiteStyleProfileMapper
{
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
