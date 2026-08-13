using System.Text.Json;
using Aero.Cms.Shared.Pages.Manager.ContentTypes;
using Shouldly;

namespace Aero.Cms.Shared.Tests.ContentTypes;

public sealed class ContentEntryReferencePreviewUiTests
{
    [Test]
    public void Preview_fields_preserve_order_and_exact_top_level_names()
    {
        var fields = ContentEntryReferencePreviewUi.ParsePreviewFields(
            " title \r\nmetadata.name\r\ntitle\r\n IsActive ");

        fields.ShouldBe(["title", "metadata.name", "IsActive"]);
    }

    [Test]
    public void Preview_fields_reject_unbounded_names()
    {
        var fields = ContentEntryReferencePreviewUi.NormalizePreviewFields(
            ["title", new string('x', ContentEntryReferencePreviewUi.MaximumFieldNameCharacters + 1)]);

        fields.ShouldBe(["title"]);
    }

    [Test]
    public void Preview_fields_ignore_non_string_settings_values()
    {
        using var document = JsonDocument.Parse("""
            { "previewFields": ["title", 17, null, "active"] }
            """);

        var fields = ContentEntryReferencePreviewUi.ReadPreviewFields(
            new Dictionary<string, JsonElement>
            {
                ["previewFields"] = document.RootElement.GetProperty("previewFields").Clone()
            },
            "previewFields");

        fields.ShouldBe(["title", "active"]);
    }

    [Test]
    public void Primitive_preview_values_have_explicit_kinds()
    {
        using var document = JsonDocument.Parse("""
            { "text": "River otter", "number": 42.5, "yes": true, "none": null }
            """);

        ContentEntryReferencePreviewUi.FormatValue(document.RootElement.GetProperty("text"))
            .ShouldBe(new ContentEntryPreviewValue("River otter", "text", false));
        ContentEntryReferencePreviewUi.FormatValue(document.RootElement.GetProperty("number"))
            .ShouldBe(new ContentEntryPreviewValue("42.5", "number", false));
        ContentEntryReferencePreviewUi.FormatValue(document.RootElement.GetProperty("yes"))
            .ShouldBe(new ContentEntryPreviewValue("True", "boolean", false));
        ContentEntryReferencePreviewUi.FormatValue(document.RootElement.GetProperty("none"))
            .ShouldBe(new ContentEntryPreviewValue("Null", "null", false));
    }

    [Test]
    public void Object_and_list_preview_is_depth_and_item_bounded()
    {
        var items = string.Join(',', Enumerable.Range(1, 20));
        using var document = JsonDocument.Parse($$"""
            { "items": [{{items}}], "nested": { "a": { "b": { "c": "hidden" } } } }
            """);

        var preview = ContentEntryReferencePreviewUi.FormatValue(document.RootElement);

        preview.Kind.ShouldBe("object");
        preview.IsTruncated.ShouldBeTrue();
        preview.Text.ShouldContain("…");
        preview.Text.Length.ShouldBeLessThanOrEqualTo(ContentEntryReferencePreviewUi.MaximumRenderedCharacters);
        preview.Text.ShouldNotContain("hidden");
    }

    [Test]
    public void Long_and_markup_like_strings_remain_text_and_are_bounded()
    {
        var source = "<script>alert('preview')</script>" + new string('x', 600);
        var element = JsonSerializer.SerializeToElement(source);

        var preview = ContentEntryReferencePreviewUi.FormatValue(element);

        preview.Kind.ShouldBe("text");
        preview.Text.ShouldStartWith("<script>alert('preview')</script>");
        preview.Text.Length.ShouldBeLessThanOrEqualTo(ContentEntryReferencePreviewUi.MaximumStringCharacters + 1);
        preview.IsTruncated.ShouldBeTrue();
    }

    [Test]
    public void Only_the_latest_preview_request_can_update_state()
    {
        var guard = new ContentEntryPreviewRequestGuard();
        var first = guard.Begin();
        var second = guard.Begin();

        guard.IsCurrent(first).ShouldBeFalse();
        guard.IsCurrent(second).ShouldBeTrue();

        guard.Invalidate();
        guard.IsCurrent(second).ShouldBeFalse();
    }
}
