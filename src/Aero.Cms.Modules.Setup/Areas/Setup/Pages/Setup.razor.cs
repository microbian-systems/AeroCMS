using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Aero.AppServer;
using Aero.Cms.Modules.Setup.Bootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Setup.Areas.Setup.Pages;

/// <summary>
/// Represents a class for Setup.
/// </summary>
public partial class Setup : ComponentBase
{
    private const int TotalSteps = 6;
    private const string DefaultServerDatabaseEndpoint = "ws://localhost:8000/rpc";

    [Inject]
    private ISetupBootstrapHandoffService SetupBootstrapHandoffService { get; set; } = default!;

    [Inject]
    private ILogger<Setup> Logger { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private IStringLocalizer<SetupResource> L { get; set; } = default!;

        /// <summary>
    /// Gets or sets the Return Url.
    /// </summary>
[Parameter]
    public string? ReturnUrl { get; set; }

        /// <summary>
    /// Gets or sets the Input.
    /// </summary>
public SetupInput Input { get; set; } = new();

        /// <summary>
    /// Gets or sets the Status Message.
    /// </summary>
public string? StatusMessage { get; set; }

        /// <summary>
    /// Gets or sets the Show Password.
    /// </summary>
public bool ShowPassword { get; set; }
        /// <summary>
    /// Gets or sets the Show Confirm Password.
    /// </summary>
public bool ShowConfirmPassword { get; set; }

    // Service readiness status
        /// <summary>
    /// Gets or sets the AeroDb Ready.
    /// </summary>
public bool AeroDbReady { get; set; }
        /// <summary>
    /// Gets or sets the Garnet Ready.
    /// </summary>
public bool GarnetReady { get; set; }

    // Computed properties for conditional display
        /// <summary>
    /// Gets or sets the Show Connection String.
    /// </summary>
public bool ShowConnectionString => Input.DatabaseMode == "Server";
        /// <summary>
    /// Gets or sets the Show Cache Connection String.
    /// </summary>
public bool ShowCacheConnectionString => Input.CacheMode == "Server";
        /// <summary>
    /// Gets or sets the Show Infisical Fields.
    /// </summary>
public bool ShowInfisicalFields => Input.SecretProvider == "Infisical";

        /// <summary>
    /// Gets or sets the Requires AeroDb.
    /// </summary>
public bool RequiresAeroDb => Input.DatabaseMode == "Embedded";
        /// <summary>
    /// Gets or sets the Requires Garnet.
    /// </summary>
public bool RequiresGarnet => Input.CacheMode == AeroAppServerConstants.LocalCacheMode;

        /// <summary>
    /// Gets or sets the Is Ready.
    /// </summary>
public bool IsReady => true;
        /// <summary>
    /// Gets or sets the Is Submitting.
    /// </summary>
public bool IsSubmitting { get; set; }

        /// <summary>
    /// Gets or sets the Readiness Message.
    /// </summary>
public string ReadinessMessage => BuildReadinessMessage();
        /// <summary>
    /// Gets or sets the Current Step.
    /// </summary>
public int CurrentStep { get; set; } = 1;
        /// <summary>
    /// Gets or sets the Is Last Step.
    /// </summary>
public bool IsLastStep => CurrentStep == TotalSteps;
        /// <summary>
    /// Gets or sets the Can Move Next.
    /// </summary>
public bool CanMoveNext => ValidateCurrentStep(false);
        /// <summary>
    /// Gets or sets the Progress Percent.
    /// </summary>
public double ProgressPercent => CurrentStep * 100d / TotalSteps;
        /// <summary>
    /// Gets or sets the Current Step Title.
    /// </summary>
public string CurrentStepTitle => GetStepName(CurrentStep);
        /// <summary>
    /// Gets or sets the Current Step Description.
    /// </summary>
public string CurrentStepDescription => GetStepSummary(CurrentStep);
        /// <summary>
    /// Gets or sets the Effective Database Mode.
    /// </summary>
public string EffectiveDatabaseMode => NormalizeMode(Input.DatabaseMode, "Embedded");
        /// <summary>
    /// Gets or sets the Effective Cache Mode.
    /// </summary>
public string EffectiveCacheMode => NormalizeMode(Input.CacheMode, AeroAppServerConstants.LocalCacheMode);
        /// <summary>
    /// Gets or sets the Effective Secret Provider.
    /// </summary>
public string EffectiveSecretProvider => NormalizeMode(Input.SecretProvider, "Local Certificate");
        /// <summary>
    /// Gets or sets the Effective Authentication Mode.
    /// </summary>
public string EffectiveAuthenticationMode => NormalizeMode(Input.AuthenticationMode, "Local");
        /// <summary>
    /// Gets or sets the Common Culture Options.
    /// </summary>
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
        new("he-IL", "Hebrew (Israel)"),
        new("hi-IN", "Hindi (India)"),
        new("uk-UA", "Ukrainian (Ukraine)")
    ];

