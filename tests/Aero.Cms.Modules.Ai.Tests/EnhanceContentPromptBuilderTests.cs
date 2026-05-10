using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Modules.Ai.Services;
using FluentAssertions;

namespace Aero.Cms.Modules.Ai.Tests;

public sealed class EnhanceContentPromptBuilderTests
{
    [Test]
    public async Task Build_ShouldIncludeStructuredOutputContractAndInput()
    {
        var builder = new EnhanceContentPromptBuilder();
        var request = new EnhanceContentRequest(
            "post",
            "summary",
            "Short summary",
            "Make it more inviting.",
            "Post title",
            "Short summary",
            "post-title",
            null,
            new Dictionary<string, string> { ["publicationState"] = "draft" });

        var prompt = builder.Build(request);

        prompt.Should().Contain("enhancedText");
        prompt.Should().Contain("warnings");
        prompt.Should().Contain("Make it more inviting.");
        prompt.Should().Contain("publicationState");
        await Assert.That(prompt).DoesNotContain("```");
    }
}
