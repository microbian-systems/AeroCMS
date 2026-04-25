using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Aero.Cms.Core.Http.Clients;
using Aero.Core.Http;
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

    [SupplyParameterFromQuery(Name = "returnUrl")]
    protected string? ReturnUrl { get; set; }

    protected readonly LoginModel Model = new();
    protected string? ErrorMessage;
    protected bool IsSubmitting { get; set; }

    protected async Task HandleSubmit()
    {
        ErrorMessage = null;
        IsSubmitting = true;

        try
        {
            var response = await AuthClient.LoginAsync(
                new LoginRequest(Model.EmailOrUserName, Model.Password));

            if (!string.IsNullOrEmpty(response.AccessToken))
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
