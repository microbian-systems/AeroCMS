using Aero.Cms.Modules.Ai.Services;
using FluentAssertions;

namespace Aero.Cms.Modules.Ai.Tests;

public sealed class EnhanceContentAgentOutputParserTests
{
    [Test]
    public async Task Deserialize_ShouldParseFencedJson()
    {
        var response = """
            ```json
            {
              "enhancedText": "Better text",
              "rationale": "Cleaned it up.",
              "warnings": []
            }
            ```
            """;

        var output = EnhanceContentAgentOutputParser.Deserialize(response, new(System.Text.Json.JsonSerializerDefaults.Web));

        output.Should().NotBeNull();
        output!.EnhancedText.Should().Be("Better text");
        output.Rationale.Should().Be("Cleaned it up.");
        output.Warnings.Should().BeEmpty();
        await Assert.That(output.EnhancedText).IsEqualTo("Better text");
    }

    [Test]
    public async Task Deserialize_ShouldRecoverLiteralNewlinesInsideEnhancedText()
    {
        var response = """
            ```json
            {
              "enhancedText": "## Title

            *   First point
            *   Second point",
              "rationale": "Expanded the content.",
              "warnings": []
            }
            ```
            """;

        var output = EnhanceContentAgentOutputParser.Deserialize(response, new(System.Text.Json.JsonSerializerDefaults.Web));

        output.Should().NotBeNull();
        output!.EnhancedText.Should().Contain("## Title");
        output.EnhancedText.Should().Contain("*   Second point");
        output.Rationale.Should().Be("Expanded the content.");
        await Assert.That(output.EnhancedText).Contains("*   First point");
    }

    [Test]
    public async Task Deserialize_ShouldRecoverMissingCommaBeforeRationale()
    {
        var response = """
            ```json
            {
              "enhancedText": "Better text"
              "rationale": "No comma after enhanced text.",
              "warnings": []
            }
            ```
            """;

        var output = EnhanceContentAgentOutputParser.Deserialize(response, new(System.Text.Json.JsonSerializerDefaults.Web));

        output.Should().NotBeNull();
        output!.EnhancedText.Should().Be("Better text");
        output.Rationale.Should().Be("No comma after enhanced text.");
        await Assert.That(output.Rationale).IsEqualTo("No comma after enhanced text.");
    }

    [Test]
    public async Task Deserialize_ShouldRecoverLmStudioMarkdownResponseShape()
    {
        var response = """
            ```json
            {
              "enhancedText": "## CMS and CRM Software

            Imagine you are building with \"bricks\".
            Need to announce a new product? No problem!
            *   **Consistent Look:** Templates keep the website tidy."
              "rationale": "Expanded on the original prompt.",
              "warnings": []
            }
            ```
            """;

        var output = EnhanceContentAgentOutputParser.Deserialize(response, new(System.Text.Json.JsonSerializerDefaults.Web));

        output.Should().NotBeNull();
        output!.EnhancedText.Should().Contain("Need to announce a new product? No problem!");
        output.EnhancedText.Should().Contain("\"bricks\"");
        output.Rationale.Should().Be("Expanded on the original prompt.");
        await Assert.That(output.Warnings).IsEmpty();
    }
}
