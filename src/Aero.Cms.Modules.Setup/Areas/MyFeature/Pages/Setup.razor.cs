using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Aero.Cms.Modules.Setup.Bootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Setup.Areas.MyFeature.Pages;

public partial class Setup : ComponentBase, IAsyncDisposable
{
    private const int TotalSteps = 6;

    [Inject]
    private ISetupBootstrapHandoffService SetupBootstrapHandoffService { get; set; } = default!;

    [Inject]
    private ILogger<Setup> Logger { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter]
    public string? ReturnUrl { get; set; }

    public SetupInput Input { get; set; } = new();

    public string? StatusMessage { get; set; }

    public bool ShowPassword { get; set; }
    public bool ShowConfirmPassword { get; set; }

    // Service readiness status
    public bool PostgresReady { get; set; }
    public bool GarnetReady { get; set; }

    // Computed properties for conditional display
    public bool ShowConnectionString => Input.DatabaseMode == "Server";
    public bool ShowCacheConnectionString => Input.CacheMode == "Server";
    public bool ShowInfisicalFields => Input.SecretProvider == "Infisical";

    public bool RequiresPostgres => Input.DatabaseMode == "Embedded";
    public bool RequiresGarnet => Input.CacheMode == "Embedded";

    public bool IsReady => (!RequiresPostgres || PostgresReady) && (!RequiresGarnet || GarnetReady);
    public bool IsSubmitting { get; set; }

    // Debug probe - remove after confirming interactivity works
    private int _probeCount;

    public string ReadinessMessage => BuildReadinessMessage();
    public int CurrentStep { get; set; } = 1;
    public bool IsLastStep => CurrentStep == TotalSteps;
    public bool CanMoveNext => ValidateCurrentStep(false);
    public double ProgressPercent => CurrentStep * 100d / TotalSteps;
    public string CurrentStepTitle => GetStepName(CurrentStep);
    public string CurrentStepDescription => GetStepSummary(CurrentStep);
    public string EffectiveDatabaseMode => NormalizeMode(Input.DatabaseMode, "Embedded");
    public string EffectiveCacheMode => NormalizeMode(Input.CacheMode, "Memory");
    public string EffectiveSecretProvider => NormalizeMode(Input.SecretProvider, "Local Certificate");

    public bool HasValidationErrors { get; set; }

    private PeriodicTimer? _statusTimer;
    private CancellationTokenSource? _pollingCts;

