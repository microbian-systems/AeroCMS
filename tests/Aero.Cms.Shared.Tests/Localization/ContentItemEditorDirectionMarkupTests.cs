using Shouldly;
using System.Text.RegularExpressions;

namespace Aero.Cms.Shared.Tests.Localization;

public sealed class ContentItemEditorDirectionMarkupTests
{
    [Test]
    public void Content_culture_direction_is_scoped_to_value_wrappers()
    {
        var sourcePath = FindEditorSource();
        var source = File.ReadAllText(sourcePath);

        source.ShouldContain("class=\"ci-localized-value\" lang=\"@(IsSharedField(field) ? null : _culture)\" dir=\"@(IsSharedField(field) ? null : PreviewDirection)\"");
        source.ShouldContain("class=\"ci-localized-value w-full\" lang=\"@(IsSharedField(field) ? null : _culture)\" dir=\"@(IsSharedField(field) ? null : PreviewDirection)\"");
        source.ShouldContain("class=\"ci-localized-value\" lang=\"@_culture\" dir=\"@PreviewDirection\"");
        source.ShouldNotContain("<label class=\"pe-property-group\" lang=\"@_culture\"");
        Regex.IsMatch(
                source,
                "<fieldset class=\\\"ci-field-editor\\\"[^>]*(?:lang|dir)=",
                RegexOptions.Singleline)
            .ShouldBeFalse();
        source.ShouldNotContain("<RadzenButton lang=\"@_culture\"");
    }

    [Test]
    public void Translation_navigation_reloads_the_editor_for_the_new_route_values()
    {
        var sourcePath = FindEditorSource("ContentItemEditor.razor.cs");
        var source = File.ReadAllText(sourcePath).ReplaceLineEndings("\n");

        source.ShouldContain(
            "$\"/manager/content/{Alias}/editor/{ok.Value.Id}?tab=translations\",\n" +
            "                    forceLoad: true);");
        source.ShouldContain(
            "$\"/manager/content/{Alias}/editor/{id}?tab=translations\",\n" +
            "            forceLoad: true);");
    }

    [Test]
    public void Shared_fields_are_editable_on_the_content_tab_with_their_concurrency_checked_command()
    {
        var sourcePath = FindEditorSource();
        var source = File.ReadAllText(sourcePath);

        source.ShouldContain("Edit it here, then choose Save shared fields. Save Draft does not overwrite it.");
        source.ShouldContain("Click=\"@SaveSharedFieldsAsync\"");
        source.ShouldContain("disabled=\"@(IsSharedField(field) && (!HasExactLocalizationTokens || _isSavingSharedFields))\"");
        source.ShouldContain("Disabled=\"@(_isSaving || IsSharedFieldsDirty)\"");
        source.ShouldContain("Disabled=\"@(_isSaving || IsSharedFieldsDirty || !PublishReviewDecision.CanPublish)\"");
        source.ShouldNotContain("disabled=\"@IsSharedField(field)\"");
        source.ShouldNotContain("Editing remains unavailable until the manager has an explicit concurrency-checked shared-value command");
    }

    [Test]
    public void Rich_text_fields_use_the_shared_wysiwyg_editor()
    {
        var source = File.ReadAllText(FindEditorSource());
        var editor = File.ReadAllText(FindEditorSource("ContentRichTextEditor.razor"));

        Regex.Matches(source, "<ContentRichTextEditor").Count.ShouldBe(2);
        editor.ShouldContain("<RadzenHtmlEditor");
        editor.ShouldContain("<RadzenHtmlEditorFormatBlock />");
        editor.ShouldContain("<RadzenHtmlEditorLink />");
        editor.ShouldContain("Paste=\"@SanitizePaste\"");
        editor.ShouldContain("RichTextHtmlCanonicalizer.CanonicalizeEditorFormatting");
        editor.ShouldContain("_safeValue = SanitizeEditorHtml(Value);");
        editor.ShouldNotContain("<RadzenHtmlEditorSource");
        editor.ShouldNotContain("<RadzenHtmlEditorImage");
        editor.ShouldNotContain("<RadzenHtmlEditorAlign");
    }

    [Test]
    public void Content_editor_actions_reflow_on_narrow_viewports()
    {
        var source = File.ReadAllText(FindEditorSource());

        source.ShouldContain("ci-page-header flex items-center justify-between gap-4");
        source.ShouldContain("ci-page-actions flex gap-3");
        source.ShouldContain("@@media (max-width: 640px)");
        source.ShouldContain("grid-template-columns: repeat(2, minmax(0, 1fr));");
        source.ShouldContain(".ci-shared-field-actions .rz-button { width: 100%; }");
    }

    private static string FindEditorSource(string fileName = "ContentItemEditor.razor")
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "Aero.Cms.Shared",
                "Pages",
                "Manager",
                "ContentTypes",
                fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"{fileName} could not be found from the test output path.");
    }
}
