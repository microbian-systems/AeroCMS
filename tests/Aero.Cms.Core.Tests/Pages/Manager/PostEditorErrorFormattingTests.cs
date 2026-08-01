using System.Collections.Immutable;
using Aero.Cms.Shared.Pages.Manager.PostEditor;
using Aero.Core;

namespace Aero.Cms.Core.Tests.Pages.Manager;

public sealed class PostEditorErrorFormattingTests
{
    [Test]
    public async Task Validation_errors_are_rendered_as_actionable_messages()
    {
        var error = new AeroError.Validation(
            ImmutableList.Create("Title is required.", "Slug is invalid."));

        var message = PostEditor.FormatError(error);

        await Assert.That(message).IsEqualTo("Title is required.; Slug is invalid.");
        await Assert.That(message).DoesNotContain("ImmutableList");
        await Assert.That(message).DoesNotContain("System.Collections");
    }
}
