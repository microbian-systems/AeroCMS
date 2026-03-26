# Aero.Cms Spec: Search Indexing and Query Architecture

## Goal

Define search architecture for published content and admin search using vector-based and full-text search within PostgreSQL.

## Search Use Cases

- Public content semantic search (vector search).
- Admin content search (keyword search).
- Search by content type, tags, categories.
- AI-powered "Related Content" recommendations.
- Incremental indexing/embedding on content change.

## Technology Stack

- **Database**: PostgreSQL with `pg_vector` extension.
- **Embeddings**: .NET SmartComponents or OpenAI/Azure OpenAI embeddings.
- **Orchestration**: TickerQ for background embedding generation.

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
    Task<SearchResultPage> SemanticSearchAsync(VectorSearchRequest request, CancellationToken ct = default);
}
```

## Search Document (Marten/Postgres)

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
    
    // pg_vector column via Marten/Npgsql
    public float[] Embedding { get; set; } 
}
```

## Implementation Strategy: pg_vector

1. **Schema**: Enable `vector` extension in Postgres.
2. **Embeddings**: When content is published, a TickerQ job is triggered.
3. **Processing**: The job extracts plain text from all blocks, sends it to an embedding model, and stores the resulting vector in the `SearchDocument`.
4. **Query**: Use cosine similarity (`<=>` operator) or inner product (`<#>`) for semantic search.

Example SQL (conceptual):
```sql
SELECT title, body_text
FROM search_documents
WHERE tenant_id = 'site1'
ORDER BY embedding <=> '[0.1, 0.2, ...]'
LIMIT 10;
```

## Hybrid Search

Combine PostgreSQL Full-Text Search (FTS) with Vector Search for maximum relevance (RRF - Reciprocal Rank Fusion).

## Job Integration

Use TickerQ for:
- Extraction of text from polymorphic blocks.
- Calling LLM/Embedding APIs.
- Batch re-indexing of vectors when models change.

## Tenant Isolation

Every query and index entry is strictly partitioned by `TenantId`.

## Deliverables

1. Search document model with vector support.
2. Indexer abstraction and Postgres implementation.
3. Embedding generation pipeline using TickerQ.
4. Semantic and Keyword search query APIs.
5. Tests for vector similarity accuracy.
