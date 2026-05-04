# Blog Import Feature

## Overview

Add the ability to import blog posts into the AeroCMS blog module. Users can upload `.json`, `.md`, or `.zip` files from the admin blog list page (`/manager/posts`). The system parses the file, resolves tags, optionally searches Pexels for a cover image, and persists the posts as `BlogPostDocument` entities.

---

## Import Format

### JSON Format (primary)

Single post:
```json
{
  "id": 0,
  "title": "How Do App Stores Work?",
  "date": "2023-01-25",
  "slug": "how-do-app-stores-work",
  "coverImage": "https://static.photos/technology/1200x630/210",
  "content": "Full markdown content...",
  "tags": ["app-stores", "mobile-apps", "distribution"]
}
```

The endpoint also accepts a JSON **array** of posts.

### MD Format (simple)

Plain markdown file:
```markdown
# First Heading Becomes Title

Rest of file becomes markdown content...
```

- Title = first `# Heading` line
- Slug = slugified title (lowercase, hyphens)
- Content = everything after the heading
- No tags, no date → `PublishedOn = UtcNow`
- `PublicationState = Published`

### ZIP Format (wrapper)

A `.zip` file containing one or more `.json` files (each matching the JSON format above). The zip must **not** be password protected.

---

## File Processing Rules

| Extension | Handling |
|-----------|----------|
| `.json`   | Deserialize as single post or array of posts |
| `.md`     | Parse heading + body, derive metadata |
| `.zip`    | Extract → find `.json` files → parse each |
| Other     | Skip, add to `Errors` list |

### Zip Extraction Strategy

Use **MemoryStream** for all zip files (up to the 50 MB API limit). No temp dir needed — the entire file is already in memory from the multipart upload:

```
→ open ZipArchive from the uploaded stream
→ enumerate entries matching *.json
→ parse each entry directly from its stream
→ (no temp files, no cleanup)
```

**Rationale**: The server already holds the file in memory (or buffered to disk by Kestrel). A separate in-memory/disk bifurcation at 512KB adds behavioral discontinuity with no real benefit. If Kestrel's `maxRequestBodySize` is set to 50MB, the server handles this.

### Security

- **Zip slip prevention**: Canonicalize via `Path.GetFullPath` and verify result starts with the intended extraction directory:
  ```csharp
  var destPath = Path.GetFullPath(Path.Combine(extractionDir, entry.FullName));
  if (!destPath.StartsWith(Path.GetFullPath(extractionDir), StringComparison.OrdinalIgnoreCase))
      continue; // skip malicious entry
  ```
- **File size limit**: 50 MB maximum via Kestrel `maxRequestBodySize` (or `RequestSizeLimit` attribute)
- **Auth**: Endpoint guarded by `[Authorize]` (consistent with existing admin endpoints)
- **Input validation**: All imported posts validated via FluentValidation before persistence
- **Rate limiting**: 1 import request per 60 seconds per user (configurable)

---

## Mapping: JSON → BlogPostDocument

| JSON Field   | Target                       | Notes                                |
|--------------|------------------------------|--------------------------------------|
| `id`         | —                            | Ignored, new `Snowflake.NewId()`     |
| `title`      | `Title`                      | Direct                              |
| `date`       | `PublishedOn`                | Parse to `DateTimeOffset`           |
| `slug`       | `Slug` + `ContentSlugDocument`| Check → configurable action (see below) |
| `coverImage` | `ImageUrl`                   | Preferred over Pexels if present    |
| `content`    | `Content`                    | Wrapped in `MarkdownBlock`          |
| `tags`       | `TagIds`                     | Resolve or create `Tag` entities    |

Derived fields:
- `PublicationState = Published` (configurable via request option)
- `Excerpt` = first 500 chars of content (optional)
- `CreatedOn` / `ModifiedOn` = `DateTimeOffset.UtcNow`

---

## Slug Conflict Behavior

Configurable via `DuplicateSlugBehavior` enum in the import request:

