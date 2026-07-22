using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Aero.AppServer;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Setup.Bootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Setup.Areas.Setup.Pages;

/// <summary>
/// Implements the state and validation flow for the six-step initial setup wizard.
/// </summary>
/// <remarks>
/// The component collects sensitive database, secret-provider, and administrator values.
/// Submission hands those values to <see cref="ISetupBootstrapHandoffService"/>; the component
/// does not persist them directly. Infrastructure readiness is currently informational.
/// </remarks>
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
    /// Gets or sets an optional return URL supplied by the route or parent component.
    /// </summary>
[Parameter]
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets the mutable setup form model.
    /// </summary>
public SetupInput Input { get; set; } = new();

    /// <summary>
    /// Gets or sets the validation, failure, or handoff status shown by the wizard.
    /// </summary>
public string? StatusMessage { get; set; }

    /// <summary>
    /// Gets or sets whether the administrator password is displayed as plain text.
    /// </summary>
public bool ShowPassword { get; set; }
    /// <summary>
    /// Gets or sets whether the password confirmation is displayed as plain text.
    /// </summary>
public bool ShowConfirmPassword { get; set; }

    // Service readiness status
    /// <summary>
    /// Gets or sets the last observed local AeroDB readiness value.
    /// </summary>
public bool AeroDbReady { get; set; }
    /// <summary>
    /// Gets or sets the last observed local Garnet readiness value.
    /// </summary>
public bool GarnetReady { get; set; }

    // Computed properties for conditional display
    /// <summary>
    /// Gets whether server database connection inputs should be displayed.
    /// </summary>
public bool ShowConnectionString => Input.DatabaseMode == "Server";
    /// <summary>
    /// Gets whether the remote cache connection input should be displayed.
    /// </summary>
public bool ShowCacheConnectionString => Input.CacheMode == "Server";
    /// <summary>
    /// Gets whether Infisical machine-identity inputs should be displayed.
    /// </summary>
public bool ShowInfisicalFields => Input.SecretProvider == "Infisical";

    /// <summary>
    /// Gets whether the selected database mode requires the local AeroDB service.
    /// </summary>
public bool RequiresAeroDb => Input.DatabaseMode == "Embedded";
    /// <summary>
    /// Gets whether the selected cache mode requires the local Garnet service.
    /// </summary>
public bool RequiresGarnet => Input.CacheMode == AeroAppServerConstants.LocalCacheMode;

    /// <summary>
    /// Gets whether readiness blocks the current setup flow.
    /// </summary>
    /// <remarks>Currently always <see langword="true"/> because local services start after handoff.</remarks>
public bool IsReady => true;
    /// <summary>
    /// Gets or sets whether a bootstrap handoff is in progress.
    /// </summary>
public bool IsSubmitting { get; set; }

    /// <summary>
    /// Gets the localized explanation of deferred infrastructure readiness.
    /// </summary>
public string ReadinessMessage => BuildReadinessMessage();
    /// <summary>
    /// Gets or sets the one-based wizard step.
    /// </summary>
public int CurrentStep { get; set; } = 1;
    /// <summary>
    /// Gets whether the wizard is on its review step.
    /// </summary>
public bool IsLastStep => CurrentStep == TotalSteps;
    /// <summary>
    /// Gets whether the current step passes validation without mutating the displayed status.
    /// </summary>
public bool CanMoveNext => ValidateCurrentStep(false);
    /// <summary>
    /// Gets completion progress as a percentage of the six wizard steps.
    /// </summary>
public double ProgressPercent => CurrentStep * 100d / TotalSteps;
    /// <summary>
    /// Gets the localized title for the current step.
    /// </summary>
public string CurrentStepTitle => GetStepName(CurrentStep);
    /// <summary>
    /// Gets the localized description for the current step.
    /// </summary>
public string CurrentStepDescription => GetStepSummary(CurrentStep);
    /// <summary>
    /// Gets the selected database mode, falling back to embedded when blank.
    /// </summary>