    protected override void OnInitialized()
    {
        // Set default values
        Input ??= new SetupInput
        {
            DatabaseMode = "Embedded",
            CacheMode = "Memory",
            SecretProvider = "Local Certificate",
            AdminUserName = "admin",
            AdminEmail = "hello@aerocms.com",
            SiteName = "Aero CMS",
            HomepageTitle = "Welcome to Aero CMS",
            BlogName = "Blog",
            Hostname = "localhost",
            DefaultCulture = "en-US"
        };

#if DEBUG
        // In debug mode, prefill passwords
        Input.Password = "*strongPassword1";
        Input.ConfirmPassword = "*strongPassword1";
#endif
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await StartPollingAsync();
        }
    }

    private async Task StartPollingAsync()
    {
        _pollingCts = new CancellationTokenSource();
        _statusTimer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        await RefreshSetupStatusAsync(_pollingCts.Token);

        _ = Task.Run(async () =>
        {
            try
            {
                while (_statusTimer != null && await _statusTimer.WaitForNextTickAsync(_pollingCts.Token))
                {
                    await RefreshSetupStatusAsync(_pollingCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, _pollingCts.Token);
    }

    private async Task RefreshSetupStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(NavigationManager.BaseUri) };
            var status = await client.GetFromJsonAsync<SetupStatusResponse>("setup/status", cancellationToken);

            if (status is null)
            {
                return;
            }

            PostgresReady = status.PostgresReady;
            GarnetReady = status.GarnetReady;
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to refresh setup readiness status.");
        }
    }

    public void TogglePassword()
    {
        ShowPassword = !ShowPassword;
    }

    public void ToggleConfirmPassword()
    {
        ShowConfirmPassword = !ShowConfirmPassword;
    }

    public async Task ShowTestAlert()
    {
        StatusMessage = "Test button clicked.";
        await JSRuntime.InvokeVoidAsync("alert", "Blazor click handler is working.");
        await InvokeAsync(StateHasChanged);
    }

    public async Task NextStep()
    {
        if (!ValidateCurrentStep(true))
        {
            return;
        }

        if (CurrentStep < TotalSteps)
        {
            CurrentStep++;
            HasValidationErrors = false;
            StatusMessage = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    public async Task PreviousStep()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
            HasValidationErrors = false;
            StatusMessage = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    public string GetFieldClass(string key)
    {
        // For now, return default styling
        // TODO: Add validation state tracking
        return "h-12 w-full px-4 rounded-xl border border-slate-200 bg-slate-50/50 text-sm focus:bg-white focus:border-indigo-500 focus:ring-4 focus:ring-indigo-50 outline-none transition-all";
    }

    protected async Task HandleSubmit()
    {
        HasValidationErrors = false;

        if (!ValidateCurrentStep(true))
        {
            return;
        }

        var secretProvider = NormalizeMode(Input.SecretProvider, "Local Certificate");
        var databaseMode = NormalizeMode(Input.DatabaseMode, "Embedded");
        var cacheMode = NormalizeMode(Input.CacheMode, "Memory");

        if (databaseMode.Equals("Server", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Input.ConnectionString))
        {
            HasValidationErrors = true;
            StatusMessage = "A database connection string is required when Database is set to Server.";
            return;
        }

        if (cacheMode.Equals("Server", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Input.CacheConnectionString))
        {
            HasValidationErrors = true;
            StatusMessage = "A cache connection string is required when Cache is set to Server.";
            return;
        }

        if (!IsReady)
        {
            HasValidationErrors = true;
            StatusMessage = ReadinessMessage;
            return;
        }

        // Show transition message before calling handoff service
        IsSubmitting = true;
        StatusMessage = "Setup complete! Starting main application...";
        Logger.LogInformation("Setup form submitted. Triggering bootstrap handoff...");
        
        // Force UI update to show the message before the async operation
        await InvokeAsync(StateHasChanged);

        // Create the seed request with all setup configuration
        var seedRequest = new SeedDatabaseRequest(
            databaseMode,
            cacheMode,
            secretProvider,
            Input.ConnectionString,
            Input.CacheConnectionString,
            Input.InfisicalMachineId,
            Input.InfisicalClientSecret,
            Input.AdminUserName,
            Input.AdminEmail,
            Input.Password,
            Input.SiteName,
            Input.HomepageTitle,
            Input.BlogName,
            Input.Hostname,
            Input.DefaultCulture);

        // Call the handoff service which will:
        // 1. Persist bootstrap configuration
        // 2. Save pending seed request
        // 3. Mark bootstrap as Configured
        // 4. Trigger StopApplication() to transition to main app
        var result = await SetupBootstrapHandoffService.CompleteAndHandoffAsync(seedRequest);

        if (!result.Succeeded)
        {
            IsSubmitting = false;
            HasValidationErrors = true;
            StatusMessage = $"Setup failed: {string.Join("; ", result.Errors)}";
            Logger.LogError("Setup bootstrap handoff failed: {Errors}", string.Join("; ", result.Errors));
        }
        // If successful, the app will shut down and the main app will start automatically
        // The user will see the "Setup complete! Starting main application..." message
    }

    private static string NormalizeMode(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private bool ValidateCurrentStep(bool showMessage)
    {
        string? error = CurrentStep switch
        {
            1 when string.IsNullOrWhiteSpace(Input.SiteName) => "Site name is required.",
            1 when string.IsNullOrWhiteSpace(Input.HomepageTitle) => "Homepage title is required.",
            1 when string.IsNullOrWhiteSpace(Input.BlogName) => "Blog name is required.",
            1 when string.IsNullOrWhiteSpace(Input.Hostname) => "Hostname is required.",
            1 when string.IsNullOrWhiteSpace(Input.DefaultCulture) => "Default culture is required.",
            2 when string.Equals(Input.DatabaseMode, "Server", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Input.ConnectionString)
                => "A database connection string is required when Database is set to Server.",
            3 when string.Equals(Input.CacheMode, "Server", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Input.CacheConnectionString)
                => "A cache connection string is required when Cache is set to Server.",
            3 when string.Equals(Input.CacheMode, "Embedded", StringComparison.OrdinalIgnoreCase) && !GarnetReady
                => "Wait for embedded Garnet cache to become ready before continuing.",
            4 when string.Equals(Input.SecretProvider, "Infisical", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Input.InfisicalMachineId)
                => "Infisical machine id is required.",
            4 when string.Equals(Input.SecretProvider, "Infisical", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Input.InfisicalClientSecret)
                => "Infisical client secret is required.",
            5 when string.IsNullOrWhiteSpace(Input.AdminUserName) => "Admin username is required.",
            5 when string.IsNullOrWhiteSpace(Input.AdminEmail) => "Admin email is required.",
            5 when string.IsNullOrWhiteSpace(Input.Password) => "Admin password is required.",
            5 when string.IsNullOrWhiteSpace(Input.ConfirmPassword) => "Please confirm the admin password.",
            5 when !string.Equals(Input.Password, Input.ConfirmPassword, StringComparison.Ordinal) => "Passwords must match.",
            _ => null
        };

        if (!showMessage)
        {
            return error is null;
        }

        if (error is null)
        {
            HasValidationErrors = false;
            return true;
        }

        HasValidationErrors = true;
        StatusMessage = error;
        return false;
    }

    public string GetStepName(int step) => step switch
    {
        1 => "CMS Info",
        2 => "Database",
        3 => "Cache",
        4 => "Secrets",
        5 => "Admin",
        6 => "Review",
        _ => "Setup"
    };

    public string GetStepSummary(int step) => step switch
    {
        1 => "Site name, culture, homepage, and blog metadata.",
        2 => "Embedded or server database connectivity.",
        3 => "Memory, embedded, or server cache configuration.",
        4 => "Local Certificate or Infisical secret handling.",
        5 => "Create the initial CMS administrator account.",
        6 => "Review your selections before initialization.",
        _ => string.Empty
    };

    private string BuildReadinessMessage()
    {
        var waitingOn = new List<string>();

        if (RequiresPostgres && !PostgresReady)
        {
            waitingOn.Add("embedded PostgreSQL");
        }

        if (RequiresGarnet && !GarnetReady)
        {
            waitingOn.Add("embedded Garnet cache");
        }

        return waitingOn.Count == 0
            ? "Required local services are ready."
            : $"Waiting for {string.Join(" and ", waitingOn)} to become ready.";
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_pollingCts is not null)
            {
                await _pollingCts.CancelAsync();
                _pollingCts.Dispose();
            }
        }
        catch
        {
        }

        _statusTimer?.Dispose();
    }
}

public sealed class SetupStatusResponse
{
    public bool PostgresReady { get; set; }
    public bool GarnetReady { get; set; }
    public bool RequiresPostgres { get; set; }
    public bool RequiresGarnet { get; set; }
    public bool IsReady { get; set; }
}

public class SetupInput
{
    [Required]
    public string DatabaseMode { get; set; } = "Embedded";

    [Required]
    public string CacheMode { get; set; } = "Memory";

    [Required]
    public string SecretProvider { get; set; } = "Local Certificate";

    public string? ConnectionString { get; set; }

    public string? CacheConnectionString { get; set; }

    public string? InfisicalMachineId { get; set; }

    public string? InfisicalClientSecret { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string AdminUserName { get; set; } = "admin";

    [Required]
    [EmailAddress]
    public string AdminEmail { get; set; } = "hello@aerocms.com";

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = "";

    [Required]
    [Compare("Password")]
    public string ConfirmPassword { get; set; } = "";

    [Required]
    [StringLength(100)]
    public string SiteName { get; set; } = "Aero CMS";

    [Required]
    [StringLength(100)]
    public string HomepageTitle { get; set; } = "Welcome";

    [Required]
    [StringLength(100)]
    public string BlogName { get; set; } = "Blog";

    [Required]
    [StringLength(256)]
    public string Hostname { get; set; } = "localhost";

    [Required]
    [StringLength(10)]
    public string DefaultCulture { get; set; } = "en-US";
}
