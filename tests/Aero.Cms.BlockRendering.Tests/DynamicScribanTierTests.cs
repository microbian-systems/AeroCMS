using System.Text.Json;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Blocks.Dynamic;
using Aero.Cms.Core.Content.Rendering;
using Aero.Cms.Core.Extensions;
using Aero.Cms.Shared.Blocks.Rendering;
using Aero.Core;
using Aero.Core.Railway;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;
using TUnit.Core;

namespace Aero.Cms.BlockRendering.Tests;

public sealed class DynamicScribanTierTests
{
    [Test]
    public void DynamicTemplateBlock_IsDiscoveredForGeneratedModelRegistration()
    {
        GeneratedBlockModelManifest.Blocks.Should().HaveCount(36);
        GeneratedBlockModelManifest.Blocks["dynamic_template"].ModelType.Should().Be(typeof(DynamicTemplateBlock));
        GeneratedBlockJsonRegistration.ModelTypes.Should().Contain(typeof(DynamicTemplateBlock));
        GeneratedBlockJsonRegistration.CollectionTypes.Should().Contain(typeof(List<DynamicTemplateBlock>));
    }

    [Test]
    public void BlockSerializer_RoundTripsDynamicTemplateBlock()
    {
        using var data = JsonDocument.Parse("""{"title":"Dynamic Title","count":3}""");
        var block = new DynamicTemplateBlock
        {
            DefinitionId = 42,
            DefinitionVersion = 7,
            Data = data
        };

        var json = BlockSerializer.Serialize(block);
        var result = BlockSerializer.Deserialize(json);

        result.Should().BeOfType<Result<BlockBase, AeroError>.Ok>();
        var deserialized = ((Result<BlockBase, AeroError>.Ok)result).Value
            .Should()
            .BeOfType<DynamicTemplateBlock>()
            .Subject;

        deserialized.DefinitionId.Should().Be(42);
        deserialized.DefinitionVersion.Should().Be(7);
        deserialized.Data!.RootElement.GetProperty("title").GetString().Should().Be("Dynamic Title");
    }

    [Test]
    public async Task SecureScribanRenderer_RendersJsonDataThroughExplicitScriptObjects()
    {
        using var data = JsonDocument.Parse("""{"title":"Dynamic Title","items":[{"name":"One"},{"name":"Two"}]}""");
        var definition = new DynamicBlockDefinition
        {
            Id = 100,
            Version = 1,
            ScribanTemplate = "<h1>{{ block.title }}</h1>{{ for item in block.items }}<span>{{ item.name }}</span>{{ end }}"
        };

        var renderer = new SecureScribanRenderer();
        var result = await renderer.RenderAsync(definition, data);

        result.Should().BeOfType<Result<string, AeroError>.Ok>();
        var html = ((Result<string, AeroError>.Ok)result).Value;
        html.Should().Contain("<h1>Dynamic Title</h1>");
        html.Should().Contain("<span>One</span>");
        html.Should().Contain("<span>Two</span>");
    }

    [Test]
    public async Task SecureScribanRenderer_AllowsCuratedScribanFunctions()
    {
        using var data = JsonDocument.Parse("""{"title":"Dynamic Title","body":"<p>Hello</p>","items":[{"name":"One"},{"name":"Two"}],"score":3.6}""");
        var definition = new DynamicBlockDefinition
        {
            Id = 103,
            Version = 1,
            ScribanTemplate = """
                <h1>{{ block.title | string.downcase }}</h1>
                <p>{{ block.body | html.strip }}</p>
                <span>{{ block.items | array.size }}</span>
                <strong>{{ block.score | math.round }}</strong>
                """
        };

        var renderer = new SecureScribanRenderer();
        var result = await renderer.RenderAsync(definition, data);

        result.Should().BeOfType<Result<string, AeroError>.Ok>();
        var html = ((Result<string, AeroError>.Ok)result).Value;
        html.Should().Contain("<h1>dynamic title</h1>");
        html.Should().Contain("<p>Hello</p>");
        html.Should().Contain("<span>2</span>");
        html.Should().Contain("<strong>4</strong>");
    }

    [Test]
    public async Task SecureScribanRenderer_RejectsMissingVariables()
    {
        using var data = JsonDocument.Parse("""{"title":"Dynamic Title"}""");
        var definition = new DynamicBlockDefinition
        {
            Id = 101,
            Version = 1,
            ScribanTemplate = "{{ block.missing }}"
        };

        var renderer = new SecureScribanRenderer();
        var result = await renderer.RenderAsync(definition, data);

        result.Should().BeOfType<Result<string, AeroError>.Failure>();
    }

