using System.ComponentModel.DataAnnotations;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
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
        var pendingStore = Substitute.For<IBootstrapPendingSetupRequestStore>();
        var model = new SetupModel(
            Substitute.For<ISetupInitializationService>(),
            Substitute.For<IDatabaseBootstrapService>(),
            Substitute.For<ICacheBootstrapService>(),
            pendingStore);
        model.ModelState.AddModelError("Input.AdminUserName", "Required");

        var result = await model.OnPostAsync(CancellationToken.None);

        await Assert.That(result).IsTypeOf<PageResult>();
        await pendingStore.DidNotReceive()
            .SaveAsync(Arg.Any<SeedDatabaseRequest>(), Arg.Any<CancellationToken>());
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

        await Assert.That(result).IsTypeOf<PageResult>();
        model.StatusMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Valid_setup_post_keeps_local_return_urls()
    {
        var model = CreateValidModel();
        model.ReturnUrl = "/admin";

        var result = await model.OnPostAsync(CancellationToken.None);

        await Assert.That(result).IsTypeOf<PageResult>();
        model.StatusMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Bootstrap_failures_are_returned_to_the_page()
    {
        var pendingStore = Substitute.For<IBootstrapPendingSetupRequestStore>();
        pendingStore.SaveAsync(Arg.Any<SeedDatabaseRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Password policy failed.")));

        var model = CreateValidModel(pendingSetupRequestStore: pendingStore);

        var result = await model.OnPostAsync(CancellationToken.None);

        await Assert.That(result).IsTypeOf<PageResult>();
        model.ModelState[string.Empty]!.Errors.Should().Contain(error => error.ErrorMessage == "Password policy failed.");
    }

    [Test]
    public async Task Server_mode_requires_a_connection_string()
    {
        var model = CreateValidModel();
        model.Input.DatabaseMode = "Server";
        model.Input.ConnectionString = string.Empty;

        var result = await model.OnPostAsync(CancellationToken.None);

        await Assert.That(result).IsTypeOf<PageResult>();
        model.ModelState[nameof(SetupModel.SetupInputModel.ConnectionString)]!.Errors
            .Should().ContainSingle(error => error.ErrorMessage == "A server connection string is required for Server mode.");
    }

    [Test]
    public async Task Server_mode_persists_pending_setup_request_when_connection_string_is_present()
    {
        var pendingStore = Substitute.For<IBootstrapPendingSetupRequestStore>();
        var model = CreateValidModel(pendingSetupRequestStore: pendingStore);
        model.Input.DatabaseMode = "Server";
        model.Input.ConnectionString = "Host=localhost;Database=aero;Username=aero;Password=secret";

        var result = await model.OnPostAsync(CancellationToken.None);

        await Assert.That(result).IsTypeOf<PageResult>();
        await pendingStore.Received(1).SaveAsync(Arg.Any<SeedDatabaseRequest>(), Arg.Any<CancellationToken>());
        model.StatusMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Embedded_mode_persists_pending_setup_request()
    {
        var pendingStore = Substitute.For<IBootstrapPendingSetupRequestStore>();
        var model = CreateValidModel(pendingSetupRequestStore: pendingStore);
        model.Input.DatabaseMode = "Embedded";

        var result = await model.OnPostAsync(CancellationToken.None);

        await Assert.That(result).IsTypeOf<PageResult>();
        await pendingStore.Received(1)
            .SaveAsync(Arg.Any<SeedDatabaseRequest>(), Arg.Any<CancellationToken>());
        model.StatusMessage.Should().NotBeNullOrWhiteSpace();
    }

    private static SetupModel CreateValidModel(
        ISetupInitializationService? setupInitializationService = null,
        IDatabaseBootstrapService? databaseBootstrapService = null,
        ICacheBootstrapService? cacheBootstrapService = null,
        IBootstrapPendingSetupRequestStore? pendingSetupRequestStore = null)
    {
        return new SetupModel(
            setupInitializationService ?? Substitute.For<ISetupInitializationService>(),
            databaseBootstrapService ?? Substitute.For<IDatabaseBootstrapService>(),
            cacheBootstrapService ?? Substitute.For<ICacheBootstrapService>(),
            pendingSetupRequestStore ?? Substitute.For<IBootstrapPendingSetupRequestStore>())
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
