using Aero.Cms.Html;
using Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Html;

public sealed class TiptapInlineContentConverterTests
{
    [Test]
    public async Task FromDocumentJson_MapsSupportedMarksAndParagraphBreaks()
    {
        const string json = """
            {
              "type": "doc",
              "content": [
                {
                  "type": "paragraph",
                  "content": [
                    { "type": "text", "text": "Hello ", "marks": [{ "type": "bold" }] },
                    { "type": "text", "text": "Aero", "marks": [{ "type": "link", "attrs": { "href": "/about" } }] }
                  ]
                },
                { "type": "paragraph", "content": [{ "type": "text", "text": "Again" }] }
              ]
            }
            """;
        var converter = new TiptapInlineContentConverter();

        var result = converter.FromDocumentJson(json) as Result<IReadOnlyList<HtmlNode>>.Ok;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Count).IsEqualTo(4);
        await Assert.That(result.Value[0].TagName).IsEqualTo("strong");
        await Assert.That(result.Value[1].TagName).IsEqualTo("a");
        await Assert.That(result.Value[1].Attributes["href"]).IsEqualTo("/about");
        await Assert.That(result.Value[2].TagName).IsEqualTo("br");
        await Assert.That(result.Value[3].Text).IsEqualTo("Again");
    }

    [Test]
    public async Task FromDocumentJson_RejectsUnsupportedMarks()
    {
        const string json = """
            { "type": "doc", "content": [{ "type": "paragraph", "content": [
              { "type": "text", "text": "No", "marks": [{ "type": "code" }] }
            ]}]}
            """;

        var result = new TiptapInlineContentConverter().FromDocumentJson(json);

        await Assert.That(result).IsTypeOf<Result<IReadOnlyList<HtmlNode>>.Failure>();
    }

    [Test]
    public async Task ToEditorHtml_EncodesTextAndPreservesSupportedInlineMarkup()
    {
        var paragraph = HtmlNode.CreateElement("p");
        var emphasis = HtmlNode.CreateElement("em");
        emphasis.Children.Add(HtmlNode.CreateText("Aero <CMS>"));
        paragraph.Children.Add(emphasis);

        var result = new TiptapInlineContentConverter().ToEditorHtml(paragraph)
            as Result<string>.Ok;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo("<p><em>Aero &lt;CMS&gt;</em></p>");
    }
}
