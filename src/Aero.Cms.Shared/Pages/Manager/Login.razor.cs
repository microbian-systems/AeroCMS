using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
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

    [Inject]
    private IHttpClientFactory HttpClientFactory { get; set; } = default!;

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
            // Step 1: Login via JWT/API key exchange (provides API access tokens)
            var result = await AuthClient.LoginAsync(
                new LoginRequest(Model.EmailOrUserName, Model.Password));

            if (result is Result<JwtTokenResponse, AeroError>.Ok(var response))
            {
                TokenProvider.SetToken(response.AccessToken);

                // Step 2: Login via ASP.NET Core Identity cookie endpoint.
                // This sets the .AeroCms.Auth cookie so that UseAuthentication()
                // middleware recognizes the user on subsequent HTTP requests and
                // the AuthenticationStateProvider reports IsAuthenticated=true.
                // Per MS Learn, cookie auth is the primary mechanism for Blazor Web Apps.
                var identityResult = await LoginViaCookieAsync();
                if (!identityResult)
                {
                    return; // ErrorMessage already set by LoginViaCookieAsync
                }

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

    /// <summary>
    /// Calls the ASP.NET Core Identity cookie login endpoint to establish the auth session.
    /// This is required in addition to JWT auth so that the Blazor Web App's
    /// AuthenticationStateProvider correctly reports the user as authenticated.
    /// </summary>
    private async Task<bool> LoginViaCookieAsync()
    {
        try
        {
            var request = new LoginRequest(Model.EmailOrUserName, Model.Password, Model.RememberMe);
            var cookieResponse = await AuthClient.LoginWithCookieAsync(request);
                
            if (cookieResponse.IsSuccessStatusCode)
            {
                return true;
            }

            var errorBody = await cookieResponse.Content.ReadAsStringAsync();
            ErrorMessage = $"Login failed: {errorBody}";
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
            return false;
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
