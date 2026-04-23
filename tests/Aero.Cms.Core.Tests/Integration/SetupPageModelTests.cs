using Aero.Cms.Modules.Setup.Areas.MyFeature.Pages;
using FluentAssertions;

namespace Aero.Cms.Core.Tests.Integration;

public class SetupPageModelTests
{
    [Test]
    public async Task Step_1_validation_blocks_moving_next_when_main_info_is_incomplete()
    {
        var model = CreateModel();
        model.CurrentStep = 1;
        model.Input.SiteName = string.Empty;
        model.Input.HomepageTitle = string.Empty;

        model.NextStep();

        await Assert.That(model.CurrentStep).IsEqualTo(1);
        model.StatusMessage.Should().Be("Site name is required.");
        model.HasValidationErrors.Should().BeTrue();
    }

    [Test]
    public async Task Server_database_mode_requires_a_connection_string_on_step_2()
    {
        var model = CreateModel();
        model.CurrentStep = 2;
        model.Input.DatabaseMode = "Server";
        model.Input.ConnectionString = string.Empty;

        model.NextStep();

        await Assert.That(model.CurrentStep).IsEqualTo(2);
        model.StatusMessage.Should().Be("A database connection string is required when Database is set to Server.");
        model.HasValidationErrors.Should().BeTrue();
    }

    [Test]
    public async Task Infisical_selection_requires_machine_id_and_client_secret_on_step_4()
    {
        var model = CreateModel();
        model.CurrentStep = 4;
        model.Input.SecretProvider = "Infisical";
        model.Input.InfisicalMachineId = string.Empty;
        model.Input.InfisicalClientSecret = string.Empty;

        model.NextStep();

        await Assert.That(model.CurrentStep).IsEqualTo(4);
        model.StatusMessage.Should().Be("Infisical machine id is required.");
        model.HasValidationErrors.Should().BeTrue();
    }

    [Test]
    public async Task Infisical_selection_requires_client_secret_when_machine_id_is_present()
    {
        var model = CreateModel();
        model.CurrentStep = 4;
        model.Input.SecretProvider = "Infisical";
        model.Input.InfisicalMachineId = "machine-id";
        model.Input.InfisicalClientSecret = string.Empty;

        model.NextStep();

        await Assert.That(model.CurrentStep).IsEqualTo(4);
        model.StatusMessage.Should().Be("Infisical client secret is required.");
        model.HasValidationErrors.Should().BeTrue();
    }

    [Test]
    public async Task Password_mismatch_blocks_final_step_progression()
    {
        var model = CreateModel();
        model.CurrentStep = 5;
        model.Input.Password = "correct horse battery";
        model.Input.ConfirmPassword = "different password";

        model.NextStep();

        await Assert.That(model.CurrentStep).IsEqualTo(5);
        model.StatusMessage.Should().Be("Passwords must match.");
        model.HasValidationErrors.Should().BeTrue();
    }

    private static Setup CreateModel()
    {
        return new Setup
        {
            Input = new SetupInput
            {
                DatabaseMode = "Embedded",
                CacheMode = "Memory",
                SecretProvider = "Local Certificate",
                AdminUserName = "admin.user",
                AdminEmail = "admin@example.com",
                Password = "correct horse battery",
                ConfirmPassword = "correct horse battery",
                SiteName = "Aero CMS",
                HomepageTitle = "Welcome to Aero CMS",
                BlogName = "Field Notes",
                Hostname = "localhost",
                DefaultCulture = "en-US"
            }
        };
    }
}
