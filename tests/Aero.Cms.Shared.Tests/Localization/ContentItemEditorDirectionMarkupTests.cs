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

    private static string FindEditorSource()
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
                "ContentItemEditor.razor");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("ContentItemEditor.razor could not be found from the test output path.");
    }
}
