using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Aero.Cms.Modules.Setup.Configuration;
using Aero.Cms.Modules.Setup.Bootstrap;

namespace Aero.Cms.Modules.Setup.Areas.MyFeature.Pages;

public sealed class SetupModel(
    ISetupInitializationService setupInitializationService,
    IDatabaseBootstrapService databaseBootstrapService,
    ICacheBootstrapService cacheBootstrapService,
    IBootstrapPendingSetupRequestStore pendingSetupRequestStore) : PageModel
{
    [BindProperty]
    public SetupInputModel Input { get; set; } = SetupInputModel.CreateDefault();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public void OnGet()
    {
        ReturnUrl = NormalizeReturnUrl(ReturnUrl);
        Input = SetupInputModel.CreateDefault();

        if (System.Diagnostics.Debugger.IsAttached)
        {
            Input.AdminUserName = "Admin";
            Input.Password = "*strongPassword1";
            Input.ConfirmPassword = "*strongPassword1";
        }
    }

public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ReturnUrl = NormalizeReturnUrl(ReturnUrl);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var secretProvider = NormalizeMode(Input.SecretProvider, "Local Certificate");
        var databaseMode = NormalizeMode(Input.DatabaseMode, "Embedded");
        var cacheMode = NormalizeMode(Input.CacheMode, "Memory");

        // Validate server mode requires connection strings
        if (databaseMode.Equals("Server", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Input.ConnectionString))
        {
            ModelState.AddModelError(nameof(Input.ConnectionString), "A server connection string is required for Server mode.");
            return Page();
        }

        if (cacheMode.Equals("Server", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Input.CacheConnectionString))
        {
            ModelState.AddModelError(nameof(Input.CacheConnectionString), "A cache connection string is required for Server cache mode.");
            return Page();
        }

        await databaseBootstrapService.PersistAsync(new Bootstrap.DatabaseBootstrapModel(
            databaseMode,
            Input.ConnectionString,
            secretProvider,
            Input.InfisicalMachineId,
            Input.InfisicalClientSecret), cancellationToken);
        await cacheBootstrapService.PersistAsync(new Bootstrap.CacheBootstrapModel(
            cacheMode,
            Input.CacheConnectionString,
            secretProvider,
            Input.InfisicalMachineId,
            Input.InfisicalClientSecret), cancellationToken);

        await pendingSetupRequestStore.SaveAsync(
            new SeedDatabaseRequest(
                Input.AdminUserName,
                Input.AdminEmail,
                Input.Password,
                Input.SiteName,
                Input.HomepageTitle,
                Input.BlogName),
            cancellationToken);

        StatusMessage = "Configuration saved. Restart the application to complete initialization.";
        return Page();
    }

    internal static string? NormalizeReturnUrl(string? returnUrl)
        => RedirectHttpResult.IsLocalUrl(returnUrl) ? returnUrl : null;

    internal static string GetSafePostTarget(string? returnUrl)
        => NormalizeReturnUrl(returnUrl) ?? SetupPathAllowlist.SetupPath;

    private static string NormalizeMode(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    public sealed class SetupInputModel
    {
        public string DatabaseMode { get; set; } = "Embedded";

        public string CacheMode { get; set; } = "Memory";

        public string SecretProvider { get; set; } = "Local Certificate";

        public string? ConnectionString { get; set; }

        public string? CacheConnectionString { get; set; }

        public string? InfisicalMachineId { get; set; }

        public string? InfisicalClientSecret { get; set; }

        [Required]
        [Display(Name = "Admin username")]
        [StringLength(32, MinimumLength = 3)]
        [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Admin username can use letters, numbers, dots, dashes, and underscores only.")]
        public string AdminUserName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Admin email")]
        [EmailAddress]
        [StringLength(256)]
        public string AdminEmail { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Password")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 12)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Confirm password")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The password confirmation must match the password.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Site name")]
        [StringLength(80, MinimumLength = 2)]
        public string SiteName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Homepage title")]
        [StringLength(120, MinimumLength = 4)]
        public string HomepageTitle { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Blog name")]
        [StringLength(80, MinimumLength = 2)]
        public string BlogName { get; set; } = string.Empty;

        public static SetupInputModel CreateDefault() => new()
        {
            DatabaseMode = "Embedded",
            CacheMode = "Memory",
            SecretProvider = "Local Certificate",
            SiteName = "Aero CMS",
            HomepageTitle = "Welcome to Aero CMS",
            BlogName = "Field Notes"
        };
    }
}
