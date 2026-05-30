using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Aero.Cms.Modules.Setup.Bootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Aero.Cms.Modules.Setup.Areas.Setup.Pages;

public partial class Setup : ComponentBase
{
    private const int TotalSteps = 6;

    [Inject]
    private ISetupBootstrapHandoffService SetupBootstrapHandoffService { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    private ILogger<Setup> Logger { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

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

    public bool IsReady => true;
    public bool IsSubmitting { get; set; }

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
    public string EffectiveAuthenticationMode => NormalizeMode(Input.AuthenticationMode, "Local");
    public IReadOnlyList<CultureOption> CommonCultureOptions { get; } =
    [
        new("en-US", "English (United States)"),
        new("en-GB", "English (United Kingdom)"),
        new("es-MX", "Spanish (Mexico)"),
        new("es-ES", "Spanish (Spain)"),
        new("fr-FR", "French (France)"),
        new("de-DE", "German (Germany)"),
        new("it-IT", "Italian (Italy)"),
        new("pt-BR", "Portuguese (Brazil)"),
        new("nl-NL", "Dutch (Netherlands)"),
        new("pl-PL", "Polish (Poland)"),
        new("ru-RU", "Russian (Russia)"),
        new("zh-CN", "Chinese (Simplified)"),
        new("ja-JP", "Japanese (Japan)"),
        new("ko-KR", "Korean (Korea)"),
        new("ar-SA", "Arabic (Saudi Arabia)"),
        new("he-IL", "Hebrew (Israel)")
    ];

    public bool HasValidationErrors { get; set; }

    protected override void OnInitialized()
    {
        // Set default values
        Input ??= new SetupInput
        {
            DatabaseMode = "Embedded",
            CacheMode = "Memory",
            SecretProvider = "Local Certificate",
            AuthenticationMode = "Local",
            AdminUserName = "admin",
            AdminEmail = "hello@getaerocms.net",
            SiteName = "Aero CMS",
            HomepageTitle = "Welcome to Aero CMS",
            BlogName = "Blog",
            Hostname = "localhost",
            DefaultCulture = "en-US",
            SupportedCultures = ["en-US"]
        };

        EnsureSupportedCulturesContainDefault();

#if DEBUG
        // In debug mode, prefill passwords
        Input.Password = "*strongPassword1";
        Input.ConfirmPassword = "*strongPassword1";
#endif
    }

    public void TogglePassword()
    {
        ShowPassword = !ShowPassword;
    }

    public void ToggleConfirmPassword()
    {
        ShowConfirmPassword = !ShowConfirmPassword;
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

    public bool IsCultureSelected(string culture)
        => Input.SupportedCultures.Any(selected => string.Equals(selected, culture, StringComparison.OrdinalIgnoreCase));

    public void ToggleCulture(string culture, ChangeEventArgs args)
    {
        var isChecked = args.Value is bool checkedValue && checkedValue;
        var normalizedCulture = NormalizeCultureName(culture);

        if (isChecked)
        {
            if (!IsCultureSelected(normalizedCulture))
            {
                Input.SupportedCultures.Add(normalizedCulture);
            }
        }
        else
        {
            Input.SupportedCultures.RemoveAll(selected => string.Equals(selected, normalizedCulture, StringComparison.OrdinalIgnoreCase));
        }

        if (Input.SupportedCultures.Count == 0)
        {
            Input.SupportedCultures.Add(Input.DefaultCulture);
        }

        if (!IsCultureSelected(Input.DefaultCulture))
        {
            Input.DefaultCulture = Input.SupportedCultures[0];
        }
    }

    public void OnDefaultCultureChanged(ChangeEventArgs args)
    {
        Input.DefaultCulture = NormalizeCultureName(args.Value?.ToString());
        EnsureSupportedCulturesContainDefault();
    }

    protected async Task HandleSubmit()
    {
        HasValidationErrors = false;
        Input.AuthenticationMode = "Local";

        if (!ValidateCurrentStep(true))
        {
            return;
        }

        EnsureSupportedCulturesContainDefault();

        var secretProvider = NormalizeMode(Input.SecretProvider, "Local Certificate");
        var databaseMode = NormalizeMode(Input.DatabaseMode, "Embedded");
        var cacheMode = NormalizeMode(Input.CacheMode, "Memory");
        var authenticationMode = NormalizeMode(Input.AuthenticationMode, "Local");

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

        // Show transition message before calling handoff service
        IsSubmitting = true;
        StatusMessage = "Setup complete! Starting main application...";
        Logger.LogInformation("Setup form submitted. Triggering bootstrap handoff...");
        
        // Force UI update to show the message before the async operation
        await InvokeAsync(StateHasChanged);

        // Clear browser storage so the fresh app starts clean
        await JSRuntime.InvokeVoidAsync("aero.setup.clearStorage");

        // Create the seed request with all setup configuration
        var seedRequest = new SeedDatabaseRequest(
            databaseMode,
            cacheMode,
            secretProvider,
            authenticationMode,
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
            Input.DefaultCulture,
            Input.SupportedCultures);

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
        EnsureSupportedCulturesContainDefault();

        string? error = CurrentStep switch
        {
            1 when string.IsNullOrWhiteSpace(Input.SiteName) => "Site name is required.",
            1 when string.IsNullOrWhiteSpace(Input.HomepageTitle) => "Homepage title is required.",
            1 when string.IsNullOrWhiteSpace(Input.BlogName) => "Blog name is required.",
            1 when string.IsNullOrWhiteSpace(Input.Hostname) => "Hostname is required.",
            1 when string.IsNullOrWhiteSpace(Input.DefaultCulture) => "Default culture is required.",
            1 when Input.SupportedCultures.Count == 0 => "Select at least one supported culture.",
            1 when !IsCultureSelected(Input.DefaultCulture) => "Default culture must be selected as a supported culture.",
            2 when string.Equals(Input.DatabaseMode, "Server", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Input.ConnectionString)
                => "A database connection string is required when Database is set to Server.",
            3 when string.Equals(Input.CacheMode, "Server", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Input.CacheConnectionString)
                => "A cache connection string is required when Cache is set to Server.",
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
        5 => "Authentication",
        6 => "Review",
        _ => "Setup"
    };

    public string GetStepSummary(int step) => step switch
    {
        1 => "Site name, culture, homepage, and blog metadata.",
        2 => "Embedded or server database connectivity.",
        3 => "Memory, embedded, or server cache configuration.",
        4 => "Local Certificate or Infisical secret handling.",
        5 => "Choose the auth mode and create the initial CMS administrator account.",
        6 => "Review your selections before initialization.",
        _ => string.Empty
    };

    private string BuildReadinessMessage()
    {
        return "Readiness shown here is informational only. Embedded services will be started and validated after handoff to the main app.";
    }

    private void EnsureSupportedCulturesContainDefault()
    {
        Input.DefaultCulture = NormalizeCultureName(Input.DefaultCulture);

        Input.SupportedCultures = Input.SupportedCultures
            .Select(NormalizeCultureName)
            .Where(culture => !string.IsNullOrWhiteSpace(culture))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!IsCultureSelected(Input.DefaultCulture))
        {
            Input.SupportedCultures.Insert(0, Input.DefaultCulture);
        }
    }

    private static string NormalizeCultureName(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return "en-US";
        }

        try
        {
            return CultureInfo.GetCultureInfo(culture.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return "en-US";
        }
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

    [Required]
    public string AuthenticationMode { get; set; } = "Local";

    public string? ConnectionString { get; set; }

    public string? CacheConnectionString { get; set; }

    public string? InfisicalMachineId { get; set; }

    public string? InfisicalClientSecret { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string AdminUserName { get; set; } = "admin";

    [Required]
    [EmailAddress]
    public string AdminEmail { get; set; } = "hello@getaerocms.net";

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

    public List<string> SupportedCultures { get; set; } = ["en-US"];
}

public sealed record CultureOption(string Name, string DisplayName);
