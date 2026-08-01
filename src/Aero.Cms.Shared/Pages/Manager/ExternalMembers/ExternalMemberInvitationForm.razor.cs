using System.ComponentModel.DataAnnotations;
using Aero.Cms.Abstractions.Authentication;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.ExternalMembers;

public partial class ExternalMemberInvitationForm : ComponentBase
{
    [Parameter] public bool Enabled { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public EventCallback<CreateExternalIdentityInvitationRequest> OnSubmit { get; set; }
    protected InvitationFormModel Form { get; } = new();

    protected Task SubmitAsync() => OnSubmit.InvokeAsync(new(Form.Email, Form.ExpiresAt));

    protected sealed class InvitationFormModel
    {
        [Required, EmailAddress, StringLength(320)] public string Email { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(1);
    }
}
