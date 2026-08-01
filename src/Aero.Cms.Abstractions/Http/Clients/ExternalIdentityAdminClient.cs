using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Authentication;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Abstractions.Http.Clients;

/// <summary>Calls the selected-site external identity administration endpoints.</summary>
public interface IExternalIdentityAdminClient
{
    Task<Result<ExternalIdentityAuthorityState, AeroError>> GetAuthorityAsync(
        CancellationToken cancellationToken = default);
    Task<Result<ExternalIdentityAuthorityState, AeroError>> ConfigureAuthorityAsync(
        ConfigureExternalIdentityAuthorityRequest request,
        string antiforgeryToken,
        CancellationToken cancellationToken = default);
    Task<Result<ExternalIdentityInvitationResponse, AeroError>> CreateInvitationAsync(
        CreateExternalIdentityInvitationRequest request,
        string antiforgeryToken,
        CancellationToken cancellationToken = default);
    Task<Result<LocalExternalMemberPasswordResetResponse, AeroError>> IssueLocalPasswordResetAsync(
        long externalMemberId,
        IssueLocalExternalMemberPasswordResetAdminRequest request,
        string antiforgeryToken,
        CancellationToken cancellationToken = default);
}

/// <summary>Uses exact routes and explicit antiforgery headers without logging sensitive bodies.</summary>
public sealed class ExternalIdentityAdminClient(HttpClient httpClient) : IExternalIdentityAdminClient
{
    private const string AuthorityRoute = "api/v1/admin/external-identity/authority";
    private const string InvitationRoute = "api/v1/admin/external-identity/invitations";
    private const string AntiforgeryHeader = "RequestVerificationToken";

    public async Task<Result<ExternalIdentityAuthorityState, AeroError>> GetAuthorityAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(AuthorityRoute, cancellationToken);
            return await ReadAsync<ExternalIdentityAuthorityState>(response, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Failure<ExternalIdentityAuthorityState>(); }
    }

    public Task<Result<ExternalIdentityAuthorityState, AeroError>> ConfigureAuthorityAsync(
        ConfigureExternalIdentityAuthorityRequest request,
        string antiforgeryToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<ConfigureExternalIdentityAuthorityRequest, ExternalIdentityAuthorityState>(
            HttpMethod.Put, AuthorityRoute, request, antiforgeryToken, cancellationToken);

    public Task<Result<ExternalIdentityInvitationResponse, AeroError>> CreateInvitationAsync(
        CreateExternalIdentityInvitationRequest request,
        string antiforgeryToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<CreateExternalIdentityInvitationRequest, ExternalIdentityInvitationResponse>(
            HttpMethod.Post, InvitationRoute, request, antiforgeryToken, cancellationToken);

    public Task<Result<LocalExternalMemberPasswordResetResponse, AeroError>> IssueLocalPasswordResetAsync(
        long externalMemberId,
        IssueLocalExternalMemberPasswordResetAdminRequest request,
        string antiforgeryToken,
        CancellationToken cancellationToken = default)
    {
        if (externalMemberId <= 0)
            return Task.FromResult(Failure<LocalExternalMemberPasswordResetResponse>());
        var route = $"api/v1/admin/external-members/{externalMemberId.ToString(CultureInfo.InvariantCulture)}/local-password-reset";
        return SendAsync<IssueLocalExternalMemberPasswordResetAdminRequest, LocalExternalMemberPasswordResetResponse>(
            HttpMethod.Post, route, request, antiforgeryToken, cancellationToken);
    }

    private async Task<Result<TResponse, AeroError>> SendAsync<TRequest, TResponse>(
        HttpMethod method,
        string route,
        TRequest body,
        string antiforgeryToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(antiforgeryToken) || antiforgeryToken.Any(char.IsControl))
            return Failure<TResponse>();
        try
        {
            using var request = new HttpRequestMessage(method, route)
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.TryAddWithoutValidation(AntiforgeryHeader, antiforgeryToken);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            return await ReadAsync<TResponse>(response, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Failure<TResponse>(); }
    }

    private static async Task<Result<T, AeroError>> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            return value is null ? Failure<T>() : Prelude.Ok<T, AeroError>(value);
        }

        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => Prelude.Fail<T, AeroError>(
                AeroError.ValidationError(["The external identity request is invalid."])),
            HttpStatusCode.NotFound => Prelude.Fail<T, AeroError>(
                AeroError.NotFoundError("The selected site is unavailable.")),
            HttpStatusCode.Conflict => Prelude.Fail<T, AeroError>(
                AeroError.ConflictError("The external identity configuration conflicts with current state.")),
            _ => Failure<T>()
        };
    }

    private static Result<T, AeroError> Failure<T>() => Prelude.Fail<T, AeroError>(
        AeroError.CreateError("External identity administration is unavailable."));
}
