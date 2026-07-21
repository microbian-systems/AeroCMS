using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.Commerce.Payments;

public sealed class CommercePaymentOptions
{
    public const string SectionName = "Commerce:Payments";

    public List<PaymentProviderAccountOptions> Accounts { get; init; } = [];
}

public sealed class PaymentProviderAccountOptions
{
    public string Provider { get; init; } = string.Empty;
    public string AccountKey { get; init; } = string.Empty;
    public long TenantId { get; init; }
    public long SiteId { get; init; }
    public bool Enabled { get; init; }
    public string? SecretKey { get; init; }
    public string? WebhookSecret { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? WebhookId { get; init; }
    public string? BaseUrl { get; init; }
    public IReadOnlyList<string> WalletCapabilities { get; init; } = [];
}

public sealed record PaymentProviderAccount(
    string Provider,
    string AccountKey,
    long TenantId,
    long SiteId,
    string? SecretKey,
    string? WebhookSecret,
    string? ClientId,
    string? ClientSecret,
    string? WebhookId,
    string? BaseUrl,
    IReadOnlyList<string> WalletCapabilities);

public sealed record PaymentInitiation(
    long AttemptId,
    string ProviderReference,
    PaymentAttemptStatus Status,
    string? ClientSecret,
    string? ApprovalUrl);

/// <summary>Classifies provider initiation outcomes so ambiguous calls never become blind retries.</summary>
public enum PaymentInitiationDisposition { Succeeded, RetryableUncertain, TerminalFailure }

public sealed record PaymentProviderInitiationOutcome(
    PaymentInitiationDisposition Disposition,
    PaymentInitiation? Initiation,
    string? Detail)
{
    public static PaymentProviderInitiationOutcome Succeeded(PaymentInitiation initiation) => new(PaymentInitiationDisposition.Succeeded, initiation, null);
    public static PaymentProviderInitiationOutcome Retryable(string? detail = null) => new(PaymentInitiationDisposition.RetryableUncertain, null, detail);
    public static PaymentProviderInitiationOutcome Terminal(string? detail = null) => new(PaymentInitiationDisposition.TerminalFailure, null, detail);
}

public static class PaymentAmountLimits
{
    public const decimal MaxUsdAmount = 1_000_000_000m;
    public static bool IsValidUsd(decimal amount) => amount > 0m && amount <= MaxUsdAmount && decimal.Round(amount, 2, MidpointRounding.ToZero) == amount;
}

public sealed record VerifiedPaymentCallback(
    string EventId,
    string ProviderReference,
    PaymentAttemptStatus Status,
    decimal Amount,
    string Currency,
    string? Detail);

public sealed record PaymentProviderInitiation(string OperationKey, decimal Amount, string Currency, long OrderId);

public interface IPaymentProviderAdapter
{
    string Provider { get; }

    Task<PaymentProviderInitiationOutcome> InitiateAsync(
        PaymentProviderAccount account,
        PaymentProviderInitiation request,
        CancellationToken ct = default);

    /// <summary>Returns current client continuation data for a known provider reference without creating another payment.</summary>
    Task<Result<PaymentInitiation, AeroError>> RetrieveAsync(
        PaymentProviderAccount account,
        string providerReference,
        CancellationToken ct = default);

