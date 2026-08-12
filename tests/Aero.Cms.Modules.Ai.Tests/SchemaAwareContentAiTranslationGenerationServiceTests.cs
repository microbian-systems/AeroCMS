using System.Text.Json;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Modules.Ai.Services;
using Aero.Core;
using Aero.Core.Railway;
using Shouldly;

namespace Aero.Cms.Modules.Ai.Tests;

public sealed class SchemaAwareContentAiTranslationGenerationServiceTests
{
    [Test]
    public async Task Resolver_snapshot_is_the_only_schema_and_content_sent_to_provider()
    {
        var translator = new RecordingTranslator();
        var result = await Create(translator, Snapshot(
            ("title", ContentFieldTypes.Text, ContentFieldLocalizationMode.Localized, "Hello"),
            ("shared", ContentFieldTypes.Text, ContentFieldLocalizationMode.Shared, "Secret"),
            ("ref", ContentFieldTypes.Reference, ContentFieldLocalizationMode.Localized, "123")))
            .GenerateAsync(Request());
        var ok = result.ShouldBeOfType<Result<GenerateContentAiTranslationResponse>.Ok>().Value;
        translator.Fields.Select(x => x.Key).ShouldBe(["title"]);
        ok.Application.SourceVersionNumber.ShouldBe(4);
        ok.Application.ExpectedTargetVersionNumber.ShouldBe(7);
        ok.Application.ProviderId.ShouldBe("stable-provider-id");
    }

    [Test]
    public async Task Deny_resolver_and_stale_or_cross_site_snapshots_fail_before_provider_egress()
    {
        var translator = new RecordingTranslator();
        (await Create(translator, null).GenerateAsync(Request())).ShouldBeOfType<Result<GenerateContentAiTranslationResponse>.Failure>();
        var stale = Snapshot(("title", "text", ContentFieldLocalizationMode.Localized, "Hi"));
        (await Create(translator, stale with { Target = stale.Target with { VersionNumber = 8 } }).GenerateAsync(Request())).ShouldBeOfType<Result<GenerateContentAiTranslationResponse>.Failure>();
        (await Create(translator, Snapshot(("title", "text", ContentFieldLocalizationMode.Localized, "Hi")) with { ContentType = new ContentTypeDefinition { SiteId = 10 } }).GenerateAsync(Request())).ShouldBeOfType<Result<GenerateContentAiTranslationResponse>.Failure>();
        translator.Fields.ShouldBeEmpty();
    }

    [Test]
    public async Task Cross_group_or_cross_culture_target_fails_before_provider_egress()
    {
        var translator = new RecordingTranslator();
        var snapshot = Snapshot(("title", "text", ContentFieldLocalizationMode.Localized, "Hi"));
        (await Create(translator, snapshot with { Target = snapshot.Target with { TranslationGroupId = 99 } }).GenerateAsync(Request())).ShouldBeOfType<Result<GenerateContentAiTranslationResponse>.Failure>();
        (await Create(translator, snapshot with { Target = snapshot.Target with { Culture = "fr" } }).GenerateAsync(Request())).ShouldBeOfType<Result<GenerateContentAiTranslationResponse>.Failure>();
        translator.Fields.ShouldBeEmpty();
    }

    [Test]
    public async Task Default_snapshot_resolver_denies_before_provider_egress()
    {
        var translator = new RecordingTranslator();
        var service = new SchemaAwareContentAiTranslationGenerationService(
            translator, [], [new TextContentTranslationFieldHandler(), new RichTextContentTranslationFieldHandler()],
            new DenyContentAiTranslationSnapshotResolver());

        (await service.GenerateAsync(Request())).ShouldBeOfType<Result<GenerateContentAiTranslationResponse>.Failure>();
        translator.Fields.ShouldBeEmpty();
    }

