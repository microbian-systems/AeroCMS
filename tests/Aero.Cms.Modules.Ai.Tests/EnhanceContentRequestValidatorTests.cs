using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Modules.Ai.Validation;
using FluentAssertions;

namespace Aero.Cms.Modules.Ai.Tests;

public sealed class EnhanceContentRequestValidatorTests
{
    private readonly EnhanceContentRequestValidator validator = new();

    [Test]
    public async Task ValidPostBodyRequest_ShouldPass()
    {
        var request = new EnhanceContentRequest(
            "post",
            "body",
            "# Hello",
            "Sharpen this.",
            "Hello",
            "Summary",
            "hello",
            null,
            null);

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public async Task UnsupportedTargetField_ShouldFail()
    {
        var request = new EnhanceContentRequest(
            "post",
            "authorBio",
            "Text",
            null,
            null,
            null,
            null,
            null,
            null);

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage.Contains("Target field"));
    }

    [Test]
    public async Task OversizedCurrentText_ShouldFail()
    {
        var request = new EnhanceContentRequest(
            "post",
            "body",
            new string('a', 30_001),
            null,
            null,
            null,
            null,
            null,
            null);

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage.Contains("30,000"));
    }
}
