using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Aero.Cms.Modules.Analytics.ViewComponents;

public sealed class SeoScriptsViewComponent(ISeoScriptRenderer renderer) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string placement)
    {
        if (!Enum.TryParse<SeoScriptPlacement>(placement, ignoreCase: true, out var parsed))
        {
            return new HtmlContentViewComponentResult(HtmlString.Empty);
        }

        var content = await renderer.RenderAsync(parsed, HttpContext.RequestAborted);
        return new HtmlContentViewComponentResult(content);
    }
}