    Task<Result<VerifiedPaymentCallback, AeroError>> VerifyAndTranslateAsync(
        PaymentProviderAccount account,
        byte[] rawBody,
        IHeaderDictionary headers,
        CancellationToken ct = default);
}

public interface IPaymentProviderRegistry
{
    Result<IPaymentProviderAdapter, AeroError> Resolve(string provider, long tenantId, long siteId);
    Result<PaymentProviderAccount, AeroError> GetAccount(string provider, long tenantId, long siteId);
    Result<PaymentProviderAccount, AeroError> GetAccountByKey(string provider, string accountKey);
}

public sealed class PaymentProviderRegistry(
    IEnumerable<IPaymentProviderAdapter> adapters,
    IOptions<CommercePaymentOptions> options) : IPaymentProviderRegistry
{
    public Result<IPaymentProviderAdapter, AeroError> Resolve(string provider, long tenantId, long siteId)
    {
        var account = GetAccount(provider, tenantId, siteId);
        if (account is not Result<PaymentProviderAccount, AeroError>.Ok) return Prelude.Fail<IPaymentProviderAdapter, AeroError>(Unavailable());

        var normalized = Normalize(provider);
        var matches = adapters.Where(x => string.Equals(x.Provider, normalized, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        return matches.Count == 1
            ? Prelude.Ok<IPaymentProviderAdapter, AeroError>(matches[0])
            : Prelude.Fail<IPaymentProviderAdapter, AeroError>(Unavailable());
    }

    public Result<PaymentProviderAccount, AeroError> GetAccount(string provider, long tenantId, long siteId)
    {
        var normalized = Normalize(provider);
        if (normalized is null || tenantId <= 0 || siteId <= 0) return Prelude.Fail<PaymentProviderAccount, AeroError>(Unavailable());

        var matches = (options.Value.Accounts ?? [])
            .Where(x => x.Enabled && x.TenantId == tenantId && x.SiteId == siteId && string.Equals(x.Provider, normalized, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        return matches.Count == 1
            ? Prelude.Ok<PaymentProviderAccount, AeroError>(ToAccount(matches[0]))
            : Prelude.Fail<PaymentProviderAccount, AeroError>(Unavailable());
    }

    public Result<PaymentProviderAccount, AeroError> GetAccountByKey(string provider, string accountKey)
    {
        var normalized = Normalize(provider);
        if (normalized is null || !TryNormalizeAccountKey(accountKey, out var canonicalKey)) return Prelude.Fail<PaymentProviderAccount, AeroError>(Unavailable());

        var matches = (options.Value.Accounts ?? [])
            .Where(x => x.Enabled && string.Equals(x.Provider, normalized, StringComparison.OrdinalIgnoreCase) && string.Equals(x.AccountKey, canonicalKey, StringComparison.Ordinal))
            .Take(2)
            .ToList();

        return matches.Count == 1
            ? Prelude.Ok<PaymentProviderAccount, AeroError>(ToAccount(matches[0]))
            : Prelude.Fail<PaymentProviderAccount, AeroError>(Unavailable());
    }

    private static PaymentProviderAccount ToAccount(PaymentProviderAccountOptions account) => new(
        account.Provider.Trim().ToLowerInvariant(), account.AccountKey.Trim(), account.TenantId, account.SiteId,
        account.SecretKey, account.WebhookSecret, account.ClientId, account.ClientSecret, account.WebhookId,
        account.BaseUrl, account.WalletCapabilities);

    private static string? Normalize(string? provider) => string.IsNullOrWhiteSpace(provider) ? null : provider.Trim().ToLowerInvariant();
    internal static bool TryNormalizeAccountKey(string? value, out string canonical)
    {
        canonical = value?.Trim() ?? string.Empty;
        return canonical.Length is >= 1 and <= 64 && value == canonical && canonical.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
    }
    private static AeroError Unavailable() => AeroError.CreateError("Payment provider is unavailable.");
}

public sealed class CommercePaymentOptionsValidator : IValidateOptions<CommercePaymentOptions>
{
    private static readonly HashSet<string> SupportedProviders = ["stripe", "paypal"];

    public ValidateOptionsResult Validate(string? name, CommercePaymentOptions options)
    {
        var failures = new List<string>();
        var providerAccountKeys = new HashSet<string>(StringComparer.Ordinal);
        var scopeProviders = new HashSet<string>(StringComparer.Ordinal);

        foreach (var account in (options.Accounts ?? []).Where(x => x.Enabled))
        {
            var provider = account.Provider?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!SupportedProviders.Contains(provider) || !string.Equals(account.Provider, provider, StringComparison.Ordinal))
                failures.Add("Enabled payment accounts must use the normalized provider name 'stripe' or 'paypal'.");
            if (!PaymentProviderRegistry.TryNormalizeAccountKey(account.AccountKey, out var accountKey)) failures.Add("Enabled payment account keys must be trimmed route-safe values of 1-64 letters, digits, '_' or '-'.");
            if (account.TenantId <= 0 || account.SiteId <= 0) failures.Add("Enabled payment accounts require positive tenant and site identifiers.");
            if (!Uri.TryCreate(account.BaseUrl, UriKind.Absolute, out var baseUrl) || baseUrl.Scheme != Uri.UriSchemeHttps)
                failures.Add("Enabled payment accounts require an HTTPS base URL.");

            var providerAccountKey = $"{provider}|{accountKey}";
            if (!providerAccountKeys.Add(providerAccountKey)) failures.Add("Each enabled provider/account key pair must be unique.");
            var scopeKey = $"{provider}|{account.TenantId}|{account.SiteId}";
            if (!scopeProviders.Add(scopeKey)) failures.Add("Each tenant/site can map to a provider only once.");

            if (provider == "stripe" && (string.IsNullOrWhiteSpace(account.SecretKey) || string.IsNullOrWhiteSpace(account.WebhookSecret)))
                failures.Add("Enabled Stripe accounts require SecretKey and WebhookSecret.");
            if (provider == "paypal" && (string.IsNullOrWhiteSpace(account.ClientId) || string.IsNullOrWhiteSpace(account.ClientSecret) || string.IsNullOrWhiteSpace(account.WebhookId)))
                failures.Add("Enabled PayPal accounts require ClientId, ClientSecret, and WebhookId.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

public static class StripeSignatureVerifier
{
    private static readonly TimeSpan MaximumClockSkew = TimeSpan.FromMinutes(5);

    public static bool IsValid(string? signatureHeader, string? webhookSecret, byte[] rawBody, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(webhookSecret) || rawBody.Length == 0) return false;

        long? timestamp = null;
        var signatures = new List<byte[]>();
        foreach (var token in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split('=', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1])) return false;
            if (parts[0] == "t" && long.TryParse(parts[1], out var parsedTimestamp)) timestamp = parsedTimestamp;
            else if (parts[0] == "t") return false;
            if (parts[0] == "v1")
            {
                try { signatures.Add(Convert.FromHexString(parts[1])); }
                catch (FormatException) { return false; }
            }
            else if (parts[0] != "t") return false;
        }

        if (timestamp is null || signatures.Count == 0) return false;
        DateTimeOffset signedAt;
        try { signedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp.Value); }
        catch (ArgumentOutOfRangeException) { return false; }
        if ((utcNow - signedAt).Duration() > MaximumClockSkew) return false;

        var prefix = Encoding.ASCII.GetBytes($"{timestamp.Value}.");
        var signedPayload = new byte[prefix.Length + rawBody.Length];
        Buffer.BlockCopy(prefix, 0, signedPayload, 0, prefix.Length);
        Buffer.BlockCopy(rawBody, 0, signedPayload, prefix.Length, rawBody.Length);
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret), signedPayload);
        return signatures.Any(signature => signature.Length == expected.Length && CryptographicOperations.FixedTimeEquals(expected, signature));
    }
}
