using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core.Railway;
using Shouldly;

namespace Aero.Cms.Core.Tests.Pages;

public sealed class SharpTsExecutorTests
{
    [Test]
    public async Task Html_tag_renders_context_and_escapes_scalar_substitutions()
    {
        var executor = new SharpTsExecutor();
        var metadata = new PageRenderMetadata(
            9_007_199_254_740_993,
            9_007_199_254_740_995,
            "aero.sharpts",
            "<Aero>",
            "aero",
            "/aero",
            "en-US");

        var result = await executor.ExecuteAsync(
            """
            export function render(context: any) {
                return html`<main data-page="${context.page.id}"><h1>${context.page.title}</h1></main>`;
            }
            """,
            SharpTsRenderContext.Create(
                metadata,
                PageContentQueryResolution.Empty,
                isPreview: true),
            10_000);

        var success = result.ShouldBeOfType<Result<string>.Ok>();
        success.Value.ShouldBe(
            "<main data-page=\"9007199254740993\"><h1>&lt;Aero&gt;</h1></main>");
    }

    [Test]
    public async Task Rendering_profile_rejects_imports_before_interpretation()
    {
        var executor = new SharpTsExecutor();
        var metadata = new PageRenderMetadata(
            null,
            1,
            "aero.sharpts",
            "New Page",
            "new-page",
            "/new-page",
            "en-US");

        var result = await executor.ExecuteAsync(
            """
            import { File } from "dotnet:System.IO.File";
            export function render(context: any) {
                return html`<p>${File.readAllText("secret.txt")}</p>`;
            }
            """,
            SharpTsRenderContext.Create(
                metadata,
                PageContentQueryResolution.Empty,
                isPreview: true),
            10_000);

        result.ShouldBeOfType<Result<string>.Failure>()
            .Error.ToString()
            .ShouldContain("Imports are not available");
    }
}
