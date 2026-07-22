using System.ComponentModel.DataAnnotations;
using Aero.Cms.Abstractions.Authentication;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.ExternalMembers;

public partial class ExternalIdentityAuthorityForm : ComponentBase
{
    [Parameter] public ExternalIdentityAuthorityState? Authority { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public EventCallback<ConfigureExternalIdentityAuthorityRequest> OnSubmit { get; set; }
    protected AuthorityFormModel Form { get; } = new();
    protected bool IdentityLocked => Authority?.Configured == true;

    protected override void OnParametersSet()
    {
        if (Authority is null) return;
        Form.Provider = Authority.Provider ?? "workos";
        Form.OrganizationId = Authority.OrganizationId ?? string.Empty;
        Form.Authority = Authority.Authority ?? "https://api.workos.com";
        Form.VaultId = Authority.VaultId ?? 0;
        Form.VaultEnvironment = Authority.VaultEnvironment ?? "production";
        Form.Enabled = Authority.Enabled;
    }

    protected Task SubmitAsync()
    {
        var authority = Form.Provider == "workos" ? "https://api.workos.com" : Form.Authority;
        return OnSubmit.InvokeAsync(new(Form.Provider, Form.OrganizationId, authority,
            Form.VaultId, Form.VaultEnvironment, Form.Enabled));
    }

    protected sealed class AuthorityFormModel
    {
        [Required] public string Provider { get; set; } = "workos";
        [Required, StringLength(512)] public string OrganizationId { get; set; } = string.Empty;
        [Required, StringLength(2048)] public string Authority { get; set; } = "https://api.workos.com";
        [Range(1, long.MaxValue)] public long VaultId { get; set; }
        [Required, StringLength(128)] public string VaultEnvironment { get; set; } = "production";
        public bool Enabled { get; set; }
    }
}
