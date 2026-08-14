using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Content.Views;

namespace Aero.Cms.Core.Content.Views;

/// <summary>
/// Executes a single, already-authorized SurrealQL read through SurrealDB's HTTP SQL endpoint.
/// The response body is capped before JSON parsing, so an unexpectedly large result can never be
/// materialized by this transport. Authentication must use the dedicated SELECT-only identity
/// configured for content views; this class never resolves the application's document session.
/// </summary>
public sealed class SurrealHttpBoundedQueryTransport :
    IContentViewBoundedQueryTransport,
    IAdminReadOnlyContentViewExecutor,
    IDisposable
{
    private const int MaximumParameterCharacters = 256;
    private readonly SableReadOnlyContentViewOptions options;
    private readonly HttpClient httpClient;
    private readonly Uri? sqlEndpoint;

    public SurrealHttpBoundedQueryTransport(SableReadOnlyContentViewOptions options)
        : this(options, CreateHttpClient())
    {
    }

    public SurrealHttpBoundedQueryTransport(SableReadOnlyContentViewOptions options, HttpClient httpClient)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        sqlEndpoint = TryCreateSqlEndpoint(options.Endpoint, out var endpoint) && IsProtectedEndpoint(endpoint)
            ? endpoint
            : null;
    }

    public bool EnforcesLimitsBeforeMaterialization => sqlEndpoint is not null;

    bool IAdminReadOnlyContentViewExecutor.IsReadOnlyGuaranteed
        => options.HasExplicitDedicatedConfiguration && EnforcesLimitsBeforeMaterialization;

    public Task<ContentViewExecutionResult> ExecuteBoundedAsync(
        ContentViewExecutionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Limits.IsValid || request.Take <= 0 || request.Take > request.Limits.MaximumTake)
            throw new InvalidOperationException("The content-view execution limits are invalid.");
        return ExecuteStatementAsync(request.View.SelectStatement, request.Parameters, request.Take, request.Limits, ct);
    }

    Task<ContentViewExecutionResult> IAdminReadOnlyContentViewExecutor.ExecuteAsync(
        ContentSurrealViewRevision view,
        ContentViewScope scope,
        IReadOnlyDictionary<string, object?> parameters,
        ContentViewExecutionLimits limits,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!scope.IsValid || view.Scope != scope || !limits.IsValid)
            throw new InvalidOperationException("The administrator preview scope or limits are invalid.");
        return ExecuteStatementAsync(view.SelectStatement, parameters, limits.MaximumRows, limits, ct);
    }

    private async Task<ContentViewExecutionResult> ExecuteStatementAsync(
        string statement,
        IReadOnlyDictionary<string, object?> parameters,
        int take,
        ContentViewExecutionLimits limits,
        CancellationToken ct)
    {
        if (sqlEndpoint is null || !options.HasExplicitDedicatedConfiguration)
            throw new InvalidOperationException("A protected SurrealDB HTTP endpoint and dedicated read-only identity are required.");
        if (string.IsNullOrWhiteSpace(statement))
            throw new InvalidOperationException("A content-view statement is required.");

        var endpoint = AddParameters(sqlEndpoint, parameters);
        var executionStatement = RewriteHttpParameterTypes(statement, parameters);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(executionStatement, Encoding.UTF8, "text/plain")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
        request.Headers.TryAddWithoutValidation("surreal-ns", options.Namespace);
        request.Headers.TryAddWithoutValidation("surreal-db", options.Database);
        ApplyAuthentication(request.Headers);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(limits.EffectiveTimeout);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (response.Content.Headers.ContentLength is > 0 and var contentLength
            && contentLength > limits.MaximumBytes)
            throw new InvalidOperationException("The read-only database response exceeded the configured byte limit.");

        await using var responseStream = await response.Content.ReadAsStreamAsync(timeout.Token);
        var payload = await ReadBoundedAsync(responseStream, limits.MaximumBytes, timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"The read-only database returned HTTP {(int)response.StatusCode}.");

        return ParseResponse(payload, take, limits);
    }

    private void ApplyAuthentication(HttpRequestHeaders headers)
    {
        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
            return;
        }

        if (!string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password))
        {
            var credential = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
            headers.Authorization = new AuthenticationHeaderValue("Basic", credential);
            return;
        }

        if (!string.IsNullOrWhiteSpace(options.Username) || !string.IsNullOrWhiteSpace(options.Password))
            throw new InvalidOperationException("Dedicated read-only SurrealDB credentials are incomplete.");
        if (options.HasAnonymousLoopbackConfiguration) return;
        throw new InvalidOperationException("Dedicated read-only SurrealDB credentials are incomplete.");
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int maximumBytes, CancellationToken ct)
    {
        var buffer = new byte[maximumBytes + 1];
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);
            if (read == 0) break;
            offset += read;
        }
        if (offset > maximumBytes)
            throw new InvalidOperationException("The read-only database response exceeded the configured byte limit.");
        return buffer.AsSpan(0, offset).ToArray();
    }

    private static ContentViewExecutionResult ParseResponse(byte[] payload, int take, ContentViewExecutionLimits limits)
    {
        using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = limits.MaximumDepth });
        var envelope = document.RootElement.ValueKind switch
        {
            JsonValueKind.Array when document.RootElement.GetArrayLength() == 1 => document.RootElement[0],
            JsonValueKind.Object => document.RootElement,
            _ => throw new InvalidOperationException("The read-only database returned an unexpected response envelope.")
        };
        if (!envelope.TryGetProperty("status", out var status)
            || !string.Equals(status.GetString(), "OK", StringComparison.OrdinalIgnoreCase)
            || !envelope.TryGetProperty("result", out var result)
            || result.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("The read-only database rejected the query.");

        if (result.GetArrayLength() > limits.MaximumRows)
            throw new InvalidOperationException("The read-only database response exceeded the configured row limit.");
        var rows = new List<IReadOnlyDictionary<string, object?>>(Math.Min(result.GetArrayLength(), take));
        var truncated = false;
        foreach (var element in result.EnumerateArray())
        {
            if (rows.Count >= take) { truncated = true; break; }
            if (element.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("A content-view SELECT must return object rows.");
            rows.Add(element.EnumerateObject().ToDictionary(
                property => property.Name,
                property => (object?)property.Value.Clone(),
                StringComparer.Ordinal));
        }
        return new ContentViewExecutionResult(rows, truncated);
    }

    private static Uri AddParameters(Uri endpoint, IReadOnlyDictionary<string, object?> parameters)
    {
        var query = new StringBuilder();
        var normalizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var name = pair.Key.StartsWith('$') ? pair.Key[1..] : pair.Key;
            if (string.IsNullOrWhiteSpace(name)
                || !name.All(character => char.IsLetterOrDigit(character) || character == '_'))
                throw new InvalidOperationException("A content-view parameter name is invalid.");
            if (!normalizedNames.Add(name))
                throw new InvalidOperationException("Content-view parameter names must be unique after wire normalization.");
            var value = FormatParameter(pair.Value);
            if (value.Length > MaximumParameterCharacters)
                throw new InvalidOperationException("A content-view parameter exceeds the configured length limit.");
            query.Append(query.Length == 0 ? '?' : '&')
                .Append(Uri.EscapeDataString(name))
                .Append('=')
                .Append(Uri.EscapeDataString(value));
        }
        return new Uri(endpoint + query.ToString(), UriKind.Absolute);
    }

    private static string FormatParameter(object? value) => value switch
    {
        string text => text,
        bool boolean => boolean ? "true" : "false",
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
            => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        DateTime dateTime => dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        JsonElement { ValueKind: JsonValueKind.String } json => json.GetString() ?? string.Empty,
        JsonElement { ValueKind: JsonValueKind.Number } json => json.GetRawText(),
        JsonElement { ValueKind: JsonValueKind.True } => "true",
        JsonElement { ValueKind: JsonValueKind.False } => "false",
        _ => throw new InvalidOperationException("Only scalar content-view parameters are supported by the HTTP transport.")
    };

    /// <summary>
    /// SurrealDB's HTTP SQL query parameters arrive as strings. The two reserved scope parameters
    /// are server-owned Int64 values, so cast only their exact parameter tokens after the statement
    /// has passed the structural read-only and scope-predicate validation boundary.
    /// </summary>
    private static string RewriteHttpParameterTypes(
        string statement,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var rewritten = statement;
        rewritten = RewriteReservedInt64Parameter(
            rewritten,
            ReservedContentViewScopeBinder.TenantParameter,
            parameters);
        rewritten = RewriteReservedInt64Parameter(
            rewritten,
            ReservedContentViewScopeBinder.SiteParameter,
            parameters);
        return rewritten;
    }

    private static string RewriteReservedInt64Parameter(
        string statement,
        string parameter,
        IReadOnlyDictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue(parameter, out var value) || value is not long)
            throw new InvalidOperationException($"The reserved content-view parameter '{parameter}' must be an Int64 value.");
        return Regex.Replace(
            statement,
            $@"{Regex.Escape(parameter)}(?![A-Za-z0-9_])",
            _ => $"type::int({parameter})",
            RegexOptions.CultureInvariant);
    }

    private static bool TryCreateSqlEndpoint(string? value, out Uri endpoint)
    {
        endpoint = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var configured)
            || configured.Scheme is not ("http" or "https" or "ws" or "wss")) return false;
        var builder = new UriBuilder(configured)
        {
            Scheme = configured.Scheme switch { "ws" => "http", "wss" => "https", _ => configured.Scheme },
            Port = configured.IsDefaultPort ? -1 : configured.Port,
            Query = string.Empty,
            Fragment = string.Empty
        };
        var path = builder.Path.TrimEnd('/');
        if (path.EndsWith("/rpc", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/sql", StringComparison.OrdinalIgnoreCase))
            path = path[..^4];
        builder.Path = path + "/sql";
        endpoint = builder.Uri;
        return true;
    }

    private static bool IsProtectedEndpoint(Uri endpoint)
        => endpoint.Scheme == Uri.UriSchemeHttps || IPAddress.TryParse(endpoint.Host, out var address) && IPAddress.IsLoopback(address)
            || string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase);

    internal static SocketsHttpHandler CreatePrimaryHandler()
        => new()
        {
            AutomaticDecompression = DecompressionMethods.None,
            AllowAutoRedirect = false
        };

    private static HttpClient CreateHttpClient()
        => new(CreatePrimaryHandler())
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

    public void Dispose() => httpClient.Dispose();
}
