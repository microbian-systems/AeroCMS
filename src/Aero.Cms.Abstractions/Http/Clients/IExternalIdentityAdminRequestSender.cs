namespace Aero.Cms.Abstractions.Http.Clients;

/// <summary>
/// Dispatches one external-identity administration request after enforcing an admin-relative route and attaching
/// fresh browser antiforgery handshake material. The caller owns and disposes the returned response.
/// </summary>
public interface IExternalIdentityAdminRequestSender
{
    /// <summary>Dispatches the supplied request without transferring response ownership.</summary>
    Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}
