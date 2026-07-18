using Aero.Cms.Modules.Setup.Areas.Setup.Pages;
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

        await model.NextStep();

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

        await model.NextStep();

        await Assert.That(model.CurrentStep).IsEqualTo(2);
        model.StatusMessage.Should().Be("A database connection string is required when Database is set to Server.");
        model.HasValidationErrors.Should().BeTrue();
    }

    [Test]
    public async Task Server_database_mode_requires_credentials_when_unauthenticated_access_is_disabled()
    {
        var model = CreateModel();
        model.CurrentStep = 2;
        model.Input.DatabaseMode = "Server";
        model.Input.ConnectionString = "ws://localhost:8000/rpc";
        model.Input.DatabaseUnauthenticated = false;
        model.Input.DatabaseUsername = string.Empty;
        model.Input.DatabasePassword = string.Empty;

        await model.NextStep();

        await Assert.That(model.CurrentStep).IsEqualTo(2);
        model.StatusMessage.Should().Be("A database username is required unless unauthenticated access is enabled.");
        model.HasValidationErrors.Should().BeTrue();
    }

    [Test]
    public async Task Server_database_mode_allows_unauthenticated_connections_without_credentials()
    {
        var model = CreateModel();
        model.CurrentStep = 2;
        model.Input.DatabaseMode = "Server";
        model.Input.ConnectionString = "ws://localhost:8000/rpc";
        model.Input.DatabaseUnauthenticated = true;
        model.Input.DatabaseUsername = null;
        model.Input.DatabasePassword = null;

        await model.NextStep();

        await Assert.That(model.CurrentStep).IsEqualTo(3);
        model.StatusMessage.Should().BeNull();
        model.HasValidationErrors.Should().BeFalse();
    }

    [Test]
    public async Task Setup_input_defaults_server_endpoint_to_local_surreal_rpc()
    {
        var input = new SetupInput();

        await Assert.That(input.ConnectionString).IsEqualTo("ws://localhost:8000/rpc");
    }

    [Test]
    public async Task Setup_input_defaults_cache_mode_to_local_garnet()
    {
        var input = new SetupInput();

        await Assert.That(input.CacheMode).IsEqualTo("Local");
    }

    [Test]
    public async Task Local_cache_mode_does_not_block_step_3_progression_on_readiness()
    {
        var model = CreateModel();
        model.CurrentStep = 3;
        model.Input.CacheMode = "Local";

        await model.NextStep();

        await Assert.That(model.CurrentStep).IsEqualTo(4);
        model.StatusMessage.Should().BeNull();
        model.HasValidationErrors.Should().BeFalse();
    }

    [Test]
    public async Task Infisical_selection_requires_machine_id_and_client_secret_on_step_4()
    {
        var model = CreateModel();
        model.CurrentStep = 4;
        model.Input.SecretProvider = "Infisical";
        model.Input.InfisicalMachineId = string.Empty;
        model.Input.InfisicalClientSecret = string.Empty;

        await model.NextStep();

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

        await model.NextStep();

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
                CacheMode = "Local",
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
