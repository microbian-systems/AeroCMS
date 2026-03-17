# Aero.Cms Spec: Search Indexing and Query Architecture

## Goal

Define search architecture for published content and admin search.

## Search Use Cases

- public content search
- admin content search
- search by content type
- search by tags/categories
- faceting/filtering later
- rebuild index per tenant
- incremental indexing on content change

## Abstractions

```csharp
public interface ISearchIndexer
{
    Task IndexAsync(ContentItem item, CancellationToken ct = default);
    Task DeleteAsync(string contentItemId, CancellationToken ct = default);
}

public interface ISearchQueryService
{
    Task<SearchResultPage> QueryAsync(SearchRequest request, CancellationToken ct = default);
}
```

## Search Document

```csharp
public sealed class SearchDocument
{
    public string TenantId { get; set; }
    public string ContentItemId { get; set; }
    public string ContentType { get; set; }
    public string Title { get; set; }
    public string BodyText { get; set; }
    public string Url { get; set; }
    public string Culture { get; set; }
    public bool Published { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
```

## Storage Options

Starter options:
- PostgreSQL full-text search
- external search provider later

A pragmatic first version for Aero.Cms can use PostgreSQL FTS and background jobs for index maintenance.

## Job Integration

Use TickerQ for:
- index on publish
- remove on unpublish/delete
- full rebuild
- tenant-scoped recurring consistency checks

## Tenant Isolation

Every index entry must include tenant.
Queries must include current tenant.

## Deliverables

1. search document model
2. indexer abstraction
3. PostgreSQL FTS implementation
4. query API
5. TickerQ rebuild jobs
6. tests
