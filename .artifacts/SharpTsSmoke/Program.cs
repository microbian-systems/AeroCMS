using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core.Railway;

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
    SharpTsRenderContext.Create(metadata, PageContentQueryResolution.Empty, true),
    10_000);

if (result is not Result<string>.Ok success)
{
    Console.Error.WriteLine(result);
    return 1;
}

const string expected =
    "<main data-page=\"9007199254740993\"><h1>&lt;Aero&gt;</h1></main>";
if (!string.Equals(success.Value, expected, StringComparison.Ordinal))
{
    Console.Error.WriteLine($"Unexpected output: {success.Value}");
    return 2;
}

Console.WriteLine(success.Value);

var denied = await executor.ExecuteAsync(
    """
    import { File } from "dotnet:System.IO.File";
    export function render(context: any) {
        return html`<p>${File.readAllText("secret.txt")}</p>`;
    }
    """,
    SharpTsRenderContext.Create(metadata, PageContentQueryResolution.Empty, true),
    10_000);
if (denied is not Result<string>.Failure)
{
    Console.Error.WriteLine("Forbidden import was not rejected.");
    return 3;
}

Console.WriteLine("Forbidden import rejected.");
return 0;
