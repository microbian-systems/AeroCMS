# Aero.Cms Spec: Media Storage, Assets, and Processing Pipeline

## Goal

Define how media is uploaded, stored, processed, versioned, and served.

## Responsibilities

Media subsystem must support:
- file upload
- folders/logical organization
- metadata
- image resizing
- thumbnail generation
- deduplication if desired
- tenant isolation
- CDN compatibility
- future pluggable providers

## Provider Abstraction

```csharp
public interface IMediaStorageProvider
{
    Task<StoredMedia> SaveAsync(Stream stream, MediaSaveRequest request, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string path, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
    Task<bool> ExistsAsync(string path, CancellationToken ct = default);
}
```

## Provider Implementations

Start with:
- local filesystem provider for dev
- S3-compatible provider
- Azure Blob Storage provider

## Media Model

```csharp
public sealed class MediaItem
{
    public string Id { get; set; }
    public string TenantId { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public long Length { get; set; }
    public string StoragePath { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string Hash { get; set; }
    public DateTime CreatedUtc { get; set; }
}
```

## Processing Pipeline

Background processing tasks:
- thumbnail generation
- image variant generation
- metadata extraction
- virus scanning hook if required
- EXIF cleanup if desired

Use TickerQ for async processing jobs.

## Tenant Isolation

Storage paths should be tenant-scoped:
- `tenant-a/uploads/...`
- `tenant-b/uploads/...`

## Permissions

Permissions examples:
- `Media.View`
- `Media.Upload`
- `Media.Delete`
- `Media.Manage`

## Deliverables

1. media domain model
2. provider abstraction
3. local + cloud providers
4. upload API
5. background processing jobs
6. permission integration
7. tests