public string EffectiveDatabaseMode => NormalizeMode(Input.DatabaseMode, "Embedded");
    /// <summary>
    /// Gets the selected cache mode, falling back to the local mode when blank.
    /// </summary>
public string EffectiveCacheMode => NormalizeMode(Input.CacheMode, AeroAppServerConstants.LocalCacheMode);
    /// <summary>
    /// Gets the selected secret provider, falling back to local certificate protection when blank.
    /// </summary>
public string EffectiveSecretProvider => NormalizeMode(Input.SecretProvider, "Local Certificate");
    /// <summary>
    /// Gets the resolved CMS manager authentication provider.
    /// </summary>
public string EffectiveManagerAuthenticationProvider
    => GetAuthenticationSelections().ManagerAuthenticationProvider;

    /// <summary>
    /// Gets the resolved storefront member authentication provider.
    /// </summary>
public string EffectiveMemberAuthenticationProvider
    => GetAuthenticationSelections().MemberAuthenticationProvider;

    /// <summary>
    /// Gets a display label for the resolved CMS manager provider.
    /// </summary>
public string EffectiveManagerAuthenticationLabel
    => GetManagerAuthenticationLabel(EffectiveManagerAuthenticationProvider);

    /// <summary>
    /// Gets a display label for the resolved storefront member provider.
    /// </summary>
public string EffectiveMemberAuthenticationLabel
    => GetMemberAuthenticationLabel(EffectiveMemberAuthenticationProvider);
    /// <summary>
    /// Gets the curated culture choices displayed by the setup wizard.
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
    /// Gets or sets whether the current status represents a validation or handoff failure.
    /// </summary>
