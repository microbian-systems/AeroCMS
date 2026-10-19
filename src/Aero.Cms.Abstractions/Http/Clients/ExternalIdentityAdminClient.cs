using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        CancellationToken cancellationToken = default);
    Task<Result<ExternalIdentityInvitationResponse, AeroError>> CreateInvitationAsync(
        CreateExternalIdentityInvitationRequest request,
        CancellationToken cancellationToken = default);
    Task<Result<LocalExternalMemberPasswordResetResponse, AeroError>> IssueLocalPasswordResetAsync(
        long externalMemberId,
        IssueLocalExternalMemberPasswordResetAdminRequest request,
        CancellationToken cancellationToken = default);
    Task<Result<ExternalMemberLifecycleResponse, AeroError>> SetLifecycleAsync(
        long externalMemberId,
        ExternalMemberLifecycleRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Uses exact routes and explicit antiforgery headers without logging sensitive bodies.</summary>
public sealed class ExternalIdentityAdminClient : IExternalIdentityAdminClient
{
    private const string AuthorityRoute = "api/v1/admin/external-identity/authority";
    private const string InvitationRoute = "api/v1/admin/external-identity/invitations";
    private const string ExternalMembersRoute = "api/v1/admin/external-members";
    private readonly HttpClient httpClient;
    private readonly IExternalIdentityAdminRequestSender? sender;

    public ExternalIdentityAdminClient(
        HttpClient httpClient,
        IExternalIdentityAdminRequestSender? sender = null)
    {
        this.httpClient = httpClient;
        this.sender = sender;
    }

    public async Task<Result<ExternalIdentityAuthorityState, AeroError>> GetAuthorityAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, AuthorityRoute);
            using var response = sender is null
                ? await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                : await sender.SendAsync(request, cancellationToken);
            return await ReadAsync<ExternalIdentityAuthorityState>(response, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Failure<ExternalIdentityAuthorityState>(); }
    }

    public Task<Result<ExternalIdentityAuthorityState, AeroError>> ConfigureAuthorityAsync(
        ConfigureExternalIdentityAuthorityRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<ConfigureExternalIdentityAuthorityRequest, ExternalIdentityAuthorityState>(
            HttpMethod.Put, AuthorityRoute, request, cancellationToken);

    public Task<Result<ExternalIdentityInvitationResponse, AeroError>> CreateInvitationAsync(
        CreateExternalIdentityInvitationRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<CreateExternalIdentityInvitationRequest, ExternalIdentityInvitationResponse>(
            HttpMethod.Post, InvitationRoute, request, cancellationToken);

    public Task<Result<LocalExternalMemberPasswordResetResponse, AeroError>> IssueLocalPasswordResetAsync(
        long externalMemberId,
        IssueLocalExternalMemberPasswordResetAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        if (externalMemberId <= 0)
            return Task.FromResult(Failure<LocalExternalMemberPasswordResetResponse>());
        var route = $"api/v1/admin/external-members/{externalMemberId.ToString(CultureInfo.InvariantCulture)}/local-password-reset";
        return SendAsync<IssueLocalExternalMemberPasswordResetAdminRequest, LocalExternalMemberPasswordResetResponse>(
            HttpMethod.Post, route, request, cancellationToken);
    }

    public Task<Result<ExternalMemberLifecycleResponse, AeroError>> SetLifecycleAsync(
        long externalMemberId,
        ExternalMemberLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (externalMemberId <= 0)
            return Task.FromResult(Failure<ExternalMemberLifecycleResponse>());
        var route = $"{ExternalMembersRoute}/{externalMemberId.ToString(CultureInfo.InvariantCulture)}/lifecycle";
        return SendAsync<ExternalMemberLifecycleRequest, ExternalMemberLifecycleResponse>(
            HttpMethod.Put, route, request, cancellationToken);
    }

    private async Task<Result<TResponse, AeroError>> SendAsync<TRequest, TResponse>(
        HttpMethod method,
        string route,
        TRequest body,
        CancellationToken cancellationToken)
    {
        if (sender is null)
            return Failure<TResponse>();
        try
        {
            using var request = new HttpRequestMessage(method, route)
            {
                Content = JsonContent.Create(body)
            };
            using var response = await sender.SendAsync(request, cancellationToken);
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

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            var lifecycleCode = await ReadLifecycleUnavailableCodeAsync(response, cancellationToken);
            if (lifecycleCode is not null)
            {
                return Prelude.Fail<T, AeroError>(
                    AeroError.HttpRequestError(HttpStatusCode.ServiceUnavailable, lifecycleCode));
            }
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

    private static async Task<string?> ReadLifecycleUnavailableCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("code", out var code) ||
                code.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return code.GetString() switch
            {
                "lifecycle_commit_unknown" => "lifecycle_commit_unknown",
                "lifecycle_committed_postprocessing_failed" => "lifecycle_committed_postprocessing_failed",
                _ => null
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static Result<T, AeroError> Failure<T>() => Prelude.Fail<T, AeroError>(
        AeroError.CreateError("External identity administration is unavailable."));

}
