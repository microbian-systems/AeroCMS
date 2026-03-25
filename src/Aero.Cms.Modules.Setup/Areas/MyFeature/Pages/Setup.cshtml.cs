using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Setup.Areas.MyFeature.Pages;

public sealed class SetupModel(ISetupCompletionService setupCompletionService) : PageModel
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

        var completionResult = await setupCompletionService.CompleteAsync(
            new SetupCompletionRequest(
                Input.AdminUserName,
                Input.AdminEmail,
                Input.Password,
                Input.SiteName,
                Input.HomepageTitle,
                Input.BlogName),
            cancellationToken);

        if (!completionResult.Succeeded)
        {
            foreach (var error in completionResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return Page();
        }

        StatusMessage = completionResult.AlreadyComplete
            ? "Aero CMS is already configured. Redirecting to the requested destination."
            : completionResult.CreatedAdmin
                ? "Aero CMS is ready with administrator access and starter content."
                : "Aero CMS starter content is confirmed and administrator access was already provisioned.";

        if (!completionResult.AlreadyComplete && completionResult.CreatedAdmin)
        {
            return LocalRedirect("/manager/settings");
        }

        return LocalRedirect(GetSafePostTarget(ReturnUrl));
    }

    internal static string? NormalizeReturnUrl(string? returnUrl)
        => RedirectHttpResult.IsLocalUrl(returnUrl) ? returnUrl : null;

    internal static string GetSafePostTarget(string? returnUrl)
        => NormalizeReturnUrl(returnUrl) ?? SetupPathAllowlist.SetupPath;

    public sealed class SetupInputModel
    {
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
            SiteName = "Aero CMS",
            HomepageTitle = "Welcome to Aero CMS",
            BlogName = "Field Notes"
        };
    }
}
