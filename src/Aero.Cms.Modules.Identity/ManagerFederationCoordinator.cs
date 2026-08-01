using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Identity;

public interface IManagerFederationCoordinator
{
    Task<Result<ManagerFederationBeginResult, AeroError>> BeginRecoveryAdministratorLinkAsync(
        BeginManagerFederationLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ManagerFederationCallbackResult, AeroError>> CompleteRecoveryAdministratorLinkAsync(
        CompleteManagerFederationCallbackRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ManagerFederationBeginResult, AeroError>> BeginSignInAsync(
        BeginManagerFederatedSignInRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ManagerFederationCallbackResult, AeroError>> CompleteSignInAsync(
        CompleteManagerFederationCallbackRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ManagerFederationCallbackResult, AeroError>> CompleteCallbackAsync(
        string expectedProvider,
        CompleteManagerFederationCallbackRequest request,
        CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(long sessionId, long userId, CancellationToken cancellationToken = default);
}

internal sealed class ManagerFederationCoordinator(
    IDocumentSession session,
    IRecoveryAdministratorAuthority recoveryAdministratorAuthority,
    IManagerFederationStateService states,
    IManagerFederationLinkService links,
    IManagerIdentityProviderStrategyFactory strategies,
    IManagerProviderSecretSource secrets,
    IManagerAuthenticationModeResolver modeResolver) : IManagerFederationCoordinator
{
    public async Task<Result<ManagerFederationBeginResult, AeroError>> BeginRecoveryAdministratorLinkAsync(
        BeginManagerFederationLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        var modeResult = await modeResolver.ResolveAsync(cancellationToken);
        if (modeResult is not Result<ManagerAuthenticationModeResolution, AeroError>.Ok(var mode) ||
            mode.Status != ManagerAuthenticationModeStatuses.Pending)
            return Failed<ManagerFederationBeginResult>();

        if (request.RecoveryAdministratorUserId <= 0 ||
            !ManagerFederationValidation.IsExactCallback(request.CallbackUri) ||
            !ManagerFederationValidation.IsSafeReturnPath(request.ReturnPath) ||
            await recoveryAdministratorAuthority.GetUserIdAsync(cancellationToken) != request.RecoveryAdministratorUserId)
            return Failed<ManagerFederationBeginResult>();

        var binding = await session.Query<ManagerIdentityAuthorityBinding>()
            .FirstOrDefaultAsync(value =>
                value.SingletonKey == ManagerIdentityAuthorityBinding.InstallationSingletonKey,
                cancellationToken);
        if (binding is null || binding.IsActive || binding.IsVerified ||
            !string.Equals(binding.Provider, mode.RequestedProvider, StringComparison.Ordinal) ||
            !ManagerIdentityAuthorityProjector.TryProject(binding, requireActive: false, out var authority) ||
            strategies.Resolve(binding.Provider) is not Result<IManagerIdentityProviderStrategy, AeroError>.Ok(var strategy))
            return Failed<ManagerFederationBeginResult>();

        var credentialsResult = await secrets.ReadAsync(authority.SecretReference, cancellationToken);
        if (credentialsResult is not Result<ManagerProviderCredentialBundle, AeroError>.Ok(var credentials))
            return Failed<ManagerFederationBeginResult>();

        using (credentials)
        {
            var context = new ManagerProviderBeginContext(authority, request.CallbackUri,
                request.ReturnPath, ManagerAuthenticationState.LinkRecoveryAdministratorPurpose);
            var preparation = await strategy.PrepareAuthorizationAsync(context, credentials, cancellationToken);
            if (preparation is not Result<ManagerProviderAuthorizationPreparation, AeroError>.Ok(var prepared))
                return Failed<ManagerFederationBeginResult>();

            var state = await states.CreateLinkStateAsync(binding, request.RecoveryAdministratorUserId,
                request.CallbackUri, request.ReturnPath, prepared.ProtectedProviderCorrelation, cancellationToken);
            if (state is not Result<string, AeroError>.Ok(var handle))
                return Failed<ManagerFederationBeginResult>();

            var challenge = await strategy.CreateAuthorizationAsync(context, prepared, handle,
                credentials, cancellationToken);
            return challenge is Result<ManagerProviderAuthorizationChallenge, AeroError>.Ok(var value)
                ? Prelude.Ok<ManagerFederationBeginResult, AeroError>(new(handle, value))
                : Failed<ManagerFederationBeginResult>();
        }
    }

    public async Task<Result<ManagerFederationCallbackResult, AeroError>> CompleteRecoveryAdministratorLinkAsync(
        CompleteManagerFederationCallbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var modeResult = await modeResolver.ResolveAsync(cancellationToken);
        if (modeResult is not Result<ManagerAuthenticationModeResolution, AeroError>.Ok(var mode) ||
            mode.Status != ManagerAuthenticationModeStatuses.Pending)
            return Failed<ManagerFederationCallbackResult>();

        // State, provider and exact callback are validated before provider secrets or Identity are touched.
        var preparedResult = await states.PrepareLinkCallbackAsync(request.StateHandle,
            request.CallbackUri, cancellationToken);
        if (preparedResult is not Result<PreparedManagerFederationCallback, AeroError>.Ok(var prepared) ||
            !string.Equals(prepared.Binding.Provider, mode.RequestedProvider, StringComparison.Ordinal) ||
            !ManagerIdentityAuthorityProjector.TryProject(prepared.Binding, requireActive: false, out var authority) ||
            strategies.Resolve(prepared.Binding.Provider) is not Result<IManagerIdentityProviderStrategy, AeroError>.Ok(var strategy))
            return Failed<ManagerFederationCallbackResult>();

        var credentialsResult = await secrets.ReadAsync(authority.SecretReference, cancellationToken);
        if (credentialsResult is not Result<ManagerProviderCredentialBundle, AeroError>.Ok(var credentials))
            return Failed<ManagerFederationCallbackResult>();

        using (credentials)
        {
            var context = new ManagerProviderCallbackContext(authority, request.CallbackUri,
                request.StateHandle, prepared.State.ProtectedProviderCorrelation, request.Code, request.Error,
                ManagerAuthenticationState.LinkRecoveryAdministratorPurpose);
            var identity = await strategy.AuthenticateAsync(context, credentials, cancellationToken);
            return identity is Result<ValidatedManagerIdentity, AeroError>.Ok(var valid)
                ? await links.CompleteAsync(prepared, valid, cancellationToken)
                : Failed<ManagerFederationCallbackResult>();
        }
    }

    public async Task<Result<ManagerFederationBeginResult, AeroError>> BeginSignInAsync(
        BeginManagerFederatedSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        var modeResult = await modeResolver.ResolveAsync(cancellationToken);
        if (modeResult is not Result<ManagerAuthenticationModeResolution, AeroError>.Ok(var mode) ||
            mode.Status != ManagerAuthenticationModeStatuses.Remote)
            return Failed<ManagerFederationBeginResult>();

        if (!ManagerFederationValidation.IsExactCallback(request.CallbackUri) ||
            !ManagerFederationValidation.IsSafeReturnPath(request.ReturnPath))
            return Failed<ManagerFederationBeginResult>();

        var binding = await session.Query<ManagerIdentityAuthorityBinding>()
            .FirstOrDefaultAsync(value =>
                value.SingletonKey == ManagerIdentityAuthorityBinding.InstallationSingletonKey,
                cancellationToken);
        if (binding is null || binding.Id != mode.AuthorityBindingId ||
            !string.Equals(binding.Provider, mode.EffectiveProvider, StringComparison.Ordinal) ||
            !ManagerIdentityAuthorityProjector.TryProject(binding, requireActive: true, out var authority) ||
            strategies.Resolve(binding.Provider) is not Result<IManagerIdentityProviderStrategy, AeroError>.Ok(var strategy))
            return Failed<ManagerFederationBeginResult>();

        var credentialsResult = await secrets.ReadAsync(authority.SecretReference, cancellationToken);
        if (credentialsResult is not Result<ManagerProviderCredentialBundle, AeroError>.Ok(var credentials))
            return Failed<ManagerFederationBeginResult>();

        using (credentials)
        {
            var context = new ManagerProviderBeginContext(authority, request.CallbackUri,
                request.ReturnPath, ManagerAuthenticationState.SignInPurpose);
            var preparation = await strategy.PrepareAuthorizationAsync(context, credentials, cancellationToken);
            if (preparation is not Result<ManagerProviderAuthorizationPreparation, AeroError>.Ok(var prepared))
                return Failed<ManagerFederationBeginResult>();
            var state = await states.CreateSignInStateAsync(binding, request.CallbackUri,
                request.ReturnPath, prepared.ProtectedProviderCorrelation, cancellationToken);
            if (state is not Result<string, AeroError>.Ok(var handle))
                return Failed<ManagerFederationBeginResult>();
            var challenge = await strategy.CreateAuthorizationAsync(context, prepared, handle,
                credentials, cancellationToken);
            return challenge is Result<ManagerProviderAuthorizationChallenge, AeroError>.Ok(var value)
                ? Prelude.Ok<ManagerFederationBeginResult, AeroError>(new(handle, value))
                : Failed<ManagerFederationBeginResult>();
        }
    }

    public async Task<Result<ManagerFederationCallbackResult, AeroError>> CompleteSignInAsync(
        CompleteManagerFederationCallbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var modeResult = await modeResolver.ResolveAsync(cancellationToken);
        if (modeResult is not Result<ManagerAuthenticationModeResolution, AeroError>.Ok(var mode) ||
            mode.Status != ManagerAuthenticationModeStatuses.Remote)
            return Failed<ManagerFederationCallbackResult>();

        var preparedResult = await states.PrepareSignInCallbackAsync(request.StateHandle,
            request.CallbackUri, cancellationToken);
        if (preparedResult is not Result<PreparedManagerFederationCallback, AeroError>.Ok(var prepared) ||
            prepared.Binding.Id != mode.AuthorityBindingId ||
            !string.Equals(prepared.Binding.Provider, mode.EffectiveProvider, StringComparison.Ordinal) ||
            !ManagerIdentityAuthorityProjector.TryProject(prepared.Binding, requireActive: true, out var authority) ||
            strategies.Resolve(prepared.Binding.Provider) is not Result<IManagerIdentityProviderStrategy, AeroError>.Ok(var strategy))
            return Failed<ManagerFederationCallbackResult>();
        var credentialsResult = await secrets.ReadAsync(authority.SecretReference, cancellationToken);
        if (credentialsResult is not Result<ManagerProviderCredentialBundle, AeroError>.Ok(var credentials))
            return Failed<ManagerFederationCallbackResult>();
        using (credentials)
        {
            var context = new ManagerProviderCallbackContext(authority, request.CallbackUri,
                request.StateHandle, prepared.State.ProtectedProviderCorrelation, request.Code, request.Error,
                ManagerAuthenticationState.SignInPurpose);
            var identity = await strategy.AuthenticateAsync(context, credentials, cancellationToken);
            return identity is Result<ValidatedManagerIdentity, AeroError>.Ok(var valid)
                ? await links.CompleteSignInAsync(prepared, valid, cancellationToken)
                : Failed<ManagerFederationCallbackResult>();
        }
    }

    public async Task<Result<ManagerFederationCallbackResult, AeroError>> CompleteCallbackAsync(
        string expectedProvider,
        CompleteManagerFederationCallbackRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ManagerIdentityProviders.IsSupported(expectedProvider))
            return Failed<ManagerFederationCallbackResult>();

        var preparedResult = await states.PrepareCallbackAsync(
            request.StateHandle, request.CallbackUri, expectedProvider, cancellationToken);
        if (preparedResult is not Result<PreparedManagerFederationCallback, AeroError>.Ok(var prepared))
            return Failed<ManagerFederationCallbackResult>();

        var isLink = prepared.State.Purpose == ManagerAuthenticationState.LinkRecoveryAdministratorPurpose;
        var modeResult = await modeResolver.ResolveAsync(cancellationToken);
        if (modeResult is not Result<ManagerAuthenticationModeResolution, AeroError>.Ok(var mode) ||
            isLink && (mode.Status != ManagerAuthenticationModeStatuses.Pending ||
                       !string.Equals(mode.RequestedProvider, expectedProvider, StringComparison.Ordinal)) ||
            !isLink && (mode.Status != ManagerAuthenticationModeStatuses.Remote ||
                        mode.AuthorityBindingId != prepared.Binding.Id ||
                        !string.Equals(mode.EffectiveProvider, expectedProvider, StringComparison.Ordinal)))
            return Failed<ManagerFederationCallbackResult>();

        if (!ManagerIdentityAuthorityProjector.TryProject(prepared.Binding, requireActive: !isLink, out var authority) ||
            strategies.Resolve(prepared.Binding.Provider) is not Result<IManagerIdentityProviderStrategy, AeroError>.Ok(var strategy))
            return Failed<ManagerFederationCallbackResult>();

        var credentialsResult = await secrets.ReadAsync(authority.SecretReference, cancellationToken);
        if (credentialsResult is not Result<ManagerProviderCredentialBundle, AeroError>.Ok(var credentials))
            return Failed<ManagerFederationCallbackResult>();

        using (credentials)
        {
            var context = new ManagerProviderCallbackContext(authority, request.CallbackUri,
                request.StateHandle, prepared.State.ProtectedProviderCorrelation, request.Code, request.Error,
                prepared.State.Purpose);
            var identity = await strategy.AuthenticateAsync(context, credentials, cancellationToken);
            if (identity is not Result<ValidatedManagerIdentity, AeroError>.Ok(var valid))
                return Failed<ManagerFederationCallbackResult>();

            return isLink
                ? await links.CompleteAsync(prepared, valid, cancellationToken)
                : await links.CompleteSignInAsync(prepared, valid, cancellationToken);
        }
    }

    public Task RevokeSessionAsync(long sessionId, long userId, CancellationToken cancellationToken = default) =>
        links.RevokeSessionAsync(sessionId, userId, cancellationToken);

    private static Result<T, AeroError> Failed<T>() =>
        Prelude.Fail<T, AeroError>(AeroError.ValidationError(["Manager federation request is invalid."]));
}
