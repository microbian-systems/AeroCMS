using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Identity;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Derives effective manager authentication from a fresh durable setup and authority snapshot.
/// </summary>
public sealed class ManagerAuthenticationModeResolver(
    IDocumentStore store,
    IBootstrapStateProvider bootstrapStateProvider) : IManagerAuthenticationModeResolver
{
    /// <inheritdoc />
    public async Task<Result<ManagerAuthenticationModeResolution, AeroError>> ResolveAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bootstrap = bootstrapStateProvider.GetState();
            if (!bootstrap.IsRunningMode)
            {
                var requested = AuthenticationProviderSelections.Manager.IsCanonical(
                    bootstrap.RequestedManagerAuthenticationProvider)
                    ? bootstrap.RequestedManagerAuthenticationProvider
                    : AuthenticationProviderSelections.Manager.Local;
                return Success(requested, AuthenticationProviderSelections.Manager.Local,
                    requested == AuthenticationProviderSelections.Manager.Local
                        ? ManagerAuthenticationModeStatuses.Local
                        : ManagerAuthenticationModeStatuses.Pending);
            }

            await using var query = await store.QuerySessionAsync(cancellationToken);
            var setup = await query.LoadAsync<SetupStateDocument>(
                SetupStateDocument.FixedId, cancellationToken);
            if (setup is not { IsComplete: true, RecoveryAdministratorUserId: > 0 } ||
                !AuthenticationProviderSelections.Manager.IsCanonical(
                    setup.RequestedManagerAuthenticationProvider))
                return Failure();

            var requestedProvider = setup.RequestedManagerAuthenticationProvider;
            if (requestedProvider == AuthenticationProviderSelections.Manager.Local)
                return Success(requestedProvider, AuthenticationProviderSelections.Manager.Local,
                    ManagerAuthenticationModeStatuses.Local);

            var bindings = await query.Query<ManagerIdentityAuthorityBinding>()
                .Where(binding => binding.SingletonKey ==
                    ManagerIdentityAuthorityBinding.InstallationSingletonKey)
                .ToListAsync(cancellationToken);
            if (bindings.Count > 1)
                return Failure();
            if (bindings.Count == 0)
                return Pending(requestedProvider);

            var binding = bindings[0];
            if (!ManagerIdentityAuthorityProjector.TryProject(
                    binding, requireActive: false, out _))
                return Failure();

            var activated = binding.IsActive && binding.IsVerified &&
                string.Equals(binding.Provider, requestedProvider, StringComparison.Ordinal) &&
                binding.VerifiedAt is not null &&
                binding.ActivatedAtUtc is not null &&
                binding.VerifiedByUserId == setup.RecoveryAdministratorUserId &&
                binding.ActivatedByRecoveryAdministratorUserId == setup.RecoveryAdministratorUserId;

            return activated
                ? Success(requestedProvider, requestedProvider,
                    ManagerAuthenticationModeStatuses.Remote, binding.Id)
                : Pending(requestedProvider);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Failure();
        }
    }

    private static Result<ManagerAuthenticationModeResolution, AeroError> Pending(string requested) =>
        Success(requested, AuthenticationProviderSelections.Manager.Local,
            ManagerAuthenticationModeStatuses.Pending);

    private static Result<ManagerAuthenticationModeResolution, AeroError> Success(
        string requested, string effective, string status, long? bindingId = null) =>
        Prelude.Ok<ManagerAuthenticationModeResolution, AeroError>(
            new(requested, effective, status, bindingId));

    private static Result<ManagerAuthenticationModeResolution, AeroError> Failure() =>
        Prelude.Fail<ManagerAuthenticationModeResolution, AeroError>(
            AeroError.DatabaseError("Manager authentication mode could not be resolved."));
}
