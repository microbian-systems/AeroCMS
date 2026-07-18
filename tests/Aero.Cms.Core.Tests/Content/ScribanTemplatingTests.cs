using System.Text.Json;
using Aero.Cms.Core.Content.Templating;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ScribanTemplatingTests
{
    [Test]
    public async Task Renders_json_field_bags_and_curated_functions()
    {
        using var data = JsonDocument.Parse(
            """{"title":"Dynamic Title","items":[{"name":"One"},{"name":"Two"}],"score":3.6}""");
        var definition = new ScribanRenderDefinition(
            100,
            1,
            """
            <h1>{{ block.title | string.downcase }}</h1>
            {{ for item in block.items }}<span>{{ item.name }}</span>{{ end }}
            <strong>{{ block.score | math.round }}</strong>
            """,
            null);

        var result = await new SecureScribanRenderer().RenderAsync(definition, data);

        var ok = result as Result<string, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value).Contains("<h1>dynamic title</h1>");
        await Assert.That(ok.Value).Contains("<span>One</span>");
        await Assert.That(ok.Value).Contains("<span>Two</span>");
        await Assert.That(ok.Value).Contains("<strong>4</strong>");
    }

    [Test]
    public async Task Rejects_missing_variables()
    {
        using var data = JsonDocument.Parse("""{"title":"Dynamic Title"}""");
        var definition = new ScribanRenderDefinition(101, 1, "{{ block.missing }}", null);

        var result = await new SecureScribanRenderer().RenderAsync(definition, data);

        await Assert.That(result).IsTypeOf<Result<string, AeroError>.Failure>();
    }

    [Test]
    public async Task Sanitizes_rendered_markup()
    {
        using var data = JsonDocument.Parse("""{"html":"<img src=x onerror=alert('x')>"}""");
        var definition = new ScribanRenderDefinition(102, 1, "<div>{{ block.html }}</div>", null);

        var result = await new SecureScribanRenderer().RenderAsync(definition, data);

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
        var definition = new ScribanRenderDefinition(103, 1, "{{ block.title }}", definitionSchema);

        var result = await new SecureScribanRenderer().RenderAsync(definition, data);

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
    public async Task Validator_rejects_active_markup_and_unsafe_functions()
    {
        var validator = new ScribanTemplateValidator();

        await Assert.That(validator.Validate("""<script>alert("x")</script>"""))
            .IsTypeOf<Result<NoneType, AeroError>.Failure>();
        await Assert.That(validator.Validate("""<div onclick="alert('x')">{{ block.title }}</div>"""))
            .IsTypeOf<Result<NoneType, AeroError>.Failure>();
        await Assert.That(validator.Validate("""{{ object.eval "1 + 1" }}"""))
            .IsTypeOf<Result<NoneType, AeroError>.Failure>();
        await Assert.That(validator.Validate("""{{ block.title | regex.replace "a" "b" }}"""))
            .IsTypeOf<Result<NoneType, AeroError>.Failure>();
    }

    [Test]
    public async Task Validator_allows_curated_functions()
    {
        var validator = new ScribanTemplateValidator();

        await Assert.That(validator.Validate(
                """{{ block.title | string.downcase | string.truncate 30 }}"""))
            .IsTypeOf<Result<NoneType, AeroError>.Ok>();
        await Assert.That(validator.Validate("""{{ block.items | array.size }}"""))
            .IsTypeOf<Result<NoneType, AeroError>.Ok>();
    }
}