| Behavior   | Description |
|------------|-------------|
| `Skip`     | Skip duplicate slugs entirely (default) — added to `SkippedPosts` |
| `Suffix`   | Append `-2`, `-3` until unique (e.g., `my-post-2`) |
| `Overwrite`| Replace existing post content entirely (use with caution) |

---

## Image Handling (Pexels)

### Landscape Orientation

Pexels search uses `orientation=landscape` — landscape images fit better as blog headers (wide format, ~1600×500 aspect ratio).

The search API call will be:
```
pexels.SearchPhotosAsync(title, count: 1, orientation: "landscape")
```

If the `IPexelsService.SearchPhotosAsync` signature doesn't currently accept `orientation`, the import service constructs the URL directly with the parameter appended.

### Flow

```
For each imported post:
  1. If JSON has coverImage → ImageUrl = coverImage
  2. Else if PEXELS_API_KEY is set:
     a. pexels.SearchPhotosAsync(title, count: 1, orientation: "landscape")
     b. If photo found:
        → DownloadPhotoAsync(photo, "blog-import", $"{post.Id}")
        → ImageUrl = /media/blog-import/{post.Id}.jpg
     c. If download fails or API errors (after retries) → ImageUrl = /img/placeholder_1600x500.webp
  3. Else → ImageUrl = /img/placeholder_1600x500.webp (no Pexels configured)
```

### API Error Retry

Since Pexels is an external API that can transiently fail:

```
maxRetries = 2
for attempt in 0..maxRetries:
    try:
        result = await pexels.SearchPhotosAsync(title, count: 1, orientation: "landscape")
        if result.Count > 0 → use result[0].Src
        else → break (no results, use placeholder)
        break
    catch:
        if attempt < maxRetries:
            await Task.Delay(1000 * (attempt + 1))  // exponential: 1s, 2s
        else:
            → fall back to /img/placeholder_1600x500.webp
```

Use a simple manual retry loop with linear backoff (no additional Polly dependency needed).

### Placeholder Fallback Image

When Pexels is unavailable, unconfigured, or returns no results, set:
```
ImageUrl = "/img/placeholder_1600x500.webp"
```

This file should exist at `wwwroot/img/placeholder_1600x500.webp` — a generic blog header placeholder.

### Store Locally (Default: Enabled)

The import dialog has a **"Store images locally"** checkbox (default: **checked**):
- Checked → Downloads Pexels photos to `wwwroot/media/blog-import/`; stores local path
- Unchecked → Stores Pexels CDN URL directly (fragile — Pexels CDN may change)

### Parallelization

Pexels API searches are parallelized with `SemaphoreSlim(3)` throttling to avoid overwhelming the external API while still being fast for bulk imports.

### Graceful Degradation

If `PEXELS_API_KEY` env var is not set, skip Pexels calls entirely. `IPexelsService` is resolved as optional (`IPexelsService?`) — posts get the placeholder image.

### Orphaned Image Cleanup

If a post fails to persist after downloading its Pexels image, delete the orphaned file from `wwwroot/media/blog-import/`.

---

## Tag Resolution (Batch)

```
1. Collect ALL unique tag strings across ALL posts
2. Query existing tags in a single call: session.Query<Tag>()
     .Where(t => slugList.Contains(t.Slug)).ToListAsync()
3. Build Dictionary<string, long> from existing tags
4. For tags not found → create Tag entities, add to the dict
5. Assign TagIds to each BlogPostDocument from the dict
```

This avoids the N+1 problem of querying one tag at a time.

---

## Slug Registration (Batch)