    [Test]
    public async Task SecureScribanRenderer_SanitizesRenderedMarkup()
    {
        using var data = JsonDocument.Parse("""{"html":"<img src=x onerror=alert('x')>"}""");
        var definition = new DynamicBlockDefinition
        {
            Id = 102,
            Version = 1,
            ScribanTemplate = "<div>{{ block.html }}</div>"
        };

        var renderer = new SecureScribanRenderer();
        var result = await renderer.RenderAsync(definition, data);

        result.Should().BeOfType<Result<string, AeroError>.Ok>();
        var html = ((Result<string, AeroError>.Ok)result).Value;
        var normalizedHtml = html.ToLowerInvariant();
        normalizedHtml.Should().NotContain("onerror");
        normalizedHtml.Should().NotContain("<script");
    }

    [Test]
    public async Task SecureScribanRenderer_ValidatesContentDataAgainstSchema()
    {
        using var data = JsonDocument.Parse("""{"count":"not-a-number"}""");
        using var schema = JsonDocument.Parse("""
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
        var definition = new DynamicBlockDefinition
        {
            Id = 104,
            Version = 1,
            ScribanTemplate = "{{ block.title }}",
            DataSchema = schema
        };

        var renderer = new SecureScribanRenderer();
        var result = await renderer.RenderAsync(definition, data);

        result.Should().BeOfType<Result<string, AeroError>.Failure>();
        var validation = ((Result<string, AeroError>.Failure)result).Error
            .Should().BeOfType<AeroError.Validation>().Subject;
        validation.Errors.Should().Contain(error => error.Contains("Required field 'title'", StringComparison.Ordinal));
        validation.Errors.Should().Contain(error => error.Contains("Field 'count' must be of type 'number'", StringComparison.Ordinal));
    }

    [Test]
    public async Task GeneratedContentTypeTemplate_RendersAnimalsPayloadWithHyphenatedFieldName()
    {
        var type = new ContentTypeDefinition
        {
            Id = 1514362346261479424,
            SiteId = 1514356897097744384,
            Name = "Animals",
            Alias = "animals",
            Fields =
            [
                new() { Name = "title", Label = "Title", FieldType = "text" },
                new() { Name = "image", Label = "Image", FieldType = "image" },
                new() { Name = "rich-text", Label = "Rich text", FieldType = "richtext" },
                new() { Name = "number", Label = "Number", FieldType = "number" },
                new() { Name = "yesno", Label = "Yes/No", FieldType = "boolean" },
                new() { Name = "date", Label = "Date", FieldType = "date" }
            ]
        };
        using var data = JsonDocument.Parse("""
            {
              "date": "2026-06-10T00:00:00",
              "image": "/media/1504185038039040000-snowflake-96.png",
              "title": "Monkey",
              "yesno": true,
              "number": 3,
              "rich-text": "This is a monkey"
            }
            """);
        var template = ContentTypeTemplateGenerator.GenerateTemplate(type, []);
        var definition = new DynamicBlockDefinition
        {
            Id = type.Id,
            Version = 1,
            ScribanTemplate = template
        };

        template.Should().Contain("""block["rich-text"]""");

        var result = await new SecureScribanRenderer().RenderAsync(definition, data);

        result.Should().BeOfType<Result<string, AeroError>.Ok>();
        var html = ((Result<string, AeroError>.Ok)result).Value;
        html.Should().Contain("Monkey");
        html.Should().Contain("/media/1504185038039040000-snowflake-96.png");
        html.Should().Contain("This is a monkey");
        html.Should().Contain(">3<");
        html.Should().Contain("2026-06-10T00:00:00");
    }

    [Test]
    public async Task ContentItemRenderer_NormalizesLegacyDotAccessForHyphenatedFields()
    {
        using var data = JsonDocument.Parse("""{"rich-text":"test"}""");
        var type = new ContentTypeDefinition
        {
            Id = 10,
            SiteId = 20,
            Alias = "test",
            Fields =
            [
                new() { Name = "rich-text", Label = "Rich text", FieldType = "richtext" }
            ]
        };
        var item = new ContentItem
        {
            Id = 30,
            SiteId = type.SiteId,
            ContentTypeAlias = type.Alias,
            Fields = new Dictionary<string, JsonElement>
            {
                ["rich-text"] = data.RootElement.GetProperty("rich-text").Clone()
            }
        };
        var definition = new DynamicBlockDefinition
        {
            Id = 40,
            Version = 1,
            ScribanTemplate = """<p>{{ block.rich-text }}</p>"""
        };
        var renderer = new ContentItemRenderer(
            new StubContentTypeRenderingBridge(definition, data),
            new SecureScribanRenderer());

        var result = await renderer.RenderAsync(type, item);

        result.Should().BeOfType<Result<string, AeroError>.Ok>();
        ((Result<string, AeroError>.Ok)result).Value.Should().Contain("<p>test</p>");
        definition.ScribanTemplate.Should().Contain("""block["rich-text"]""");
    }

    [Test]
    public async Task ContentItemRenderer_NormalizesLegacyAsteriskComments()
    {
        using var data = JsonDocument.Parse("""{"title":"Monkey"}""");
        var type = new ContentTypeDefinition
        {
            Id = 11,
            SiteId = 21,
            Alias = "animals",
            Fields =
            [
                new() { Name = "title", Label = "Title", FieldType = "text" }
            ]
        };
        var item = new ContentItem
        {
            Id = 31,
            SiteId = type.SiteId,
            ContentTypeAlias = type.Alias,
            Fields = new Dictionary<string, JsonElement>
            {
                ["title"] = data.RootElement.GetProperty("title").Clone()
            }
        };
        var definition = new DynamicBlockDefinition
        {
            Id = 41,
            Version = 1,
            ScribanTemplate = """
                {{* HERO SECTION *}}
                <h1>{{ block.title }}</h1>
                """
        };
        var renderer = new ContentItemRenderer(
            new StubContentTypeRenderingBridge(definition, data),
            new SecureScribanRenderer());

        var result = await renderer.RenderAsync(type, item);

        result.Should().BeOfType<Result<string, AeroError>.Ok>();
        ((Result<string, AeroError>.Ok)result).Value.Should().Contain("<h1>Monkey</h1>");
        definition.ScribanTemplate.Should().Contain("{{## HERO SECTION ##}}");
    }

    [Test]
    public async Task ContentItemRenderer_RejectsUnimplementedBlockLayoutMode()
    {
        var renderer = new ContentItemRenderer(
            new ThrowingContentTypeRenderingBridge(),
            new SecureScribanRenderer());
        var type = new ContentTypeDefinition
        {
            Id = 1,
            SiteId = 2,
            Alias = "team-member",
            RenderMode = ContentTypeRenderMode.BlockLayout
        };
        var item = new ContentItem
        {
            Id = 3,
            SiteId = 2,
            ContentTypeAlias = type.Alias
        };

        var result = await renderer.RenderAsync(type, item);

        result.Should().BeOfType<Result<string, AeroError>.Failure>();
    }

    [Test]
    public async Task BlockRenderer_WithDynamicTemplateBlock_RendersThroughGeneratedAdapter()
    {
        using var data = JsonDocument.Parse("""{"title":"Generated Dynamic","body":"<strong>Body</strong>"}""");
        var block = new DynamicTemplateBlock
        {
            DefinitionId = 500,
            DefinitionVersion = 2,
            Data = data
        };

        var definition = new DynamicBlockDefinition
        {
            Id = 500,
            Version = 2,
            IsPublished = true,
            ScribanTemplate = "<article><h2>{{ block.title }}</h2>{{ block.body }}</article>"
        };

        var html = await RenderComponentAsync<BlockRenderer>(
            new Dictionary<string, object?>
            {
                ["Block"] = block
            },
            services => services.AddSingleton<IDynamicBlockDefinitionService>(new StubDynamicBlockDefinitionService(definition)));

        html.Should().Contain("<h2>Generated Dynamic</h2>");
        html.Should().Contain("<strong>Body</strong>");
        html.Should().NotContain("Unknown block type: dynamic_template");
    }

    [Test]
    public void CmsBlockRenderRegistry_ResolvesDynamicTemplateBlock()
    {
        CmsBlockRenderRegistry.TryGet("dynamic_template", out var adapter)
            .Should()
            .BeTrue();

        adapter.ModelType.Should().Be(typeof(DynamicTemplateBlock));
    }

    [Test]
    public void DynamicTemplateValidator_RejectsScriptTagsAndEventHandlers()
    {
        var validator = new DynamicTemplateValidator();

        validator.Validate("""<script>alert("x")</script>""")
            .Should()
            .BeOfType<Result<NoneType, AeroError>.Failure>();

        validator.Validate("""<div onclick="alert('x')">{{ block.title }}</div>""")
            .Should()
            .BeOfType<Result<NoneType, AeroError>.Failure>();
    }

    [Test]
    public void DynamicTemplateValidator_RejectsUnsafeFunctionCalls()
    {
        var validator = new DynamicTemplateValidator();

        validator.Validate("""{{ object.eval "1 + 1" }}""")
            .Should()
            .BeOfType<Result<NoneType, AeroError>.Failure>();

        validator.Validate("""{{ block.title | regex.replace "a" "b" }}""")
            .Should()
            .BeOfType<Result<NoneType, AeroError>.Failure>();
    }

    [Test]
    public void DynamicTemplateValidator_AllowsCuratedFunctionCallsByDefault()
    {
        var validator = new DynamicTemplateValidator();

        var textResult = validator.Validate("""{{ block.title | string.downcase | string.truncate 30 }}""");
        textResult
            .Should()
            .BeOfType<Result<NoneType, AeroError>.Ok>(Describe(textResult));

        var arrayResult = validator.Validate("""{{ block.items | array.size }}""");
        arrayResult
            .Should()
            .BeOfType<Result<NoneType, AeroError>.Ok>(Describe(arrayResult));
    }

    [Test]
    public void AddBlockSystemServices_RegistersSecureScribanServices()
    {
        var services = new ServiceCollection();
        services.AddBlockSystemServices();

        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<SecureScribanTemplateOptions>()
            .Should()
            .NotBeNull();

        serviceProvider.GetRequiredService<DynamicTemplateValidator>()
            .Should()
            .NotBeNull();

        serviceProvider.GetRequiredService<ISecureScribanRenderer>()
            .Should()
            .BeOfType<SecureScribanRenderer>();
    }

    private static async Task<string> RenderComponentAsync<TComponent>(
        IDictionary<string, object?> parameters,
        Action<IServiceCollection>? configureServices = null)
        where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRadzenComponents();
        services.AddBlockSystemServices();
        services.AddSingleton<IErrorBoundaryLogger, NoOpErrorBoundaryLogger>();
        configureServices?.Invoke(services);

        await using var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        await using var htmlRenderer = new HtmlRenderer(serviceProvider, loggerFactory);

        return await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameterView = ParameterView.FromDictionary(parameters);
            var output = await htmlRenderer.RenderComponentAsync<TComponent>(parameterView);
            return output.ToHtmlString();
        });
    }

    private static string Describe(Result<NoneType, AeroError> result)
    {
        return result is Result<NoneType, AeroError>.Failure { Error: AeroError.Validation validation }
            ? string.Join("; ", validation.Errors)
            : result.ToString() ?? string.Empty;
    }

    private sealed class StubDynamicBlockDefinitionService(DynamicBlockDefinition definition) : IDynamicBlockDefinitionService
    {
        public Task<Result<DynamicBlockDefinition, AeroError>> GetAsync(
            long definitionId,
            int version,
            CancellationToken cancellationToken = default)
        {
            return definition.Id == definitionId && definition.Version == version
                ? Task.FromResult<Result<DynamicBlockDefinition, AeroError>>(definition)
                : Task.FromResult<Result<DynamicBlockDefinition, AeroError>>(AeroError.NotFoundError("Definition not found."));
        }
    }

    private sealed class ThrowingContentTypeRenderingBridge : IContentTypeRenderingBridge
    {
        public Task<Result<DynamicTemplateBlock, AeroError>> ToDynamicBlockAsync(
            ContentTypeDefinition typeDef,
            ContentItem item,
            CancellationToken ct = default)
            => throw new InvalidOperationException("BlockLayout should not enter the Scriban bridge.");

        public Task<Result<DynamicBlockDefinition, AeroError>> GetDefinitionAsync(
            ContentTypeDefinition typeDef,
            CancellationToken ct = default)
            => throw new InvalidOperationException("BlockLayout should not resolve a Scriban definition.");
    }

    private sealed class StubContentTypeRenderingBridge(
        DynamicBlockDefinition definition,
        JsonDocument data) : IContentTypeRenderingBridge
    {
        public Task<Result<DynamicTemplateBlock, AeroError>> ToDynamicBlockAsync(
            ContentTypeDefinition typeDef,
            ContentItem item,
            CancellationToken ct = default)
            => Task.FromResult<Result<DynamicTemplateBlock, AeroError>>(new DynamicTemplateBlock
            {
                Id = item.Id,
                DefinitionId = definition.Id,
                DefinitionVersion = definition.Version,
                Data = data
            });

        public Task<Result<DynamicBlockDefinition, AeroError>> GetDefinitionAsync(
            ContentTypeDefinition typeDef,
            CancellationToken ct = default)
            => Task.FromResult<Result<DynamicBlockDefinition, AeroError>>(definition);
    }

    private sealed class NoOpErrorBoundaryLogger : IErrorBoundaryLogger
    {
        public ValueTask LogErrorAsync(Exception exception)
            => ValueTask.CompletedTask;
    }
}
