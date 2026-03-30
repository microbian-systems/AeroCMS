using System.ComponentModel.DataAnnotations;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Areas.MyFeature.Pages;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Integration;

public class SetupPageModelTests
{
    [Test]
    public async Task Invalid_setup_post_stays_on_the_page()
    {
        var completionService = Substitute.For<ISetupCompletionService>();
        var model = new SetupModel(completionService);
        model.ModelState.AddModelError("Input.AdminUserName", "Required");

        var result = await model.OnPostAsync(CancellationToken.None);

        await Assert.That(result).IsTypeOf<PageResult>();
        await completionService.DidNotReceive()
            .CompleteAsync(Arg.Any<SeedDatabaseRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Setup_input_model_requires_expected_fields_and_matching_passwords()
    {
        var input = new SetupModel.SetupInputModel
        {
            AdminUserName = "ab",
            AdminEmail = "not-an-email",
            Password = "short",
            ConfirmPassword = "different",
            SiteName = string.Empty,
            HomepageTitle = string.Empty,
            BlogName = string.Empty
        };

        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(input, new ValidationContext(input), validationResults, validateAllProperties: true);

        await Assert.That(isValid).IsFalse();
        validationResults.Should().Contain(result => result.MemberNames.Contains(nameof(SetupModel.SetupInputModel.AdminUserName)));
        validationResults.Should().Contain(result => result.MemberNames.Contains(nameof(SetupModel.SetupInputModel.AdminEmail)));
        validationResults.Should().Contain(result => result.MemberNames.Contains(nameof(SetupModel.SetupInputModel.Password)));
        validationResults.Should().Contain(result => result.MemberNames.Contains(nameof(SetupModel.SetupInputModel.ConfirmPassword)));
        validationResults.Should().Contain(result => result.MemberNames.Contains(nameof(SetupModel.SetupInputModel.SiteName)));
        validationResults.Should().Contain(result => result.MemberNames.Contains(nameof(SetupModel.SetupInputModel.HomepageTitle)));
        validationResults.Should().Contain(result => result.MemberNames.Contains(nameof(SetupModel.SetupInputModel.BlogName)));
    }

    [Test]
    public async Task Valid_setup_post_rejects_external_return_urls()
    {
        var model = CreateValidModel();
        model.ReturnUrl = "https://evil.example/phish";

        var result = await model.OnPostAsync(CancellationToken.None);

        var redirect = result.Should().BeOfType<LocalRedirectResult>().Subject;
        redirect.Url.Should().Be(SetupPathAllowlist.SetupPath);
        model.StatusMessage.Should().NotBeNullOrWhiteSpace();

        await Assert.That(redirect.Url).IsEqualTo(SetupPathAllowlist.SetupPath);
    }

    [Test]
    public async Task Valid_setup_post_keeps_local_return_urls()
    {
        var model = CreateValidModel();
        model.ReturnUrl = "/admin";

        var result = await model.OnPostAsync(CancellationToken.None);

        var redirect = result.Should().BeOfType<LocalRedirectResult>().Subject;
        redirect.Url.Should().Be("/admin");

        await Assert.That(redirect.Url).IsEqualTo("/admin");
    }

    [Test]
    public async Task Bootstrap_failures_are_returned_to_the_page()
    {
        var completionService = Substitute.For<ISetupCompletionService>();
        completionService.CompleteAsync(Arg.Any<SeedDatabaseRequest>(), Arg.Any<CancellationToken>())
            .Returns(SeedDatabaseResult.Failure("Password policy failed."));

        var model = CreateValidModel(completionService);

        var result = await model.OnPostAsync(CancellationToken.None);

        await Assert.That(result).IsTypeOf<PageResult>();
        model.ModelState[string.Empty]!.Errors.Should().Contain(error => error.ErrorMessage == "Password policy failed.");
    }

    private static SetupModel CreateValidModel(ISetupCompletionService? completionService = null)
    {
        if (completionService == null)
        {
            completionService = Substitute.For<ISetupCompletionService>();
            completionService.CompleteAsync(Arg.Any<SeedDatabaseRequest>(), Arg.Any<CancellationToken>())
                .Returns(new SeedDatabaseResult { CreatedAdmin = true });
        }

        return new SetupModel(completionService)
        {
            Input = new SetupModel.SetupInputModel
            {
                AdminUserName = "admin.user",
                AdminEmail = "admin@example.com",
                Password = "correct horse battery",
                ConfirmPassword = "correct horse battery",
                SiteName = "Aero CMS",
                HomepageTitle = "Welcome to Aero CMS",
                BlogName = "Field Notes"
            }
        };
    }
}
