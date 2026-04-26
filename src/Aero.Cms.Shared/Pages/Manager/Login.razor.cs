using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager;

public abstract class LoginBase : ComponentBase
{
    [Inject]
    private IAuthClient AuthClient { get; set; } = default!;

    [Inject]
    private InMemoryTokenProvider TokenProvider { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    protected IConfiguration Configuration { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "returnUrl")]
    protected string? ReturnUrl { get; set; }

    protected readonly LoginModel Model = new();
    protected string? ErrorMessage;
    protected bool IsSubmitting { get; set; }
    protected bool ShowPassword { get; set; }

    protected override void OnInitialized()
    {
        var env = Configuration["ASPNETCORE_ENVIRONMENT"] ?? Configuration["Environment"];
        var isDev = env == "Development" || Navigation.BaseUri.Contains("localhost") || Navigation.BaseUri.Contains("127.0.0.1");

        if (isDev)
        {
            Model.EmailOrUserName = "admin";
            Model.Password = "*strongPassword1";
        }
    }

    protected void TogglePasswordVisibility() => ShowPassword = !ShowPassword;

    protected async Task HandleSubmit()
    {
        ErrorMessage = null;
        IsSubmitting = true;

        try
        {
            var result = await AuthClient.LoginAsync(
                new LoginRequest(Model.EmailOrUserName, Model.Password));

            if (result is Result<JwtTokenResponse, AeroError>.Ok(var response))
            {
                TokenProvider.SetToken(response.AccessToken);
                // Note: Refresh token handled in memory only for now as per spec

                Navigation.NavigateTo(string.IsNullOrWhiteSpace(ReturnUrl) ? "/manager" : ReturnUrl!, forceLoad: true);
                return;
            }

            ErrorMessage = "Login failed: Invalid credentials or insufficient permissions.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    protected sealed class LoginModel
    {
        [Required]
        public string EmailOrUserName { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
