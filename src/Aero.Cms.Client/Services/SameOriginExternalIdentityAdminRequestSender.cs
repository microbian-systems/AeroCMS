using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Aero.Cms.Web.Client.Services;

/// <summary>
/// Browser-only external-identity mutation sender. It intentionally owns a
/// dedicated same-origin client instead of using the general typed-client pipeline.
/// </summary>
public sealed class SameOriginExternalIdentityAdminRequestSender : IExternalIdentityAdminRequestSender, IDisposable
{
    private const string AntiforgeryRoute = "api/v1/admin/external-identity/antiforgery";
    private const int MaximumHandshakeBytes = 4096;
    private static readonly HashSet<string> ForbiddenHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cookie", "Set-Cookie", "Host", "Authorization", "Origin", "Content-Length"
    };

    private readonly HttpClient client;
    private readonly Uri trustedHostBaseAddress;
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool disposed;

    public SameOriginExternalIdentityAdminRequestSender(HttpClient client, Uri trustedHostBaseAddress)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentNullException.ThrowIfNull(trustedHostBaseAddress);
        if (!trustedHostBaseAddress.IsAbsoluteUri ||
            (trustedHostBaseAddress.Scheme != Uri.UriSchemeHttp &&
             trustedHostBaseAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("The trusted host base address must be an absolute HTTP(S) URI.",
                nameof(trustedHostBaseAddress));
        }

        this.trustedHostBaseAddress = new Uri(trustedHostBaseAddress.GetLeftPart(UriPartial.Authority) + "/");
        // This client is created only for this scoped sender. Reassigning its base address
        // prevents the configurable typed-client API origin from steering the handshake.
        this.client.BaseAddress = this.trustedHostBaseAddress;
    }

    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendCoreAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var unsafeMutation = ValidateAllowedRequestBeforeDispatch(request);
        if (!unsafeMutation)
        {
            SetBrowserOptions(request);
            EnsureResolvedRequestIsTrusted(request);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (IsRedirect(response.StatusCode) || !ResponseRemainsTrusted(response))
            {
                response.Dispose();
                throw new InvalidOperationException("The external-identity response was not same-origin.");
            }

            return response;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var antiforgery = await AcquireAntiforgeryAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateRequestHeaders(request);
            if (!request.Headers.TryAddWithoutValidation(antiforgery.HeaderName, antiforgery.RequestToken))
                throw new InvalidOperationException("The external-identity antiforgery header could not be attached.");
            SetBrowserOptions(request);
            EnsureResolvedRequestIsTrusted(request);

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (IsRedirect(response.StatusCode) || !ResponseRemainsTrusted(response))
            {
                response.Dispose();
                throw new InvalidOperationException("The external-identity response was not same-origin.");
            }

            return response;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<ExternalIdentityAntiforgeryResponse> AcquireAntiforgeryAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AntiforgeryRoute);
        SetBrowserOptions(request);
        EnsureResolvedRequestIsTrusted(request);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode || IsRedirect(response.StatusCode) || !ResponseRemainsTrusted(response))
            throw new InvalidOperationException("The external-identity antiforgery handshake failed.");

        var payload = await ReadBoundedAsync(response.Content, cancellationToken);
        if (payload is null)
            throw new InvalidOperationException("The external-identity antiforgery handshake was malformed.");

        try
        {
            var value = await JsonSerializer.DeserializeAsync(
                payload,
                ExternalIdentityAdminJsonContext.Default.ExternalIdentityAntiforgeryResponse,
                cancellationToken);
            if (value is null || !IsValidHeaderName(value.HeaderName) || !IsValidToken(value.RequestToken))
                throw new InvalidOperationException("The external-identity antiforgery handshake was malformed.");

            return value;
        }
        catch (JsonException exception)
        {
            _ = exception;
            throw new InvalidOperationException("The external-identity antiforgery handshake was malformed.");
        }
        finally
        {
            Array.Clear(payload.GetBuffer(), 0, (int)payload.Length);
            await payload.DisposeAsync();
        }
    }

    private static async Task<MemoryStream?> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is <= 0 or > MaximumHandshakeBytes)
            return null;

        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        var destination = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(1024);
        try
        {
            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, 1024), cancellationToken);
                if (read == 0)
                    break;
                if (destination.Length + read > MaximumHandshakeBytes)
                {
                    destination.Dispose();
                    return null;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (destination.Length == 0)
            {
                destination.Dispose();
                return null;
            }

            destination.Position = 0;
            return destination;
        }
        catch
        {
            destination.Dispose();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static bool ValidateAllowedRequestBeforeDispatch(HttpRequestMessage request)
    {
        if (request.RequestUri is not { IsAbsoluteUri: false } uri ||
            request.Headers.Any(header => ForbiddenHeaders.Contains(header.Key)))
        {
            throw new InvalidOperationException("The external-identity request is not an allowed same-origin mutation.");
        }

        if (request.Method == HttpMethod.Get &&
            uri.OriginalString == "api/v1/admin/external-identity/authority" &&
            request.Content is null)
        {
            return false;
        }

        if (!IsAllowedUnsafeRoute(request.Method, uri.OriginalString))
            throw new InvalidOperationException("The external-identity request is not an allowed same-origin mutation.");

        return true;
    }

    private static bool IsAllowedUnsafeRoute(HttpMethod method, string route)
    {
        if (string.IsNullOrEmpty(route) || route.StartsWith('/') || route.Contains('\\') ||
            route.Contains('?') || route.Contains('#') || route.StartsWith("../", StringComparison.Ordinal))
        {
            return false;
        }

        if (method == HttpMethod.Put && route == "api/v1/admin/external-identity/authority")
            return true;
        if (method == HttpMethod.Post && route == "api/v1/admin/external-identity/invitations")
            return true;

        const string membersPrefix = "api/v1/admin/external-members/";
        if (!route.StartsWith(membersPrefix, StringComparison.Ordinal))
            return false;
        var suffix = route[membersPrefix.Length..];
        var separator = suffix.IndexOf('/');
        if (separator <= 0)
            return false;
        var memberIdText = suffix[..separator];
        if (!long.TryParse(memberIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var memberId) ||
            memberId <= 0 ||
            !string.Equals(memberIdText, memberId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            return false;

        return (method == HttpMethod.Post && suffix[(separator + 1)..] == "local-password-reset") ||
               (method == HttpMethod.Put && suffix[(separator + 1)..] == "lifecycle");
    }

    private static void ValidateRequestHeaders(HttpRequestMessage request)
    {
        // This dedicated sender owns request headers. Content headers remain on
        // HttpContent, but caller headers cannot carry a stale second token.
        if (request.Headers.Any())
        {
            throw new InvalidOperationException("The external-identity request contains a forbidden header.");
        }
    }

    private static bool IsValidHeaderName(string? value) =>
        !string.IsNullOrEmpty(value) && !ForbiddenHeaders.Contains(value) && value.All(character =>
            (character is >= 'A' and <= 'Z') ||
            (character is >= 'a' and <= 'z') ||
            (character is >= '0' and <= '9') ||
            "!#$%&'*+-.^_`|~".Contains(character));

    private static bool IsValidToken(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumHandshakeBytes && !value.Any(char.IsControl);

    private void EnsureResolvedRequestIsTrusted(HttpRequestMessage request)
    {
        var resolved = request.RequestUri is { IsAbsoluteUri: true } absolute
            ? absolute
            : new Uri(trustedHostBaseAddress, request.RequestUri!);
        if (!IsTrustedOrigin(resolved))
            throw new InvalidOperationException("The external-identity request was not same-origin.");
    }

    private bool ResponseRemainsTrusted(HttpResponseMessage response) =>
        response.RequestMessage?.RequestUri is { IsAbsoluteUri: true } finalUri && IsTrustedOrigin(finalUri);

    private bool IsTrustedOrigin(Uri uri) =>
        string.Equals(uri.Scheme, trustedHostBaseAddress.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(uri.Host, trustedHostBaseAddress.Host, StringComparison.OrdinalIgnoreCase) &&
        uri.Port == trustedHostBaseAddress.Port;

    private static bool IsRedirect(HttpStatusCode statusCode) => (int)statusCode is >= 300 and <= 399;

    private static void SetBrowserOptions(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        request.SetBrowserRequestOption("redirect", "error");
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(SameOriginExternalIdentityAdminRequestSender));
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        gate.Dispose();
        client.Dispose();
    }
}
