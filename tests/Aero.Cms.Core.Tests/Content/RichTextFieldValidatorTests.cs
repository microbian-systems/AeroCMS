using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Services;
using FluentValidation;

namespace Aero.Cms.Core.Tests.Content;

public sealed class RichTextFieldValidatorTests
{
    [Test]
    public async Task Accepts_safe_semantic_html()
    {
        var result = Validate("<p>Hello <strong>world</strong>. <a href=\"https://example.com\">Learn more</a></p>");

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    [Arguments("<script>alert('x')</script><p>Hello</p>")]
    [Arguments("<p onclick=\"alert('x')\">Hello</p>")]
    [Arguments("<a href=\"javascript:alert('x')\">Unsafe</a>")]
    [Arguments("<img src=\"https://example.com/tracker.png\" alt=\"tracker\">")]
    [Arguments("<p class=\"arbitrary\" style=\"position:fixed\">Hello</p>")]
    public async Task Rejects_html_changed_by_the_security_allowlist(string html)
    {
        var result = Validate(html);

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Single().ErrorMessage).Contains("unsafe HTML");
    }

    private static FluentValidation.Results.ValidationResult Validate(string html)
    {
        var definition = new ContentTypeDefinition
        {
            Alias = "animal",
            Name = "Animal",
            Fields =
            [
                new ContentFieldDefinition
                {
                    Name = "description",
                    Label = "Description",
                    FieldType = ContentFieldTypes.RichText
                }
            ]
        };
        var item = new ContentItem
        {
            ContentTypeAlias = "animal",
            Slug = "okapi",
            Fields = new Dictionary<string, JsonElement>
            {
                ["description"] = JsonSerializer.SerializeToElement(html)
            }
        };

        return new DynamicContentValidator(
                definition,
                ContentValidationMode.Draft,
                [new RichTextFieldValidator()])
            .Validate(item);
    }
}