- Pre-fetch all existing `ContentSlugDocument` entries that match any imported slug in a single query
- For `Skip` behavior: filter out duplicates before persistence
- For `Suffix` behavior: find the next available suffix per slug
- Reserve all slugs in a single batch (or skip reservation for `Skip` behavior since they're already filtered)

---

## API Design

### Endpoint

```
POST /api/v1/admin/blogs/import
Content-Type: multipart/form-data
```

### Request (multipart form)

| Field              | Type      | Required | Description |
|--------------------|-----------|----------|-------------|
| `file`             | File      | Yes      | .zip, .json, or .md file (max 50MB) |
| `storeLocalImages` | bool      | No       | Default: true |
| `duplicateBehavior`| string    | No       | "skip" (default), "suffix", or "overwrite" |
| `defaultAuthorId`  | long?     | No       | Optional author to assign to all imported posts |
| `publishImported`  | bool      | No       | Default: true (if false, imports as Draft) |

### Response

```json
{
  "totalProcessed": 10,
  "totalImported": 8,
  "totalSkipped": 2,
  "importedPosts": [
    { "id": 12345, "slug": "how-do-app-stores-work", "title": "How Do App Stores Work?" }
  ],
  "skippedPosts": [
    { "slug": "existing-post", "reason": "Slug already exists" }
  ],
  "errors": [
    { "item": "invalid-post.json", "message": "Invalid JSON: missing 'title' field" }
  ]
}
```

### Status Codes

| Code | When |
|------|------|
| **200 OK** | All posts imported successfully |
| **207 Multi-Status** | Partial success (some imported, some skipped/errored) |
| **400 Bad Request** | Invalid/unreadable file, wrong format, extraction failure |
| **413 Payload Too Large** | File exceeds 50 MB |

---

## Architecture: File Parsers (Strategy Pattern)

Instead of one monolithic service method, use a strategy pattern for file dispatch:

```
IBlogImportParser
├── JsonBlogImportParser   (.json)
├── MarkdownBlogImportParser (.md)
└── ZipBlogImportParser    (.zip) → delegates to JsonBlogImportParser
```

```csharp
public interface IBlogImportParser
{
    bool Supports(string fileName);
    Task<Result<List<ImportablePost>, AeroError>> ParseAsync(
        Stream fileStream, string fileName, CancellationToken ct);
}
```

```csharp
public record ImportablePost
{
    public string Title { get; init; }
    public string Slug { get; init; }
    public string MarkdownContent { get; init; }
    public string? CoverImage { get; init; }
    public DateTimeOffset? PublishedOn { get; init; }
    public List<string> Tags { get; init; } = [];
}
```

These parsers are pure transformations (no DB, no I/O beyond reading streams) — easily testable.

---

## Service Layer: IBlogImportService

A dedicated service (not `IBlogPostContentService`) to orchestrate the full import pipeline:

```csharp
public interface IBlogImportService
{
    Task<Result<ImportBlogResult, AeroError>> ImportAsync(
        Stream fileStream,
        string fileName,
        ImportOptions options,
        CancellationToken ct = default);
}

public record ImportOptions(
    bool StoreLocalImages = true,
    DuplicateSlugBehavior DuplicateBehavior = DuplicateSlugBehavior.Skip,
    long? DefaultAuthorId = null,
    bool PublishImported = true
);
```

`IBlogImportService` orchestrates:
1. Resolve parser → `ParseAsync(fileStream, fileName)`
2. Batch tag resolution from DB
3. Check existing slugs (configurable behavior)
4. Parallel Pexels searches (throttled, optional)
5. Create `BlogPostDocument` + `ContentSlugDocument` + `Tag` entities
6. Batch `session.Store()` + single `session.SaveChangesAsync()`
7. Return `ImportBlogResult`

### Implementation location

`src/Aero.Cms.Modules.Blog/BlogImportService.cs`

---

## DTO Placement

Per existing codebase convention, DTOs live alongside the HTTP client:

**`src/Aero.Cms.Abstractions/Http/Clients/BlogClient.cs`** — add to existing file alongside `BlogSummary`, `BlogDetail`, `CreateBlogRequest`, `UpdateBlogRequest`:

```csharp
// Request
public sealed record ImportFileRequest(
    string FileName,
    long FileSize,
    Stream FileStream        // multipart stream, not base64
);

// Response
public sealed record ImportBlogResult(
    int TotalProcessed,
    int TotalImported,
    int TotalSkipped,
    IReadOnlyList<ImportedPostSummary> ImportedPosts,
    IReadOnlyList<SkippedPostInfo> SkippedPosts,
    IReadOnlyList<ImportError> Errors
);

public sealed record ImportedPostSummary(long Id, string Slug, string Title);
public sealed record SkippedPostInfo(string Slug, string Reason);
public sealed record ImportError(string Item, string Message);
```

Wait — since this is a multipart upload (not JSON body), the HTTP client pattern changes. The client will use `MultipartFormDataContent` (not `PostAsJsonAsync`). Update `IBlogHttpClient`:

```csharp
Task<Result<ImportBlogResult, AeroError>> ImportAsync(
    Stream fileStream,
    string fileName,
    bool storeLocalImages = true,
    DuplicateSlugBehavior duplicateBehavior = DuplicateSlugBehavior.Skip,
    long? defaultAuthorId = null,
    bool publishImported = true,
    CancellationToken ct = default);
```

The `HttpClientBase` will need a `PostMultipartAsync` method (or the implementation constructs `MultipartFormDataContent` directly).

---

## FluentValidation

**`src/Aero.Cms.Abstractions/Validators/ImportFileValidator.cs`** (or alongside the DTOs):

```csharp
public class ImportFileValidator : AbstractValidator<ImportFileRequest>
{
    public ImportFileValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.FileSize).InclusiveBetween(1, 50 * 1024 * 1024);
        RuleFor(x => x.FileName).Must(name =>
            name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .WithMessage("File must be .json, .md, or .zip");
    }
}
```

---

## Audit Trail

Add `BlogPostsImportedEvent` to the audit system (in `AeroEvents.cs` or equivalent):

```csharp
public sealed record BlogPostsImportedEvent : AeroEvent
{
    public long UserId { get; init; }
    public string FileName { get; init; }
    public int TotalProcessed { get; init; }
    public int TotalImported { get; init; }
    public int TotalSkipped { get; init; }
    public IReadOnlyList<long> ImportedPostIds { get; init; }
}
```

Logged at the start and end of the import operation.

---

## Files to Create / Modify

### New Files (3)

| # | File | Purpose |
|---|------|---------|
| 1 | `src/Aero.Cms.Modules.Blog/BlogImportService.cs` | Dedicated import orchestration service |
| 2 | `src/Aero.Cms.Modules.Blog/Parsers/JsonBlogImportParser.cs` | Parse `.json` files → `List<ImportablePost>` |
| 3 | `src/Aero.Cms.Modules.Blog/Parsers/MarkdownBlogImportParser.cs` | Parse `.md` files → `List<ImportablePost>` (single) |
| 4 | `src/Aero.Cms.Modules.Blog/Parsers/ZipBlogImportParser.cs` | Extract zip → delegate to `JsonBlogImportParser` |
| 5 | `src/Aero.Cms.Shared/Pages/Manager/ImportDialog.razor` | Radzen dialog for file selection + upload |

### Modified Files (4)

| # | File | Changes |
|---|------|---------|
| 6 | `src/Aero.Cms.Abstractions/Http/Clients/BlogClient.cs` | Add `ImportBlogResult` DTOs + `ImportAsync` to `IBlogHttpClient` |
| 7 | `src/Aero.Cms.Modules.Headless/Areas/Api/v1/BlogApi.cs` | Add `POST /import` endpoint (multipart) |
| 8 | `src/Aero.Cms.Modules.Blog/BlogModule.cs` | Register `IBlogImportService` + parsers in DI |
| 9 | `src/Aero.Cms.Shared/Pages/Manager/Posts.razor` | Add Import button, open dialog, refresh grid on completion |

---

## Implementation Order (Phases)

### Phase 1 — Backend DTOs & HTTP Client
1. Add `ImportBlogResult`, `ImportedPostSummary`, `SkippedPostInfo`, `ImportError` to `BlogClient.cs`
2. Add `ImportAsync` to `IBlogHttpClient` and `BlogHttpClient`
3. Create `ImportFileValidator`

### Phase 2 — File Parsers
4. Create `ImportablePost` record
5. Create `IBlogImportParser` + `JsonBlogImportParser`
6. Create `MarkdownBlogImportParser`
7. Create `ZipBlogImportParser`

### Phase 3 — Import Service
8. Create `ImportOptions` record + `DuplicateSlugBehavior` enum
9. Create `IBlogImportService` + `BlogImportService` with:
   - File dispatch via parser strategy
   - Batch tag resolution
   - Slug dedup (configurable)
   - Batch DB operations
   - Parallel Pexels with throttling

### Phase 4 — API Endpoint
10. Add `POST /import` to `BlogApi.cs`
11. Register services in `BlogModule.cs`

### Phase 5 — UI
12. Create `ImportDialog.razor`
13. Modify `Posts.razor` — import button + dialog trigger

---

## UI Details

### ImportDialog.razor

```
┌─────────────────────────────────┐
│  Import Posts                   │
├─────────────────────────────────┤
│  [File Input: .zip .json .md]  │
│  Accepted: .zip (no password),  │
│  .json, .md | Max: 50MB         │
│                                 │
│  ☑ Store images locally         │
│  [Duplicate handling: Skip ▼]   │
│                                 │
│         [Cancel]  [Import]      │
└─────────────────────────────────┘
```

- Uses Radzen `DialogService.OpenAsync<ImportDialog>()`
- File input via `<InputFile>` or Radzen `<FileInput>` accepting `.zip`, `.json`, `.md`
- "Store images locally" defaults to checked
- Duplicate handling dropdown: Skip (default), Suffix (`-2`), Overwrite
- On submit → reads file stream → calls `BlogApi.ImportAsync()`
- On success (200/207) → shows summary notification → closes dialog → parent refreshes grid
- On error (400/413) → `NotificationService.Notify(NotificationSeverity.Error, ...)`

### Posts.razor Changes

Add button alongside "New Post":
```
┌──────────────────────────────────────────────┐
│  Posts                         [Import] [+ New Post] │
│  ┌──────────────────────────────────────────┐ │
│  │  Title           │ Status │ Created       │ │
│  │  ...             │ ...    │ ...           │ │
│  └──────────────────────────────────────────┘ │
└──────────────────────────────────────────────┘
```

```csharp
async Task OpenImportDialog()
{
    var result = await DialogService.OpenAsync<ImportDialog>("Import Posts",
        new DialogOptions { Width = "640px" });
    if (result is true)
    {
        await grid.Reload();
    }
}
```

---

## Dependencies

- `System.IO.Compression` — built into .NET, no NuGet needed
- `IPexelsService` — already registered by `MediaModule` (via `TryAddScoped`)
- `IBlogPostContentService` — already registered by `BlogModule`
- `IBlogHttpClient` — already registered by `AeroHttpClientRegistrations`

No new NuGet packages required.

---

## Council Review Summary

The spec was reviewed by `@council` (multi-LLM consensus). Key revisions made:

| Issue | Initial Spec | Revised Spec |
|-------|-------------|--------------|
| Transport | Base64 JSON body | Multipart form upload |
| Arch | Add to `IBlogPostContentService` | New `IBlogImportService` |
| File parsing | Inline dispatch | Strategy pattern parsers |
| Zip strategy | 512KB threshold bifurcation | MemoryStream for all (50MB limit) |
| Slug conflict | Silent skip | Configurable: Skip / Suffix / Overwrite |
| Response code | Always 200 | 200 OK / 207 Multi-Status / 400 / 413 |
| Pexels default | "Store locally" unchecked | Default checked, fallback to placeholder |
| Pexels orientation | Default (any) | `orientation=landscape` for blog headers |
| Pexels error handling | Fail silently, no image | Retry 2×, then `/img/placeholder_1600x500.webp` |
| No Pexels configured | `ImageUrl = null` | `ImageUrl = /img/placeholder_1600x500.webp` |
| Pexels perf | Sequential per post | Parallel with SemaphoreSlim(3) |
| Tag resolution | N+1 queries per tag | Batch single query |
| DTO placement | Blog/Models | Abstractions/Http/Clients (per convention) |
| Security | Basic zip-slip | Canonicalized via Path.GetFullPath |
| Auth | Not mentioned | [Authorize] enforced |
| Validation | Not mentioned | FluentValidation |
| Audit trail | Not mentioned | BlogPostsImportedEvent |
