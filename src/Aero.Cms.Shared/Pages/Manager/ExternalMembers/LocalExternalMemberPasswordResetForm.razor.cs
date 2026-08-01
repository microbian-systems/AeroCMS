using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.ExternalMembers;

public partial class LocalExternalMemberPasswordResetForm : ComponentBase
{
    [Parameter] public bool Enabled { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public EventCallback<long> OnSubmit { get; set; }
    protected PasswordResetFormModel Form { get; } = new();

    protected Task SubmitAsync() => OnSubmit.InvokeAsync(Form.ExternalMemberId);

    protected sealed class PasswordResetFormModel
    {
        [Range(1, long.MaxValue)] public long ExternalMemberId { get; set; }
    }
}
