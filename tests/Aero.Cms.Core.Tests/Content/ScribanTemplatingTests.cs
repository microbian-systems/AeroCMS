using System.Text.Json;
using Aero.Cms.Core.Content.Templating;
using Aero.Core;
using Aero.Core.Railway;
using Scriban.Runtime;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ScribanTemplatingTests
{
    [Test]
    public async Task Renders_explicit_scopes_with_loops_conditionals_and_local_functions()
    {
        using var data = JsonDocument.Parse(
            """{"title":"Dynamic Title","items":[{"name":"One"},{"name":"Two"}],"score":3.6}""");
        var definition = new ScribanRenderDefinition(
            100,
            1,
            """
            {{ func heading(value) }}<h1>{{ value | string.downcase }}</h1>{{ end }}
            {{ heading fields.title }}
            {{ if item.publication_state == "Published" }}
              {{ for row in fields.items }}<span>{{ row.name }}</span>{{ end }}
            {{ end }}
            <strong>{{ fields.score | math.round }}</strong>
            <footer>{{ content_type.alias }}:{{ site.id }}:{{ site.current_culture }}</footer>
            """,
            null);

        var result = await new SecureScribanRenderer().RenderAsync(
            definition,
            CreateModel(data.RootElement));

        var ok = result as Result<string, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value).Contains("<h1>dynamic title</h1>");
        await Assert.That(ok.Value).Contains("<span>One</span>");
        await Assert.That(ok.Value).Contains("<span>Two</span>");
        await Assert.That(ok.Value).Contains("<strong>4</strong>");
        await Assert.That(ok.Value).Contains("<footer>feature:7:en-US</footer>");
    }

    [Test]
    public async Task Renders_only_the_bounded_resolved_reference_scope()
    {
        using var fields = JsonDocument.Parse("""{"species":{"provider":"view:species","stableId":"492B3"}}""");
        using var references = JsonDocument.Parse(
            """{"species":{"provider":"view:species","stableId":"492B3","scientificName":"Okapia johnstoni","lineage":{"family":"Giraffidae"}}}""");
        var model = CreateModel(fields.RootElement) with { References = references.RootElement.Clone() };

        var result = await new SecureScribanRenderer().RenderAsync(
            new ScribanRenderDefinition(
                1001,
                1,
                "{{ references.species.scientificName }}|{{ references.species.lineage.family }}",
                null),
            model);

        var ok = result as Result<string, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value).IsEqualTo("Okapia johnstoni|Giraffidae");
    }

    [Test]
    public async Task Rejects_missing_variables()
    {
        using var data = JsonDocument.Parse("""{"title":"Dynamic Title"}""");
        var definition = new ScribanRenderDefinition(101, 1, "{{ fields.missing }}", null);

        var result = await new SecureScribanRenderer().RenderAsync(
            definition,
            CreateModel(data.RootElement));

        await Assert.That(result).IsTypeOf<Result<string, AeroError>.Failure>();
    }

    [Test]
    public async Task Does_not_expose_the_legacy_block_alias()
    {
        using var data = JsonDocument.Parse("""{"title":"Dynamic Title"}""");
        var definition = new ScribanRenderDefinition(1011, 1, "{{ block.title }}", null);

        var result = await new SecureScribanRenderer().RenderAsync(
            definition,
            CreateModel(data.RootElement));

        await Assert.That(result).IsTypeOf<Result<string, AeroError>.Failure>();
    }

    [Test]
    public async Task Sanitizes_rendered_markup()
    {
        using var data = JsonDocument.Parse("""{"html":"<img src=x onerror=alert('x')>"}""");
        var definition = new ScribanRenderDefinition(102, 1, "<div>{{ fields.html }}</div>", null);

        var result = await new SecureScribanRenderer().RenderAsync(
            definition,
            CreateModel(data.RootElement));

        var ok = result as Result<string, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value.ToLowerInvariant()).DoesNotContain("onerror");
        await Assert.That(ok.Value.ToLowerInvariant()).DoesNotContain("<script");
    }

    [Test]
    public async Task Validates_field_bag_against_schema()
    {
        using var data = JsonDocument.Parse("""{"count":"not-a-number"}""");
        using var schema = JsonDocument.Parse(
            """
            {
              "type": "object",
              "properties": {
                "title": { "type": "string" },
                "count": { "type": "number" }
              },
              "required": ["title"],
              "additionalProperties": false
            }
            """);
        using var definitionSchema = JsonDocument.Parse(schema.RootElement.GetRawText());
        var definition = new ScribanRenderDefinition(103, 1, "{{ fields.title }}", definitionSchema);

        var result = await new SecureScribanRenderer().RenderAsync(
            definition,
            CreateModel(data.RootElement));

        var failure = result as Result<string, AeroError>.Failure;
        await Assert.That(failure).IsNotNull();
        var validation = failure!.Error as AeroError.Validation;
        await Assert.That(validation).IsNotNull();
        await Assert.That(validation!.Errors.Any(error =>
            error.Contains("Required field 'title'", StringComparison.Ordinal))).IsTrue();
        await Assert.That(validation.Errors.Any(error =>
            error.Contains("Field 'count' must be of type 'number'", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Validator_rejects_active_markup_and_includes()
    {
        var validator = new ScribanTemplateValidator();

        await Assert.That(validator.Validate("""<script>alert("x")</script>"""))
            .IsTypeOf<Result<NoneType, AeroError>.Failure>();
        await Assert.That(validator.Validate("""<div onclick="alert('x')">{{ fields.title }}</div>"""))
            .IsTypeOf<Result<NoneType, AeroError>.Failure>();
        var includeResult = validator.Validate("""{{ include "hidden-template" }}""");
        await Assert.That(includeResult)
            .IsTypeOf<Result<NoneType, AeroError>.Failure>();

        var includeFailure = includeResult as Result<NoneType, AeroError>.Failure;
        var validation = includeFailure?.Error as AeroError.Validation;
        await Assert.That(validation?.Errors.Any(error =>
            error.Contains("includes are not supported", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Test]
    public async Task Full_cms_policy_allows_standard_regex_and_object_builtins()
    {
        using var data = JsonDocument.Parse("""{"title":"Item 42","other":"value"}""");
        var definition = new ScribanRenderDefinition(
            104,
            1,
            """{{ fields.title | regex.replace "[0-9]+" "#" }}|{{ fields | object.keys | array.size }}""",
            null);

        var result = await new SecureScribanRenderer().RenderAsync(
            definition,
            CreateModel(data.RootElement));

        var ok = result as Result<string, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value).Contains("Item #|2");
    }

    [Test]
    [Arguments("""{{ object.eval "1 + 1" }}""")]
    [Arguments("""{{ "1 + 1" | object.eval }}""")]
    [Arguments("""{{ object.eval_template "{{ fields.title }}" }}""")]
    public async Task Rejects_dynamic_template_evaluation(string template)
    {
        using var data = JsonDocument.Parse("""{"title":"Aero"}""");
        var definition = new ScribanRenderDefinition(1041, 1, template, null);

        var result = await new SecureScribanRenderer().RenderAsync(
            definition,
            CreateModel(data.RootElement));

        var failure = result as Result<string, AeroError>.Failure;
        await Assert.That(failure).IsNotNull();
        var validation = failure!.Error as AeroError.Validation;
        await Assert.That(validation).IsNotNull();
        await Assert.That(validation!.Errors.Any(error =>
            error.Contains("dynamic template evaluation", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Test]
    public async Task Imports_only_explicitly_supplied_script_objects()
    {
        using var data = JsonDocument.Parse("""{"title":"Aero"}""");
        var helpers = new ScriptObject
        {
            ["prefix"] = "CMS"
        };
        var imports = new Dictionary<string, ScriptObject>(StringComparer.Ordinal)
        {
            ["theme_helpers"] = helpers
        };
        var definition = new ScribanRenderDefinition(
            105,
            1,
            """{{ import theme_helpers }}{{ prefix }}:{{ fields.title }}""",
            null);

        var result = await new SecureScribanRenderer().RenderAsync(
            definition,
            CreateModel(data.RootElement),
            imports: imports);

        var ok = result as Result<string, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value).IsEqualTo("CMS:Aero");
    }

    [Test]
    public async Task Trusted_globals_are_cloned_and_do_not_gain_content_item_scopes()
    {
        var page = new ScriptObject { ["title"] = "Original" };
        var globals = new ScriptObject { ["page"] = page };
        var renderer = new SecureScribanRenderer();
        var mutation = await renderer.RenderTrustedAsync(
            new ScribanRenderDefinition(
                1_052,
                1,
                """{{ page.title = "Changed" }}{{ page.title }}""",
                null),
            globals);
        var unavailableItem = await renderer.RenderTrustedAsync(
            new ScribanRenderDefinition(1_053, 1, "{{ item.id }}", null),
            globals);

        var success = mutation as Result<string>.Ok;
        await Assert.That(success).IsNotNull();
        await Assert.That(success!.Value).IsEqualTo("Changed");
        await Assert.That(page["title"]).IsEqualTo("Original");
        await Assert.That(unavailableItem).IsTypeOf<Result<string>.Failure>();
    }

    [Test]
    public async Task Enforces_output_and_input_depth_limits()
    {
        using var shallowData = JsonDocument.Parse("""{"title":"Aero"}""");
        var outputDefinition = new ScribanRenderDefinition(
            106,
            1,
            """{{ for i in 1..10 }}12345{{ end }}""",
            null);
        var outputRenderer = new SecureScribanRenderer(
            new SecureScribanTemplateOptions { MaxOutputLength = 10 });

        var outputResult = await outputRenderer.RenderAsync(
            outputDefinition,
            CreateModel(shallowData.RootElement));

        await Assert.That(outputResult).IsTypeOf<Result<string, AeroError>.Failure>();

        using var deepData = JsonDocument.Parse("""{"level1":{"level2":"value"}}""");
        var depthDefinition = new ScribanRenderDefinition(
            107,
            1,
            """{{ fields.level1.level2 }}""",
            null);
        var depthRenderer = new SecureScribanRenderer(
            new SecureScribanTemplateOptions { MaxInputDepth = 1 });

        var depthResult = await depthRenderer.RenderAsync(
            depthDefinition,
            CreateModel(deepData.RootElement));

        await Assert.That(depthResult).IsTypeOf<Result<string, AeroError>.Failure>();
    }

    private static ScribanContentRenderModel CreateModel(JsonElement fields) =>
        new(
            fields.Clone(),
            new ScribanContentItemRenderScope(
                202,
                "sample",
                "Sample",
                "en-US",
                "Published",
                3,
                "2026-07-17T00:00:00.0000000+00:00",
                null,
                "2026-07-17T00:00:00.0000000+00:00",
                fields.Clone()),
            new ScribanContentTypeRenderScope(
                101,
                "feature",
                "Feature",
                "Feature content",
                "Marketing",
                []),
            new ScribanSiteRenderScope(
                7,
                "en-US",
                null,
                null,
                null));
}
