using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Aero.Cms.Shared.Pages.Manager.ExternalMembers;

public partial class ExternalMembers : ComponentBase, IAsyncDisposable
{
    [Inject] protected IExternalIdentityAdminClient Client { get; set; } = default!;
    [Inject] protected AntiforgeryStateProvider Antiforgery { get; set; } = default!;

    protected ExternalIdentityAuthorityState? Authority { get; private set; }
    protected string? InvitationHandle { get; private set; }
    protected string? PasswordResetHandle { get; private set; }
    protected DateTimeOffset? PasswordResetExpiresAt { get; private set; }
    protected string? Error { get; private set; }
    protected bool IsLoading { get; private set; } = true;
    protected bool IsSavingAuthority { get; private set; }
    protected bool IsCreatingInvitation { get; private set; }
    protected bool IsIssuingPasswordReset { get; private set; }
    protected bool IsLocalAuthority => Authority is
        { Configured: true, Provider: LocalExternalMemberAuthentication.Provider };
    private CancellationTokenSource _lifetime = new();

    protected override Task OnInitializedAsync() => LoadAsync();

    protected async Task LoadAsync()
    {
        InvitationHandle = null;
        ClearPasswordResetHandle();
        Error = null;
        IsLoading = true;
        try
        {
            var result = await Client.GetAuthorityAsync(_lifetime.Token);
            if (result is Result<ExternalIdentityAuthorityState, AeroError>.Ok(var authority))
                Authority = authority;
            else
                Error = "External identity configuration could not be loaded.";
        }
        finally { IsLoading = false; }
    }

    protected async Task SaveAuthorityAsync(ConfigureExternalIdentityAuthorityRequest request)
    {
        ClearPasswordResetHandle();
        Error = null;
        IsSavingAuthority = true;
        try
        {
            var token = Antiforgery.GetAntiforgeryToken()?.Value;
            if (string.IsNullOrWhiteSpace(token))
            {
                Error = "The security token is unavailable. Reload the page and try again.";
                return;
            }
            var result = await Client.ConfigureAuthorityAsync(request, token, _lifetime.Token);
            if (result is Result<ExternalIdentityAuthorityState, AeroError>.Ok(var authority))
                Authority = authority;
            else
                Error = "External identity configuration could not be saved.";
        }
        finally { IsSavingAuthority = false; }
    }

    protected async Task CreateInvitationAsync(CreateExternalIdentityInvitationRequest request)
    {
        ClearPasswordResetHandle();
        InvitationHandle = null;
        Error = null;
        IsCreatingInvitation = true;
        try
        {
            var token = Antiforgery.GetAntiforgeryToken()?.Value;
            if (string.IsNullOrWhiteSpace(token))
            {
                Error = "The security token is unavailable. Reload the page and try again.";
                return;
            }
            var result = await Client.CreateInvitationAsync(request, token, _lifetime.Token);
            if (result is Result<ExternalIdentityInvitationResponse, AeroError>.Ok(var invitation))
                InvitationHandle = invitation.Handle;
            else
                Error = "The invitation could not be created.";
        }
        finally { IsCreatingInvitation = false; }
    }

    protected async Task IssuePasswordResetAsync(long externalMemberId)
    {
        ClearPasswordResetHandle();
        Error = null;
        IsIssuingPasswordReset = true;
        try
        {
            var token = Antiforgery.GetAntiforgeryToken()?.Value;
            if (string.IsNullOrWhiteSpace(token))
            {
                Error = "The security token is unavailable. Reload the page and try again.";
                return;
            }

            var request = new IssueLocalExternalMemberPasswordResetAdminRequest(DateTimeOffset.UtcNow.AddHours(1));
            var result = await Client.IssueLocalPasswordResetAsync(
                externalMemberId, request, token, _lifetime.Token);
            if (result is Result<LocalExternalMemberPasswordResetResponse, AeroError>.Ok(var reset))
            {
                PasswordResetHandle = reset.Handle;
                PasswordResetExpiresAt = reset.ExpiresAt;
            }
            else
            {
                Error = "The local password-reset handle could not be issued.";
            }
        }
        finally { IsIssuingPasswordReset = false; }
    }

    private void ClearPasswordResetHandle()
    {
        PasswordResetHandle = null;
        PasswordResetExpiresAt = null;
    }

    public async ValueTask DisposeAsync()
    {
        InvitationHandle = null;
        ClearPasswordResetHandle();
        await _lifetime.CancelAsync();
        _lifetime.Dispose();
    }
}