    [Test]
    public async Task Rejects_non_string_and_hostile_or_changed_richtext()
    {
        var number = JsonSerializer.SerializeToElement(5);
        var snapshot = Snapshot(("body", "richtext", ContentFieldLocalizationMode.Localized, "<a href=\"https://safe.example\">Hello</a>"));
        snapshot = snapshot with { Source = snapshot.Source with { Fields = new Dictionary<string, JsonElement> { ["body"] = number } } };
        var translator = new RecordingTranslator();
        (await Create(translator, snapshot).GenerateAsync(Request())).ShouldBeOfType<Result<GenerateContentAiTranslationResponse>.Failure>();

        var hostile = new RecordingTranslator(new Dictionary<string, string> { ["body"] = "<a href=\"javascript:alert(1)\">Bonjour</a>" });
        var result = await Create(hostile, Snapshot(("body", "richtext", ContentFieldLocalizationMode.Localized, "<a href=\"https://safe.example\">Hello</a>"))).GenerateAsync(Request());
        var ok = result.ShouldBeOfType<Result<GenerateContentAiTranslationResponse>.Ok>().Value;
        ok.Application.TranslatedFields["body"].GetString().ShouldBe("<a href=\"https://safe.example\">Hello</a>");
    }

    [Test]
    public async Task Preserves_markdown_structure_and_rejects_hostile_reference_links()
    {
        const string source = "---\ntitle: Hello\n---\n# Heading\n- item\n```csharp\ncode\n```\n[read][ref]\n[ref]: https://safe.example/docs";
        const string changedDestination = "---\ntitle: Bonjour\n---\n# Titre\n- élément\n```csharp\ncode\n```\n[lire][ref]\n[ref]: javascript:alert(1)";
        var result = await Create(new RecordingTranslator(new Dictionary<string, string> { ["body"] = changedDestination }), Snapshot(("body", "richtext", ContentFieldLocalizationMode.Localized, source))).GenerateAsync(Request());
        var ok = result.ShouldBeOfType<Result<GenerateContentAiTranslationResponse>.Ok>().Value;
        ok.Application.TranslatedFields["body"].GetString().ShouldBe(source);
    }

    private static SchemaAwareContentAiTranslationGenerationService Create(RecordingTranslator translator, ContentAiTranslationGenerationSnapshot? snapshot) => new(translator, [], [new TextContentTranslationFieldHandler(), new RichTextContentTranslationFieldHandler()], new Resolver(snapshot));
    private static GenerateContentAiTranslationRequest Request() => new(9, 101, 4, 202, 7, "fr-FR");

    private static ContentAiTranslationGenerationSnapshot Snapshot(params (string Name, string Type, ContentFieldLocalizationMode Mode, string Value)[] fields)
    {
        var type = new ContentTypeDefinition { SiteId = 9, Alias = "article" };
        type.Fields.AddRange(fields.Select(x => new ContentFieldDefinition { Name = x.Name, FieldType = x.Type, LocalizationMode = x.Mode }));
        return new(type, new ContentLocalizationContext(9, "en-US", ["en-US", "fr-FR"], ContentCultureFallbackPolicy.ExactOnly), new(101, 9, "article", 55, 4, "en-US", fields.ToDictionary(x => x.Name, x => JsonSerializer.SerializeToElement(x.Value))), new(202, 9, "article", 55, 7, "fr-FR"));
    }

    private sealed class Resolver(ContentAiTranslationGenerationSnapshot? snapshot) : IContentAiTranslationSnapshotResolver
    {
        public Task<Result<ContentAiTranslationGenerationSnapshot>> ResolveAsync(long siteId, long sourceItemId, long targetItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<Result<ContentAiTranslationGenerationSnapshot>>(snapshot is null ? AeroError.InvalidRequestError("denied") : snapshot);
    }

    private sealed class RecordingTranslator(IReadOnlyDictionary<string, string>? values = null) : IAiContentTranslationService
    {
        public List<TranslateDocumentField> Fields { get; } = [];
        public Task<Result<TranslateDocumentResponse>> TranslateAsync(TranslateDocumentRequest request, CancellationToken cancellationToken = default)
        {
            Fields.AddRange(request.Fields);
            return Task.FromResult<Result<TranslateDocumentResponse>>(new TranslateDocumentResponse(request.Fields.ToDictionary(x => x.Key, x => values?.GetValueOrDefault(x.Key) ?? $"translated {x.SourceText}"), [], "stable-provider-id", "Human label", "model"));
        }
    }
}
