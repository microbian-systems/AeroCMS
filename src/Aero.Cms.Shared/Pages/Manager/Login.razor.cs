using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager;

public abstract class LoginBase : ComponentBase
{
    [Inject]
    private HttpClient Http { get; set; } = default!;

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
            var response = await Http.PostAsJsonAsync("api/v1/admin/auth/login",
                new LocalLoginRequest(Model.EmailOrUserName, Model.Password, Model.RememberMe));

            if (response.IsSuccessStatusCode)
            {
                Navigation.NavigateTo(string.IsNullOrWhiteSpace(ReturnUrl) ? "/manager" : ReturnUrl!, forceLoad: true);
                return;
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                ErrorMessage = "This account doesn't have an Aero CMS role and can't access the manager.";
                return;
            }

            var payload = await response.Content.ReadFromJsonAsync<LocalLoginResponse>();
            ErrorMessage = payload?.Message ?? "Login failed.";
        }
        catch
        {
            ErrorMessage = "Login failed due to an unexpected error.";
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

    private sealed record LocalLoginRequest(string EmailOrUserName, string Password, bool RememberMe);

    private sealed record LocalLoginResponse(bool Succeeded, string Message);
}