        /// <summary>
    /// Gets or sets the Has Validation Errors.
    /// </summary>
public bool HasValidationErrors { get; set; }

        /// <summary>
    /// OnInitialized method.
    /// </summary>
protected override void OnInitialized()
    {
        // Set default values
        Input ??= new SetupInput
        {
            DatabaseMode = "Embedded",
            CacheMode = AeroAppServerConstants.LocalCacheMode,
            SecretProvider = "Local Certificate",
            AuthenticationMode = "Local",
            ConnectionString = DefaultServerDatabaseEndpoint,
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

        /// <summary>
    /// TogglePassword method.
    /// </summary>
public void TogglePassword()
    {
        ShowPassword = !ShowPassword;
    }

        /// <summary>
    /// ToggleConfirmPassword method.
    /// </summary>
public void ToggleConfirmPassword()
    {
        ShowConfirmPassword = !ShowConfirmPassword;
    }

        /// <summary>
    /// NextStep method.
    /// </summary>
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
        }

        await Task.CompletedTask;
    }

        /// <summary>
    /// PreviousStep method.
    /// </summary>
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

        /// <summary>
    /// GetFieldClass method.
    /// </summary>
public string GetFieldClass(string key)
    {
        // For now, return default styling
        // TODO: Add validation state tracking
        return "h-12 w-full px-4 rounded-xl border border-slate-200 bg-slate-50/50 text-sm focus:bg-white focus:border-indigo-500 focus:ring-4 focus:ring-indigo-50 outline-none transition-all";
    }

        /// <summary>
    /// IsCultureSelected method.
    /// </summary>
public bool IsCultureSelected(string culture)
        => Input.SupportedCultures.Any(selected => string.Equals(selected, culture, StringComparison.OrdinalIgnoreCase));

        /// <summary>
    /// ToggleCulture method.
    /// </summary>
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

        /// <summary>
    /// OnDefaultCultureChanged method.
    /// </summary>
public void OnDefaultCultureChanged(ChangeEventArgs args)
    {
        Input.DefaultCulture = NormalizeCultureName(args.Value?.ToString());
        EnsureSupportedCulturesContainDefault();
    }

        /// <summary>
    /// HandleSubmit method.
    /// </summary>
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
        var cacheMode = NormalizeMode(Input.CacheMode, AeroAppServerConstants.LocalCacheMode);
        var authenticationMode = NormalizeMode(Input.AuthenticationMode, "Local");

        if (!IsSupportedCacheMode(cacheMode))
        {
            HasValidationErrors = true;
            StatusMessage = "Cache mode must be Local or Server.";
            return;
        }

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
            Input.SupportedCultures)
        {
            DatabaseUnauthenticated = Input.DatabaseUnauthenticated,
            DatabaseUsername = Input.DatabaseUsername,
            DatabasePassword = Input.DatabasePassword
        };

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

    private static bool IsSupportedCacheMode(string cacheMode)
        => cacheMode.Equals(AeroAppServerConstants.LocalCacheMode, StringComparison.OrdinalIgnoreCase)
           || cacheMode.Equals(AeroAppServerConstants.ServerCacheMode, StringComparison.OrdinalIgnoreCase);

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
            2 when string.Equals(Input.DatabaseMode, "Server", StringComparison.OrdinalIgnoreCase) && !Input.DatabaseUnauthenticated && string.IsNullOrWhiteSpace(Input.DatabaseUsername)
                => "A database username is required unless unauthenticated access is enabled.",
            2 when string.Equals(Input.DatabaseMode, "Server", StringComparison.OrdinalIgnoreCase) && !Input.DatabaseUnauthenticated && string.IsNullOrWhiteSpace(Input.DatabasePassword)
                => "A database password is required unless unauthenticated access is enabled.",
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

        /// <summary>
    /// GetStepName method.
    /// </summary>
public string GetStepName(int step) => step switch
    {
        1 => L["CMS Info"],
        2 => L["Database"],
        3 => L["Cache"],
        4 => L["Secrets"],
        5 => L["Authentication"],
        6 => L["Review"],
        _ => L["Setup"]
    };

        /// <summary>
    /// GetStepSummary method.
    /// </summary>