public bool HasValidationErrors { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// Normalizes culture selections and, in debug builds only, populates development passwords.
    /// Existing bound input is retained.
    /// </remarks>
protected override void OnInitialized()
    {
        // Set default values
        Input ??= new SetupInput
        {
            DatabaseMode = "Embedded",
            CacheMode = AeroAppServerConstants.LocalCacheMode,
            SecretProvider = "Local Certificate",
            AuthenticationFamily = AuthenticationFamilies.Local,
            ManagerAuthenticationProvider = AuthenticationProviderSelections.Manager.Local,
            MemberAuthenticationProvider = AuthenticationProviderSelections.Member.Disabled,
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
    /// Toggles visibility of the administrator password field.
    /// </summary>
public void TogglePassword()
    {
        ShowPassword = !ShowPassword;
    }

    /// <summary>
    /// Toggles visibility of the password-confirmation field.
    /// </summary>
public void ToggleConfirmPassword()
    {
        ShowConfirmPassword = !ShowConfirmPassword;
    }

    /// <summary>
    /// Advances one step when the current step is valid.
    /// </summary>
    /// <returns>A completed task after state is updated; no asynchronous I/O is performed.</returns>
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
    /// Moves back one step and clears transient validation state.
    /// </summary>
    /// <returns>A task that completes after the component has requested a render.</returns>
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
    /// Gets the current Tailwind class list for a form field.
    /// </summary>
    /// <param name="key">Reserved for future field-specific validation styling.</param>
    /// <returns>The default input class list.</returns>
public string GetFieldClass(string key)
    {
        // For now, return default styling
        // TODO: Add validation state tracking
        return "h-12 w-full px-4 rounded-xl border border-slate-200 bg-slate-50/50 text-sm focus:bg-white focus:border-indigo-500 focus:ring-4 focus:ring-indigo-50 outline-none transition-all";
    }

    /// <summary>
    /// Determines whether a culture is selected, ignoring case.
    /// </summary>
    /// <param name="culture">The normalized or user-supplied culture name.</param>
    /// <returns><see langword="true"/> when the culture appears in the supported-culture list.</returns>
public bool IsCultureSelected(string culture)
        => Input.SupportedCultures.Any(selected => string.Equals(selected, culture, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Adds or removes a culture while preserving at least one selection and a selected default culture.
    /// </summary>
    /// <param name="culture">The culture represented by the changed checkbox.</param>
    /// <param name="args">The checkbox change event; only a Boolean <see langword="true"/> is treated as checked.</param>
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
    /// Normalizes a changed default culture and ensures it is selected as supported.
    /// </summary>
    /// <param name="args">The selection change containing a culture name.</param>
public void OnDefaultCultureChanged(ChangeEventArgs args)
    {
        Input.DefaultCulture = NormalizeCultureName(args.Value?.ToString());
        EnsureSupportedCulturesContainDefault();
    }

    /// <summary>
    /// Validates the final selections and initiates the persistent setup-to-runtime handoff.
    /// </summary>
    /// <returns>A task that completes when the handoff succeeds or returns a failure.</returns>
    /// <remarks>
    /// On success the setup host is expected to stop, so submitting state remains set; on failure
    /// the UI is restored and errors are shown.
    /// </remarks>
protected async Task HandleSubmit()
    {
        HasValidationErrors = false;

        if (!ValidateCurrentStep(true))
        {
            return;
        }

        EnsureSupportedCulturesContainDefault();

        var secretProvider = NormalizeMode(Input.SecretProvider, "Local Certificate");
        var databaseMode = NormalizeMode(Input.DatabaseMode, "Embedded");
        var cacheMode = NormalizeMode(Input.CacheMode, AeroAppServerConstants.LocalCacheMode);
        var authenticationSelections = GetAuthenticationSelections();

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
            authenticationSelections.ManagerAuthenticationProvider,
            authenticationSelections.MemberAuthenticationProvider,
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

    /// <summary>
    /// Applies a fallback only when a mode value is blank.
    /// </summary>
    private static string NormalizeMode(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    /// <summary>
    /// Accepts only the local and server cache modes exposed by the AppServer contract.
    /// </summary>
    private static bool IsSupportedCacheMode(string cacheMode)
        => cacheMode.Equals(AeroAppServerConstants.LocalCacheMode, StringComparison.OrdinalIgnoreCase)
           || cacheMode.Equals(AeroAppServerConstants.ServerCacheMode, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves progressive-disclosure controls to the two canonical persisted selections.
    /// </summary>
    private AuthenticationSelections GetAuthenticationSelections()
    {
        if (Input.UseAdvancedAuthenticationOptions)
        {
            return new AuthenticationSelections(
                Input.ManagerAuthenticationProvider,
                Input.MemberAuthenticationProvider);
        }

        return Input.AuthenticationFamily switch
        {
            AuthenticationFamilies.Local => new AuthenticationSelections(
                AuthenticationProviderSelections.Manager.Local,
                Input.EnableStorefrontMembers
                    ? AuthenticationProviderSelections.Member.Local
                    : AuthenticationProviderSelections.Member.Disabled),
            AuthenticationFamilies.MicrosoftEntra => new AuthenticationSelections(
                AuthenticationProviderSelections.Manager.EntraWorkforce,
                Input.EnableStorefrontMembers
                    ? AuthenticationProviderSelections.Member.EntraExternalId
                    : AuthenticationProviderSelections.Member.Disabled),
            AuthenticationFamilies.WorkOs => new AuthenticationSelections(
                AuthenticationProviderSelections.Manager.WorkOs,
                Input.EnableStorefrontMembers
                    ? AuthenticationProviderSelections.Member.WorkOs
                    : AuthenticationProviderSelections.Member.Disabled),
            _ => new AuthenticationSelections(string.Empty, string.Empty)
        };
    }

    /// <summary>
    /// Returns a user-facing error when a selection is unknown or not yet operational.
    /// </summary>
    private string? GetAuthenticationSelectionError()
    {
        var selections = GetAuthenticationSelections();

        if (!AuthenticationProviderSelections.Manager.IsCanonical(selections.ManagerAuthenticationProvider))
        {
            return "Select a valid CMS manager authentication provider.";
        }

        if (!AuthenticationProviderSelections.Manager.IsAvailable(selections.ManagerAuthenticationProvider))
        {
            return "The selected CMS manager authentication provider is not available.";
        }

        if (!AuthenticationProviderSelections.Member.IsCanonical(selections.MemberAuthenticationProvider))
        {
            return "Select a valid storefront member authentication provider.";
        }

        if (!AuthenticationProviderSelections.Member.IsAvailable(selections.MemberAuthenticationProvider))
        {
            return "The selected storefront member authentication provider is not available.";
        }

        return null;
    }

    private static string GetManagerAuthenticationLabel(string provider) => provider switch
    {
        AuthenticationProviderSelections.Manager.Local => "Local Identity",
        AuthenticationProviderSelections.Manager.EntraWorkforce => "Microsoft Entra Workforce",
        AuthenticationProviderSelections.Manager.WorkOs => "WorkOS",
        _ => "Invalid selection"
    };

    private static string GetMemberAuthenticationLabel(string provider) => provider switch
    {
        AuthenticationProviderSelections.Member.Disabled => "Disabled",
        AuthenticationProviderSelections.Member.Local => "Local Identity",
        AuthenticationProviderSelections.Member.EntraExternalId => "Microsoft Entra External ID",
        AuthenticationProviderSelections.Member.WorkOs => "WorkOS",
        _ => "Invalid selection"
    };

    /// <summary>
    /// Validates only the fields owned by the current wizard step.
    /// </summary>
    /// <param name="showMessage">Whether to update public validation state and the status message.</param>
    /// <returns><see langword="true"/> when no step-specific validation error is found.</returns>
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
            5 when GetAuthenticationSelectionError() is { } authenticationError => authenticationError,
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
    /// Gets the localized title for a one-based wizard step.
    /// </summary>
    /// <param name="step">The step number.</param>
    /// <returns>The localized title, or the generic setup title for an unknown step.</returns>
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
    /// Gets the localized summary for a one-based wizard step.
    /// </summary>
    /// <param name="step">The step number.</param>
    /// <returns>The localized summary, or an empty string for an unknown step.</returns>
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

    /// <summary>
    /// Builds the localized message explaining that infrastructure validation occurs after handoff.
    /// </summary>
    private string BuildReadinessMessage()
    {
        return L["Readiness shown here is informational only. Local services will be started and validated after handoff to the main app."];
    }

    /// <summary>
    /// Canonicalizes and deduplicates cultures, then inserts the default culture when missing.
    /// </summary>
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

    /// <summary>
    /// Converts a culture name to its canonical form, falling back to <c>en-US</c> when blank or invalid.
    /// </summary>
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
/// Represents local infrastructure readiness returned to a setup client.
/// </summary>
public sealed class SetupStatusResponse
{
    /// <summary>
    /// Gets or sets whether local AeroDB has reported ready.
    /// </summary>
public bool AeroDbReady { get; set; }
    /// <summary>
    /// Gets or sets whether local Garnet has reported ready.
    /// </summary>
public bool GarnetReady { get; set; }
    /// <summary>
    /// Gets or sets whether the selected setup requires local AeroDB.
    /// </summary>
public bool RequiresAeroDb { get; set; }
    /// <summary>
    /// Gets or sets whether the selected setup requires local Garnet.
    /// </summary>
public bool RequiresGarnet { get; set; }
    /// <summary>
    /// Gets or sets the aggregate readiness decision.
    /// </summary>
public bool IsReady { get; set; }
}

/// <summary>
/// Captures setup wizard selections before they are protected and persisted.
/// </summary>
/// <remarks>
/// Passwords, connection strings, database credentials, and Infisical credentials are
/// sensitive. This UI model must not be logged or persisted directly.
/// </remarks>
public class SetupInput
{
    /// <summary>
    /// Gets or sets the embedded or server database mode.
    /// </summary>
[Required]
    public string DatabaseMode { get; set; } = "Embedded";

    /// <summary>
    /// Gets or sets the local or server cache mode.
    /// </summary>
[Required]
    public string CacheMode { get; set; } = AeroAppServerConstants.LocalCacheMode;

    /// <summary>
    /// Gets or sets the provider used to protect bootstrap secrets.
    /// </summary>
[Required]
    public string SecretProvider { get; set; } = "Local Certificate";

    /// <summary>
    /// Gets or sets the provider family used by the simple authentication view.
    /// </summary>
[Required]
    public string AuthenticationFamily { get; set; } = AuthenticationFamilies.Local;

    /// <summary>
    /// Gets or sets whether the wizard displays independent manager and member selections.
    /// This presentation choice is not persisted.
    /// </summary>
public bool UseAdvancedAuthenticationOptions { get; set; }

    /// <summary>
    /// Gets or sets whether the simple view enables storefront member authentication.
    /// This presentation choice is resolved to a canonical member provider before persistence.
    /// </summary>
public bool EnableStorefrontMembers { get; set; }

    /// <summary>
    /// Gets or sets the manager provider selected in the advanced view.
    /// </summary>
[Required]
public string ManagerAuthenticationProvider { get; set; } = AuthenticationProviderSelections.Manager.Local;

    /// <summary>
    /// Gets or sets the storefront member provider selected in the advanced view.
    /// </summary>
[Required]
public string MemberAuthenticationProvider { get; set; } = AuthenticationProviderSelections.Member.Disabled;

    /// <summary>
    /// Gets or sets the remote database endpoint used in server mode.
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
    /// Gets or sets the remote cache connection string used in server mode.
    /// </summary>
public string? CacheConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the Infisical machine identity used to bootstrap external secret access.
    /// </summary>
public string? InfisicalMachineId { get; set; }

    /// <summary>
    /// Gets or sets the Infisical client secret used to bootstrap external secret access.
    /// </summary>
public string? InfisicalClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the initial administrator user name.
    /// </summary>
[Required]
    [StringLength(100, MinimumLength = 3)]
    public string AdminUserName { get; set; } = "admin";

    /// <summary>
    /// Gets or sets the initial administrator email address.
    /// </summary>
[Required]
    [EmailAddress]
    public string AdminEmail { get; set; } = "hello@getaerocms.net";

    /// <summary>
    /// Gets or sets the initial administrator password.
    /// </summary>
[Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = "";

    /// <summary>
    /// Gets or sets the password confirmation used only for UI validation.
    /// </summary>
[Required]
    [Compare("Password")]
    public string ConfirmPassword { get; set; } = "";

    /// <summary>
    /// Gets or sets the initial site name.
    /// </summary>
[Required]
    [StringLength(100)]
    public string SiteName { get; set; } = "Aero CMS";

    /// <summary>
    /// Gets or sets the initial home-page title.
    /// </summary>
[Required]
    [StringLength(100)]
    public string HomepageTitle { get; set; } = "Welcome";

    /// <summary>
    /// Gets or sets the initial blog name.
    /// </summary>
[Required]
    [StringLength(100)]
    public string BlogName { get; set; } = "Blog";

    /// <summary>
    /// Gets or sets the host name associated with the initial site.
    /// </summary>
[Required]
    [StringLength(256)]
    public string Hostname { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the canonical default culture.
    /// </summary>
[Required]
    [StringLength(10)]
    public string DefaultCulture { get; set; } = "en-US";

    /// <summary>
    /// Gets or sets the cultures enabled for the initial site.
    /// </summary>
public List<string> SupportedCultures { get; set; } = ["en-US"];
}

/// <summary>
/// Describes a selectable culture by canonical name and display label.
/// </summary>
public sealed record CultureOption(string Name, string DisplayName);

/// <summary>
/// Defines provider-family values used only by the setup wizard's simple view.
/// </summary>
public static class AuthenticationFamilies
{
    /// <summary>Local AeroCMS authentication.</summary>
    public const string Local = "local";
    /// <summary>Microsoft Entra authentication.</summary>
    public const string MicrosoftEntra = "microsoft_entra";
    /// <summary>WorkOS authentication.</summary>
    public const string WorkOs = "workos";
}

internal sealed record AuthenticationSelections(
    string ManagerAuthenticationProvider,
    string MemberAuthenticationProvider);
