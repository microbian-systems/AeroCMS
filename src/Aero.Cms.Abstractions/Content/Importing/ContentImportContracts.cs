using Aero.Cms.Abstractions.Content.Views;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aero.Cms.Abstractions.Content.Importing;

/// <summary>Identifies a host-registered content importer.</summary>
public sealed record ContentTypeImporterDescriptor(string Key, string DisplayName, string Version)
{
    public bool IsValid => IsBounded(Key, 128) && IsBounded(DisplayName, 256) && IsBounded(Version, 128);

    internal static bool IsBounded(string? value, int maximumLength) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;
}

/// <summary>Immutable, site-pinned request to import external content.</summary>
public sealed record ContentImportRequest(
    long SiteId,
    string ImporterKey,
    string ImporterVersion,
    string SourceFingerprint,
    string SelectionFingerprint,
    string OptionsJson,
    string Actor,
    bool Activate)
{
    public const int MaximumOptionsJsonLength = 16_384;
    public const int MaximumActorLength = 256;

    public bool IsValid => SiteId > 0 && ContentTypeImporterDescriptor.IsBounded(ImporterKey, 128)
        && ContentTypeImporterDescriptor.IsBounded(ImporterVersion, 128) && ContentTypeImporterDescriptor.IsBounded(SourceFingerprint, 512)
        && ContentTypeImporterDescriptor.IsBounded(SelectionFingerprint, 512) && ContentTypeImporterDescriptor.IsBounded(Actor, MaximumActorLength)
        && OptionsJson is { Length: <= MaximumOptionsJsonLength } && TryCanonicalizeOptions(OptionsJson, out _);

    /// <summary>Canonical JSON used for durable idempotency. Object members are ordered ordinally.</summary>
    public string CanonicalOptionsJson => TryCanonicalizeOptions(OptionsJson, out var canonical)
        ? canonical
        : throw new InvalidOperationException("OptionsJson must be bounded, valid JSON before an import request is persisted.");

    /// <summary>Stable identity that makes a restart resume the same pinned request.</summary>
    public string Identity
    {
        get
        {
            if (!IsValid) throw new InvalidOperationException("An invalid import request cannot have a durable identity.");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Append(hash, SiteId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(hash, ImporterKey); Append(hash, ImporterVersion); Append(hash, SourceFingerprint);
            Append(hash, SelectionFingerprint); Append(hash, CanonicalOptionsJson); Append(hash, Actor);
            Append(hash, Activate ? "1" : "0");
            return Convert.ToHexString(hash.GetHashAndReset());
        }
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static bool TryCanonicalizeOptions(string? json, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumOptionsJsonLength) return false;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer)) WriteCanonical(document.RootElement, writer);
            canonical = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
            return true;
        }
        catch (JsonException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var properties = element.EnumerateObject().ToArray();
                if (properties.Select(static x => x.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                    throw new ArgumentException("Options JSON must not contain duplicate object property names.");
                foreach (var property in properties.OrderBy(static x => x.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(property.Name); WriteCanonical(property.Value, writer); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) WriteCanonical(item, writer); writer.WriteEndArray(); break;
            default: element.WriteTo(writer); break;
        }
    }
}

public enum ContentImportJobState { Pending, Running, Completed, Failed, ManualReview }

public enum ContentImportFailureDisposition { Retryable, Terminal }

/// <summary>Durable platform-owned import-job state.</summary>
public sealed record ContentImportJob(
    long Id, string RequestIdentity, long TenantId, ContentImportRequest Request, ContentImportJobState State,
    int Attempt, string? Checkpoint, long ProgressCurrent, long? ProgressTotal, string? LastError,
    string? LeaseToken, long FencingVersion, DateTimeOffset? LeaseExpiresOn, DateTimeOffset? NextAttemptOn, DateTimeOffset CreatedOn, DateTimeOffset ModifiedOn);

public sealed record ContentImportLease(long JobId, string Token, long FencingVersion, DateTimeOffset ExpiresOn);

/// <summary>Expected provider failure; unexpected exceptions are handled by the worker.</summary>
public sealed record ContentImportProviderResult(bool Succeeded, string? Checkpoint = null, long ProgressCurrent = 0,
    long? ProgressTotal = null, string? Error = null, ContentImportFailureDisposition FailureDisposition = ContentImportFailureDisposition.Retryable)
{
    public static ContentImportProviderResult Success(string? checkpoint = null, long progressCurrent = 0, long? progressTotal = null)
        => new(true, checkpoint, progressCurrent, progressTotal);
    public static ContentImportProviderResult Failure(string error, string? checkpoint = null, ContentImportFailureDisposition disposition = ContentImportFailureDisposition.Retryable)
        => new(false, checkpoint, Error: error, FailureDisposition: disposition);
}

/// <summary>Desired code-owned CMS definitions. Existing manager-edited definitions must match exactly.</summary>
public sealed record ContentImportProvisioningPlan(
    IReadOnlyList<ContentTypeDefinition> ContentTypes,
    IReadOnlyList<ContentSurrealViewRevision> Views)
{
    public static ContentImportProvisioningPlan Empty { get; } = new([], []);
}

public sealed record ContentImportContext(ContentImportJob Job, ContentViewScope Scope);

public interface IContentImportProgressSink
{
    Task<bool> ReportAsync(string? checkpoint, long progressCurrent, long? progressTotal, CancellationToken ct = default);
}

public sealed record ContentImportExecutionContext(ContentImportContext Import, IContentImportProgressSink Progress);

/// <summary>Generic extension point implemented by consuming applications.</summary>
public interface IContentTypeImporter
{
    ContentTypeImporterDescriptor Descriptor { get; }
    Task<ContentImportProvisioningPlan> PlanAsync(ContentImportContext context, CancellationToken ct = default);
    Task<ContentImportProviderResult> ImportAsync(ContentImportExecutionContext context, CancellationToken ct = default);
    Task<ContentImportProviderResult> ActivateAsync(ContentImportExecutionContext context, CancellationToken ct = default);
}

/// <summary>Optional host source that declares deterministic requests to ensure.</summary>
public interface IContentImportRequestSource
{
    Task<IReadOnlyList<ContentImportRequest>> GetRequestsAsync(CancellationToken ct = default);
}

public interface IContentImportJobStore
{
    Task<ContentImportJob?> LoadAsync(long jobId, CancellationToken ct = default);
    Task<ContentImportJob?> EnsureAsync(ContentImportRequest request, long tenantId, CancellationToken ct = default);
    Task<ContentImportLease?> TryClaimAsync(long jobId, string owner, DateTimeOffset now, TimeSpan duration, CancellationToken ct = default);
    Task<bool> RenewAsync(ContentImportLease lease, DateTimeOffset now, TimeSpan duration, CancellationToken ct = default);
    Task<bool> ReportAsync(ContentImportLease lease, string? checkpoint, long progressCurrent, long? progressTotal, CancellationToken ct = default);
    Task<bool> CompleteAsync(ContentImportLease lease, CancellationToken ct = default);
    /// <summary>Persists an expected retryable failure. Null progress preserves the last durable progress.</summary>
    Task<bool> RetryAsync(ContentImportLease lease, string? checkpoint, long? progressCurrent, long? progressTotal, string error, CancellationToken ct = default);
    /// <summary>Persists a terminal failure. Null progress preserves the last durable progress.</summary>
    Task<bool> FailAsync(ContentImportLease lease, string? checkpoint, long? progressCurrent, long? progressTotal, string error, CancellationToken ct = default);
    Task<bool> ReleaseAsync(ContentImportLease lease, CancellationToken ct = default);
    Task<IReadOnlyList<ContentImportJob>> ListRunnableAsync(DateTimeOffset now, int take, CancellationToken ct = default);
}

public interface IContentImportCoordinator
{
    Task<ContentImportProviderResult> ExecuteAsync(ContentImportLease lease, CancellationToken ct = default);
}