public string GetStepSummary(int step) => step switch
    {
        1 => L["Site name, culture, homepage, and blog metadata."],
        2 => L["Embedded or server database connectivity."],
        3 => L["Local Garnet or remote server cache configuration."],
        4 => L["Local Certificate or Infisical secret handling."],
        5 => L["Choose the auth mode and create the initial CMS administrator account."],
        6 => L["Review your selections before initialization."],
        _ => string.Empty
    };

    private string BuildReadinessMessage()
    {
        return L["Readiness shown here is informational only. Local services will be started and validated after handoff to the main app."];
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

/// <summary>
/// Represents a class for SetupStatusResponse.
/// </summary>
public sealed class SetupStatusResponse
{
        /// <summary>
    /// Gets or sets the AeroDb Ready.
    /// </summary>
public bool AeroDbReady { get; set; }
        /// <summary>
    /// Gets or sets the Garnet Ready.
    /// </summary>
public bool GarnetReady { get; set; }
        /// <summary>
    /// Gets or sets the Requires AeroDb.
    /// </summary>
public bool RequiresAeroDb { get; set; }
        /// <summary>
    /// Gets or sets the Requires Garnet.
    /// </summary>
public bool RequiresGarnet { get; set; }
        /// <summary>
    /// Gets or sets the Is Ready.
    /// </summary>
public bool IsReady { get; set; }
}

/// <summary>
/// Represents a class for SetupInput.
/// </summary>
public class SetupInput
{
        /// <summary>
    /// Gets or sets the Database Mode.
    /// </summary>
[Required]
    public string DatabaseMode { get; set; } = "Embedded";

        /// <summary>
    /// Gets or sets the Cache Mode.
    /// </summary>
[Required]
    public string CacheMode { get; set; } = AeroAppServerConstants.LocalCacheMode;

        /// <summary>
    /// Gets or sets the Secret Provider.
    /// </summary>
[Required]
    public string SecretProvider { get; set; } = "Local Certificate";

        /// <summary>
    /// Gets or sets the Authentication Mode.
    /// </summary>
[Required]
    public string AuthenticationMode { get; set; } = "Local";

        /// <summary>
    /// Gets or sets the Connection String.
    /// </summary>
public string? ConnectionString { get; set; } = "ws://localhost:8000/rpc";

        /// <summary>
    /// Gets or sets whether the server database permits unauthenticated connections.
    /// </summary>
public bool DatabaseUnauthenticated { get; set; }

        /// <summary>
    /// Gets or sets the server database username.
    /// </summary>
public string? DatabaseUsername { get; set; }

        /// <summary>
    /// Gets or sets the server database password.
    /// </summary>
public string? DatabasePassword { get; set; }

        /// <summary>
    /// Gets or sets the Cache Connection String.
    /// </summary>
public string? CacheConnectionString { get; set; }

        /// <summary>
    /// Gets or sets the Infisical Machine Id.
    /// </summary>
public string? InfisicalMachineId { get; set; }

        /// <summary>
    /// Gets or sets the Infisical Client Secret.
    /// </summary>
public string? InfisicalClientSecret { get; set; }

        /// <summary>
    /// Gets or sets the Admin User Name.
    /// </summary>
[Required]
    [StringLength(100, MinimumLength = 3)]
    public string AdminUserName { get; set; } = "admin";

        /// <summary>
    /// Gets or sets the Admin Email.
    /// </summary>
[Required]
    [EmailAddress]
    public string AdminEmail { get; set; } = "hello@getaerocms.net";

        /// <summary>
    /// Gets or sets the Password.
    /// </summary>
[Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = "";

        /// <summary>
    /// Gets or sets the Confirm Password.
    /// </summary>
[Required]
    [Compare("Password")]
    public string ConfirmPassword { get; set; } = "";

        /// <summary>
    /// Gets or sets the Site Name.
    /// </summary>
[Required]
    [StringLength(100)]
    public string SiteName { get; set; } = "Aero CMS";

        /// <summary>
    /// Gets or sets the Homepage Title.
    /// </summary>
[Required]
    [StringLength(100)]
    public string HomepageTitle { get; set; } = "Welcome";

        /// <summary>
    /// Gets or sets the Blog Name.
    /// </summary>
[Required]
    [StringLength(100)]
    public string BlogName { get; set; } = "Blog";

        /// <summary>
    /// Gets or sets the Hostname.
    /// </summary>
[Required]
    [StringLength(256)]
    public string Hostname { get; set; } = "localhost";

        /// <summary>
    /// Gets or sets the Default Culture.
    /// </summary>
[Required]
    [StringLength(10)]
    public string DefaultCulture { get; set; } = "en-US";

        /// <summary>
    /// Gets or sets the Supported Cultures.
    /// </summary>
public List<string> SupportedCultures { get; set; } = ["en-US"];
}

/// <summary>
/// Represents a record for CultureOption.
/// </summary>
public sealed record CultureOption(string Name, string DisplayName);
