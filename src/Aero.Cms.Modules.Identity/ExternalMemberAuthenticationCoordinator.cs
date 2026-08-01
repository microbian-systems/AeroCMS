using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Core.Http;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Identity;

public sealed record ExternalMemberBrowserBeginRequest(string? InvitationHandle, string ReturnPath);
public sealed record ExternalMemberTrustedRoute(Uri CallbackUri, string RequestHost);
public sealed record ExternalMemberAuthenticationBeginResult(ExternalMemberAuthenticationHandle Handle, ExternalProviderAuthorizationChallenge Challenge);
public sealed record ExternalMemberAuthenticationCallbackResult(ExternalMemberIssuanceReceipt Receipt, ValidatedExternalIdentity Identity);
public interface IExternalMemberAuthenticationCoordinator
{
    Task<Result<ExternalMemberAuthenticationBeginResult, AeroError>> BeginAsync(ExternalMemberBrowserBeginRequest request, ExternalMemberTrustedRoute route, CancellationToken ct = default);
    Task<Result<ExternalMemberAuthenticationCallbackResult, AeroError>> CallbackAsync(string authenticationHandle, ExternalMemberTrustedRoute route, string? code, string? error, CancellationToken ct = default);
}
public sealed class ExternalMemberAuthenticationCoordinator(ISiteContext site, IQuerySession query, IExternalMemberIssuanceService issuance,
    IExternalMemberProviderStrategyFactory strategies, IExternalProviderSecretSource secrets) : IExternalMemberAuthenticationCoordinator
{
    public async Task<Result<ExternalMemberAuthenticationBeginResult, AeroError>> BeginAsync(ExternalMemberBrowserBeginRequest request, ExternalMemberTrustedRoute route, CancellationToken ct = default)
    {
        if (!ValidRoute(route) || !ExternalMemberIssuanceRules.IsSafeLocalReturnPath(request.ReturnPath) || site is not { TenantId: > 0, SiteId: > 0 }) return Fail<ExternalMemberAuthenticationBeginResult>();
        var persistedSite = await query.LoadAsync<SitesModel>(site.SiteId, ct);
        if (persistedSite is not { IsEnabled: true } || persistedSite.TenantId != site.TenantId) return Fail<ExternalMemberAuthenticationBeginResult>();
        var binding = await query.Query<ExternalOrganizationBinding>()
            .FirstOrDefaultAsync(x => x.TenantId == site.TenantId && x.IsActive, ct);
        if (!ExternalProviderAuthorityProjector.TryProject(binding, site.TenantId, out var authority) ||
            strategies.Resolve(authority.Provider) is not Result<IExternalMemberProviderStrategy, AeroError>.Ok(var strategy))
            return Fail<ExternalMemberAuthenticationBeginResult>();

        var credentials = await secrets.ReadAsync(authority.SecretReference, ct);
        if (credentials is not Result<ExternalProviderCredentialBundle, AeroError>.Ok(var bundle))
            return Fail<ExternalMemberAuthenticationBeginResult>();
        using (bundle)
        {
            var context = new ExternalProviderBeginContext(authority, site.SiteId, route.CallbackUri, request.ReturnPath);
            var prepared = await strategy.PrepareAuthorizationAsync(context, bundle, ct);
            if (prepared is not Result<ExternalProviderAuthorizationPreparation, AeroError>.Ok(var prep) ||
                !ExternalMemberIssuanceRules.IsProtectedProviderCorrelation(prep.ProtectedProviderCorrelation))
                return Fail<ExternalMemberAuthenticationBeginResult>();

            var started = await issuance.BeginAsync(
                new(site.TenantId, site.SiteId, authority.BindingId, request.InvitationHandle,
                    authority.Provider, request.ReturnPath, prep.ProtectedProviderCorrelation), ct);
            if (started is not Result<ExternalMemberAuthenticationHandle, AeroError>.Ok(var handle))
                return Fail<ExternalMemberAuthenticationBeginResult>();

            // The committed WU-4a handle is the only local value a provider may place in its state.
            var challenge = await strategy.CreateAuthorizationAsync(context, prep, handle.Handle, bundle, ct);
            return challenge is Result<ExternalProviderAuthorizationChallenge, AeroError>.Ok(var challengeValue)
                ? Prelude.Ok<ExternalMemberAuthenticationBeginResult, AeroError>(new(handle, challengeValue))
                : Fail<ExternalMemberAuthenticationBeginResult>();
        }
    }
    public async Task<Result<ExternalMemberAuthenticationCallbackResult, AeroError>> CallbackAsync(string authenticationHandle, ExternalMemberTrustedRoute route, string? code, string? error, CancellationToken ct = default)
    {
        if (!ValidRoute(route) || site is not { TenantId: > 0, SiteId: > 0 }) return Fail<ExternalMemberAuthenticationCallbackResult>();
        var persistedSite = await query.LoadAsync<SitesModel>(site.SiteId, ct);
        if (persistedSite is not { IsEnabled: true } || persistedSite.TenantId != site.TenantId) return Fail<ExternalMemberAuthenticationCallbackResult>();
        var prep = await issuance.PrepareCallbackAsync(authenticationHandle, site.TenantId, site.SiteId, ct);
        if (prep is not Result<ExternalMemberCallbackPreparationWithProvider, AeroError>.Ok(var state)) return Fail<ExternalMemberAuthenticationCallbackResult>();
        var binding = await query.LoadAsync<ExternalOrganizationBinding>(state.OrganizationBindingId, ct);
        if (!ExternalProviderAuthorityProjector.TryProject(binding, site.TenantId, out var authority) ||
            !string.Equals(authority.Provider, state.Provider, StringComparison.Ordinal) ||
            strategies.Resolve(state.Provider) is not Result<IExternalMemberProviderStrategy, AeroError>.Ok(var strategy))
            return Fail<ExternalMemberAuthenticationCallbackResult>();

        var credentials = await secrets.ReadAsync(authority.SecretReference, ct);
        if (credentials is not Result<ExternalProviderCredentialBundle, AeroError>.Ok(var bundle))
            return Fail<ExternalMemberAuthenticationCallbackResult>();
        using (bundle)
        {
            var identity = await strategy.AuthenticateAsync(new(authority, site.SiteId, route.CallbackUri, authenticationHandle, state.ProtectedProviderCorrelation, Sanitize(code), Sanitize(error), null, null), bundle, ct);
            if (identity is not Result<ValidatedExternalIdentity, AeroError>.Ok(var valid)) return Fail<ExternalMemberAuthenticationCallbackResult>();
            var completed = await issuance.CompleteAsync(new(authenticationHandle, site.TenantId, site.SiteId, state.Provider, valid), ct);
            return completed is Result<ExternalMemberIssuanceReceipt, AeroError>.Ok(var receipt) ? Prelude.Ok<ExternalMemberAuthenticationCallbackResult, AeroError>(new(receipt, valid)) : Fail<ExternalMemberAuthenticationCallbackResult>();
        }
    }
    private static bool ValidRoute(ExternalMemberTrustedRoute r) => r.CallbackUri.IsAbsoluteUri && r.CallbackUri.Scheme == Uri.UriSchemeHttps && r.CallbackUri.IsDefaultPort && string.IsNullOrEmpty(r.CallbackUri.UserInfo) && string.IsNullOrEmpty(r.CallbackUri.Query) && string.IsNullOrEmpty(r.CallbackUri.Fragment) && string.Equals(r.CallbackUri.Host, r.RequestHost, StringComparison.OrdinalIgnoreCase);
    private static string? Sanitize(string? s) => s is { Length: > 0 and <= 2048 } && !s.Any(char.IsControl) ? s : null;
    private static Result<T, AeroError> Fail<T>() => Prelude.Fail<T, AeroError>(AeroError.CreateError("External sign-in is unavailable."));
}
