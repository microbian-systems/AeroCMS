using System.Collections.Immutable;
using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Core;
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
            export function render(context: AeroRenderContext): AeroHtmlFragment {
                return html`<main data-page="${context.page.id}"><h1>${context.page.title}</h1></main>`;
            }
            """,
            SharpTsRenderContext.Create(
                metadata,
                PageContentQueryResolution.Empty,
                isPreview: true),
            10_000);

        RequireSuccess(result).ShouldBe(
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
            export function render(context: AeroRenderContext): AeroHtmlFragment {
                return html`<p>${File.readAllText("secret.txt")}</p>`;
            }
            """,
            SharpTsRenderContext.Create(
                metadata,
                PageContentQueryResolution.Empty,
                isPreview: true),
            10_000);

        FormatError(result.ShouldBeOfType<Result<string>.Failure>().Error)
            .ShouldContain("is not allowed by rendering.safe-v1");
    }

    [Test]
    public async Task Aero_content_import_traverses_only_the_resolved_query_snapshot()
    {
        var executor = new SharpTsExecutor();
        var metadata = new PageRenderMetadata(
            42,
            9_007_199_254_740_995,
            "aero.sharpts",
            "Animals",
            "animals",
            "/animals",
            "en-US");
        var child = new ContentNode(
            "9007199254740993",
            "animal",
            "Otter",
            "otter",
            ImmutableDictionary<string, JsonElement>.Empty,
            []);
        var root = new ContentNode(
            "9007199254740995",
            "animal",
            "Mammals",
            "mammals",
            ImmutableDictionary<string, JsonElement>.Empty,
            [child]);
        var query = new ContentQueryResult(
            "animals",
            "animal",
            [root],
            2,
            false);
        var resolution = new PageContentQueryResolution
        {
            Results = ImmutableDictionary<string, ContentQueryResult>.Empty
                .Add("animals", query)
        };

        var result = await executor.ExecuteAsync(
            """
            import {
                findById,
                flatten,
                getQuery
            } from
                "aero:content";

            export function render(context: AeroRenderContext): AeroHtmlFragment {
                const query = getQuery("animals");
                if (query === null) {
                    return html`<p>Missing</p>`;
                }

                const otter = findById(query, "9007199254740993");
                return html`<p data-count="${flatten(query).length}">${otter?.title}</p>`;
            }
            """,
            SharpTsRenderContext.Create(metadata, resolution, isPreview: false),
            10_000);

        RequireSuccess(result).ShouldBe("<p data-count=\"2\">Otter</p>");
    }

    [Test]
    public async Task Rendering_profile_allows_selected_collection_types()
    {
        var executor = new SharpTsExecutor();
        var metadata = new PageRenderMetadata(
            null,
            1,
            "aero.sharpts",
            "Collections",
            "collections",
            "/collections",
            "en-US");

        var result = await executor.ExecuteAsync(
            """
            import { ArrayList } from "dotnet:System.Collections.ArrayList";

            export function render(context: AeroRenderContext): AeroHtmlFragment {
                const values = new ArrayList();
                values.add(context.page.title);
                return html`<p>${values.count}</p>`;
            }
            """,
            SharpTsRenderContext.Create(
                metadata,
                PageContentQueryResolution.Empty,
                isPreview: false),
            10_000);

        RequireSuccess(result).ShouldBe("<p>1</p>");
    }

    [Test]
    public async Task Rendering_profile_allows_selected_generic_collection_types()
    {
        var executor = new SharpTsExecutor();
        var metadata = new PageRenderMetadata(
            null,
            1,
            "aero.sharpts",
            "Generic Collections",
            "generic-collections",
            "/generic-collections",
            "en-US");

        var result = await executor.ExecuteAsync(
            """
            @DotNetType("System.Collections.Generic.List`1[System.String]")
            declare class StringList {
                constructor();
                add(item: string): void;
                readonly count: number;
            }

            export function render(context: AeroRenderContext): AeroHtmlFragment {
                const values = new StringList();
                values.add(context.page.title);
                return html`<p>${values.count}</p>`;
            }
            """,
            SharpTsRenderContext.Create(
                metadata,
                PageContentQueryResolution.Empty,
                isPreview: false),
            10_000);

        RequireSuccess(result).ShouldBe("<p>1</p>");
    }

    [Test]
    public async Task Rendering_profile_allows_selected_linq_expression_and_task_types()
    {
        var executor = new SharpTsExecutor();
        var metadata = new PageRenderMetadata(
            null,
            1,
            "aero.sharpts",
            "BCL",
            "bcl",
            "/bcl",
            "en-US");

        var result = await executor.ExecuteAsync(
            """
            import { Enumerable } from "dotnet:System.Linq.Enumerable";
            import { Expression } from "dotnet:System.Linq.Expressions.Expression";
            import { Task } from "dotnet:System.Threading.Tasks.Task";

            export function render(context: AeroRenderContext): AeroHtmlFragment {
                const range = Enumerable.range(1, 3);
                const constant = Expression.constant("Aero");
                const completed = Task.completedTask;
                return html`<p>${range !== null}:${constant !== null}:${completed.isCompleted}</p>`;
            }
            """,
            SharpTsRenderContext.Create(
                metadata,
                PageContentQueryResolution.Empty,
                isPreview: false),
            10_000);

        RequireSuccess(result).ShouldBe("<p>true:true:true</p>");
    }

    [Test]
    public async Task Rendering_profile_rejects_physical_module_imports_before_resolution()
    {
        var executor = new SharpTsExecutor();
        var metadata = new PageRenderMetadata(
            null,
            1,
            "aero.sharpts",
            "Imports",
            "imports",
            "/imports",
            "en-US");

        var result = await executor.ExecuteAsync(
            """
            import { secret } from "C:/application/secrets.ts";
            export function render(context: AeroRenderContext): AeroHtmlFragment {
                return html`<p>${secret}</p>`;
            }
            """,
            SharpTsRenderContext.Create(
                metadata,
                PageContentQueryResolution.Empty,
                isPreview: false),
            10_000);

        FormatError(result.ShouldBeOfType<Result<string>.Failure>().Error)
            .ShouldContain("is not available in rendering.safe-v1");
    }

    [Test]
    public async Task Rendering_profile_rejects_multiline_physical_module_imports()
    {
        var executor = new SharpTsExecutor();
        var metadata = new PageRenderMetadata(
            null,
            1,
            "aero.sharpts",
            "Imports",
            "imports",
            "/imports",
            "en-US");

        var result = await executor.ExecuteAsync(
            """
            import {
                secret
            } from "C:/application/secrets.ts";

            export function render(context: AeroRenderContext): AeroHtmlFragment {
                return html`<p>${secret}</p>`;
            }
            """,
            SharpTsRenderContext.Create(
                metadata,
                PageContentQueryResolution.Empty,
                isPreview: false),
            10_000);

        FormatError(result.ShouldBeOfType<Result<string>.Failure>().Error)
            .ShouldContain("is not available in rendering.safe-v1");
    }

    [Test]
    public async Task Rendering_profile_does_not_treat_comments_or_strings_as_imports()
    {
        var executor = new SharpTsExecutor();
        var metadata = new PageRenderMetadata(
            null,
            1,
            "aero.sharpts",
            "Lexer",
            "lexer",
            "/lexer",
            "en-US");

        var result = await executor.ExecuteAsync(
            """
            // import("./not-a-module.ts");
            const note = "require(\"./also-not-a-module.ts\")";
            const contentText = "from \"aero:content\"";

            export function render(context: AeroRenderContext): AeroHtmlFragment {
                return html`<p>${note.length}:${contentText}</p>`;
            }
            """,
            SharpTsRenderContext.Create(
                metadata,
                PageContentQueryResolution.Empty,
                isPreview: false),
            10_000);

        RequireSuccess(result).ShouldBe("<p>33:from &quot;aero:content&quot;</p>");
    }

    [Test]
    public async Task Rendering_profile_rejects_dynamic_imports()
    {
        var executor = new SharpTsExecutor();
        var metadata = new PageRenderMetadata(
            null,
            1,
            "aero.sharpts",
            "Dynamic Imports",
            "dynamic-imports",
            "/dynamic-imports",
            "en-US");

        var result = await executor.ExecuteAsync(
            """
            export async function load(): Promise<any> {
                return await import("./not-allowed.ts");
            }

            export function render(context: AeroRenderContext): AeroHtmlFragment {
                return html`<p>Blocked</p>`;
            }
            """,
            SharpTsRenderContext.Create(
                metadata,
                PageContentQueryResolution.Empty,
                isPreview: false),
            10_000);

        FormatError(result.ShouldBeOfType<Result<string>.Failure>().Error)
            .ShouldContain("Dynamic imports are not available");
    }

    [Test]
    [Arguments("System.IO.File")]
    [Arguments("System.Collections.Generic.List`1")]
    [Arguments("System.Collections.Generic.List`1[System.DateTime]")]
    public async Task Rendering_profile_rejects_unapproved_dotnet_type_declarations(
        string typeName)
    {
        var executor = new SharpTsExecutor();
        var metadata = new PageRenderMetadata(
            null,
            1,
            "aero.sharpts",
            "Interop",
            "interop",
            "/interop",
            "en-US");

        var result = await executor.ExecuteAsync(
            $$"""
            @DotNetType("{{typeName}}")
            declare class File {
                static readAllText(path: string): string;
            }

            export function render(context: AeroRenderContext): AeroHtmlFragment {
                return html`<p>${File.readAllText("secret.txt")}</p>`;
            }
            """,
            SharpTsRenderContext.Create(
                metadata,
                PageContentQueryResolution.Empty,
                isPreview: false),
            10_000);

        FormatError(result.ShouldBeOfType<Result<string>.Failure>().Error)
            .ShouldContain("@DotNetType is limited to approved closed generic families");
    }

    private static string RequireSuccess(Result<string> result)
        => result switch
        {
            Result<string>.Ok success => success.Value,
            Result<string>.Failure failure => throw new InvalidOperationException(
                $"Expected SharpTS success: {FormatError(failure.Error)}"),
            _ => throw new InvalidOperationException("Unexpected SharpTS result.")
        };

    private static string FormatError(AeroError error)
        => error is AeroError.Validation validation
            ? string.Join(" | ", validation.Errors)
            : error.ToString();
}
