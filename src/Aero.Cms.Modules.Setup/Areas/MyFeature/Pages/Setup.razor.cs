using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Aero.Cms.Modules.Setup.Areas.MyFeature.Pages;

public partial class Setup : ComponentBase
{
    [Inject]
    private ILogger<Setup> Logger { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter]
    public string? ReturnUrl { get; set; }

    [SupplyParameterFromForm]
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

    // IsReady should be true by default since user hasn't selected Embedded yet
    public bool IsReady => true;

    public bool HasValidationErrors { get; set; }

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
            BlogName = "Aero Blog"
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
            // Start polling for service status
            _ = PollSetupStatus();
        }
    }

    private async Task PollSetupStatus()
    {
        // TODO: Implement status polling via HTTP client
        // For now, assume services are ready
        PostgresReady = true;
        GarnetReady = true;
        await InvokeAsync(StateHasChanged);
    }

    public void TogglePassword()
    {
        ShowPassword = !ShowPassword;
    }

    public void ToggleConfirmPassword()
    {
        ShowConfirmPassword = !ShowConfirmPassword;
    }

    public string GetFieldClass(string key)
    {
        // For now, return default styling
        // TODO: Add validation state tracking
        return "h-12 w-full px-4 rounded-xl border border-slate-200 bg-slate-50/50 text-sm focus:bg-white focus:border-indigo-500 focus:ring-4 focus:ring-indigo-50 outline-none transition-all";
    }

    /// <summary>
    /// Test method to verify Blazor interactivity is working
    /// </summary>
    public async Task TestButtonClick()
    {
        // Log to server
        Logger.LogInformation("Test button clicked! Blazor interactivity is working.");
        Logger.LogInformation("DatabaseMode: {DatabaseMode}, CacheMode: {CacheMode}, SecretProvider: {SecretProvider}", 
            Input.DatabaseMode, Input.CacheMode, Input.SecretProvider);
        Logger.LogInformation("ShowConnectionString: {ShowConnectionString}, ShowCacheConnectionString: {ShowCacheConnectionString}, ShowInfisicalFields: {ShowInfisicalFields}",
            ShowConnectionString, ShowCacheConnectionString, ShowInfisicalFields);

        // Log to browser console
        await JSRuntime.InvokeVoidAsync("console.log", "Test button clicked! Blazor interactivity is working.");
        await JSRuntime.InvokeVoidAsync("console.log", $"DatabaseMode: {Input.DatabaseMode}");
        await JSRuntime.InvokeVoidAsync("console.log", $"CacheMode: {Input.CacheMode}");
        await JSRuntime.InvokeVoidAsync("console.log", $"SecretProvider: {Input.SecretProvider}");
        await JSRuntime.InvokeVoidAsync("console.log", $"ShowConnectionString: {ShowConnectionString}");
        await JSRuntime.InvokeVoidAsync("console.log", $"ShowCacheConnectionString: {ShowCacheConnectionString}");
        await JSRuntime.InvokeVoidAsync("console.log", $"ShowInfisicalFields: {ShowInfisicalFields}");
    }

    protected async Task HandleSubmit()
    {
        HasValidationErrors = false;

        // TODO: Implement actual form submission
        // This would call the setup service to initialize the system

        await Task.CompletedTask;
    }
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
    public string BlogName { get; set; } = "Journal";
}