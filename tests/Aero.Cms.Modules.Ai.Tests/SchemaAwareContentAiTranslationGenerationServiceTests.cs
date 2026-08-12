using System.Text.Json;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Modules.Ai.Services;
using Aero.Core.Railway;
using Shouldly;

namespace Aero.Cms.Modules.Ai.Tests;

public sealed class SchemaAwareContentAiTranslationGenerationServiceTests
{
    [Test]
    public async Task Generates_only_localized_supported_textual_fields_and_retains_apply_versions()
    {
        var translator = new RecordingTranslator(["title", "body"]);
        var result = await Create(translator).GenerateAsync(Request(
            ("title", ContentFieldTypes.Text, ContentFieldLocalizationMode.Localized, "Hello"),
            ("body", ContentFieldTypes.RichText, ContentFieldLocalizationMode.Localized, "<p>Hello</p>"),
            ("shared", ContentFieldTypes.Text, ContentFieldLocalizationMode.Shared, "Private"),
            ("reference", ContentFieldTypes.Reference, ContentFieldLocalizationMode.Localized, "123"),
            ("copied", ContentFieldTypes.Text, ContentFieldLocalizationMode.CopyOnFork, "Copy")));

        var ok = result.ShouldBeOfType<Result<GenerateContentAiTranslationResponse>.Ok>().Value;
        translator.Fields.Select(field => field.Key).ShouldBe(["title", "body"]);
        ok.Application.SourceVersionNumber.ShouldBe(4);
        ok.Application.ExpectedTargetVersionNumber.ShouldBe(7);
        ok.Application.TranslatedFields.Keys.ShouldBe(["title", "body"]);
        ok.Warnings.ShouldContain(warning => warning.Contains("reference", StringComparison.Ordinal));
    }

    [Test]
    public async Task Rejects_unsupported_cultures_and_aggregate_limits_before_provider_access()
    {
        var translator = new RecordingTranslator([]);
        var cultures = new ContentLocalizationContext(9, "en-US", ["en-US", "fr-FR"], ContentCultureFallbackPolicy.ExactOnly);
        var invalidCulture = Request(("title", ContentFieldTypes.Text, ContentFieldLocalizationMode.Localized, "Hello")) with { Localization = cultures, TargetCulture = "fr" };
        (await Create(translator).GenerateAsync(invalidCulture)).ShouldBeOfType<Result<GenerateContentAiTranslationResponse>.Failure>();

        var oversized = Request(("title", ContentFieldTypes.Text, ContentFieldLocalizationMode.Localized, new string('a', 200_001)));
        (await Create(translator).GenerateAsync(oversized)).ShouldBeOfType<Result<GenerateContentAiTranslationResponse>.Failure>();
        translator.Fields.ShouldBeEmpty();
    }

    [Test]
    public async Task Preserves_source_when_rich_text_structure_changes_or_provider_omits_field()
    {
        var translator = new RecordingTranslator(["body"], new Dictionary<string, string> { ["body"] = "<div>Bonjour</div>" });
        var result = await Create(translator).GenerateAsync(Request(("body", ContentFieldTypes.RichText, ContentFieldLocalizationMode.Localized, "<p>Hello</p>")));
        var ok = result.ShouldBeOfType<Result<GenerateContentAiTranslationResponse>.Ok>().Value;

        ok.Application.TranslatedFields["body"].GetString().ShouldBe("<p>Hello</p>");
        ok.Warnings.ShouldContain(warning => warning.Contains("did not preserve markup", StringComparison.Ordinal));
    }

    private static SchemaAwareContentAiTranslationGenerationService Create(RecordingTranslator translator) => new(translator, [], [new TextContentTranslationFieldHandler(), new RichTextContentTranslationFieldHandler()]);

    private static GenerateContentAiTranslationRequest Request(params (string Name, string Type, ContentFieldLocalizationMode Mode, string Value)[] fields)
    {
        var definition = new ContentTypeDefinition { SiteId = 9, Alias = "article" };
        definition.Fields.AddRange(fields.Select(field => new ContentFieldDefinition { Name = field.Name, FieldType = field.Type, LocalizationMode = field.Mode }));
        return new(9, definition, new ContentLocalizationContext(9, "en-US", ["en-US", "fr-FR"], ContentCultureFallbackPolicy.ExactOnly),
            new ContentTranslationSource(101, 4, fields.ToDictionary(field => field.Name, field => JsonSerializer.SerializeToElement(field.Value))),
            new ContentTranslationTarget(202, 7), "en-US", "fr-FR");
    }

    private sealed class RecordingTranslator(IEnumerable<string> expected, IReadOnlyDictionary<string, string>? values = null) : IAiContentTranslationService
    {
        public List<TranslateDocumentField> Fields { get; } = [];

        public Task<Result<TranslateDocumentResponse>> TranslateAsync(TranslateDocumentRequest request, CancellationToken cancellationToken = default)
        {
            Fields.AddRange(request.Fields);
            var translated = expected.ToDictionary(key => key, key => values?.GetValueOrDefault(key) ?? $"translated-{key}");
            return Task.FromResult<Result<TranslateDocumentResponse>>(new TranslateDocumentResponse(translated, [], "provider", "model"));
        }
    }
}
