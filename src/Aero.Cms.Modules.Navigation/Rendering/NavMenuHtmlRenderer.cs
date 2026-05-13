using System.Text;
using System.Text.Encodings.Web;
using System.Globalization;
using Aero.Cms.Modules.Navigation.Domain;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Modules.Navigation.Rendering;

public enum NavMenuRenderMode
{
    Desktop,
    Mobile
}

public interface INavMenuComponentVisitor<out TResult>
{
    TResult Visit(NavLink link);
    TResult Visit(NavMenu menu);
    TResult Visit(NavSearch search);
    TResult Visit(NavHtml html);
}

public interface INavMenuHtmlRenderer
{
    IHtmlContent Render(INavMenuComponent component, NavMenuRenderMode mode);
}

public sealed class NavMenuHtmlRenderer : INavMenuHtmlRenderer
{
    public IHtmlContent Render(INavMenuComponent component, NavMenuRenderMode mode)
    {
        var visitor = new HtmlVisitor(mode);
        return component switch
        {
            NavLink link => visitor.Visit(link),
            NavMenu menu => visitor.Visit(menu),
            NavSearch search => visitor.Visit(search),
            NavHtml html => visitor.Visit(html),
            _ => HtmlString.Empty
        };
    }

    private sealed class HtmlVisitor(NavMenuRenderMode mode) : INavMenuComponentVisitor<IHtmlContent>
    {
        public IHtmlContent Visit(NavLink link)
        {
            var css = mode == NavMenuRenderMode.Mobile
                ? "block px-4 py-4 rounded-2xl text-lg font-bold text-slate-700 hover:bg-indigo-50 hover:text-indigo-600 transition-all duration-200"
                : "px-4 py-2 rounded-lg text-sm font-semibold text-slate-600 hover:text-indigo-600 hover:bg-slate-50 transition-all duration-200";
            var target = link.OpenInNewTab ? " target=\"_blank\" rel=\"noopener noreferrer\"" : string.Empty;

            return new HtmlString(
                $"<a href=\"{Encode(link.Href)}\" class=\"{css}\"{target}>{Encode(link.Label)}</a>");
        }

        public IHtmlContent Visit(NavMenu menu)
        {
            var builder = new StringBuilder();
            var wrapperClass = mode == NavMenuRenderMode.Mobile ? "space-y-2" : "relative group";
            var labelClass = mode == NavMenuRenderMode.Mobile
                ? "block px-4 py-4 rounded-2xl text-lg font-bold text-slate-700"
                : "px-4 py-2 rounded-lg text-sm font-semibold text-slate-600";
            var childWrapperClass = mode == NavMenuRenderMode.Mobile
                ? "ml-4 border-l border-slate-200 pl-2 space-y-2"
                : "absolute left-0 top-full hidden min-w-48 rounded-lg border border-slate-200 bg-white p-2 shadow-xl group-hover:block";

            builder.Append(CultureInfo.InvariantCulture, $"<div class=\"{wrapperClass}\">");
            builder.Append(CultureInfo.InvariantCulture, $"<span class=\"{labelClass}\">{Encode(menu.Label)}</span>");
            builder.Append(CultureInfo.InvariantCulture, $"<div class=\"{childWrapperClass}\">");
            foreach (var child in menu.Children)
            {
                builder.Append(RenderToString(child));
            }

            builder.Append("</div></div>");
            return new HtmlString(builder.ToString());
        }

        public IHtmlContent Visit(NavSearch search)
        {
            var css = mode == NavMenuRenderMode.Mobile
                ? "block w-full rounded-xl border border-slate-200 px-4 py-3 text-base"
                : "rounded-lg border border-slate-200 px-3 py-2 text-sm";

            return new HtmlString(
                $"<form action=\"{Encode(search.SearchAction)}\" method=\"get\"><input name=\"q\" class=\"{css}\" placeholder=\"{Encode(search.Placeholder)}\" /></form>");
        }

        public IHtmlContent Visit(NavHtml html)
            => new HtmlString(html.Html);

        private string RenderToString(INavMenuComponent component)
        {
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            var rendered = component switch
            {
                NavLink link => Visit(link),
                NavMenu menu => Visit(menu),
                NavSearch search => Visit(search),
                NavHtml html => Visit(html),
                _ => HtmlString.Empty
            };
            rendered.WriteTo(writer, HtmlEncoder.Default);
            return writer.ToString();
        }

        private static string Encode(string value)
            => HtmlEncoder.Default.Encode(value);
    }
}
