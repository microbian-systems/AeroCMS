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
    public void Shared_field_notice_points_editors_to_the_available_concurrency_checked_command()
    {
        var sourcePath = FindEditorSource();
        var source = File.ReadAllText(sourcePath);

        source.ShouldContain("Edit it in Translations, under Shared fields, then choose Save shared fields.");
        source.ShouldNotContain("Editing remains unavailable until the manager has an explicit concurrency-checked shared-value command");
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
