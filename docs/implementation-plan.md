# AeroCMS Feature Implementation Plan

This plan is based on the docs in `docs/` and is ordered to deliver a working CMS early, then layer advanced platform capabilities.

## Project Purpose (from docs)

AeroCMS is a modular, multi-tenant, block-based CMS on ASP.NET Core (.NET 10+) with:

- Razor-rendered frontend for Pages/Blog
- Minimal APIs for headless/public content delivery
- PostgreSQL + Marten as primary content store
- Pluggable module system (services, endpoints, UI, hooks)
- Tenant-aware configuration and feature enablement
- Strong permissions/RBAC, media, search, caching, and operability

## Implementation Sequence

## Phase 0 - Foundation and Bootstrap

- [ ] Create solution-level architecture skeleton (`Core`, `Infrastructure`, `Modules`, `WebHost`)
- [ ] Implement core contracts (`IModule`, `ModuleDescriptor`, module builder abstractions)
- [ ] Add module discovery and deterministic dependency graph resolution
- [ ] Add tenant context resolver (host/path strategy) and tenant settings model
- [ ] Wire baseline infrastructure (Marten, auth shell, logging, health checks)

Deliverable: host can boot with core module loading and tenant resolution.

## Phase 1 - Base CMS MVP (Blocks + Pages + Blog) **FIRST**

### 1.1 Block System
- [ ] Implement `BlockBase` hierarchy and polymorphic serialization strategy
- [ ] Build block renderer/slice registry and rendering contracts
- [ ] Ship core blocks (RichText, Heading, Image, CTA, Quote, Embed)
- [ ] Add admin block editor metadata contracts

### 1.2 Page and Layout Model
- [ ] Implement page aggregate with culture, slug, status, SEO fields
- [ ] Implement layout regions/columns and ordered block placement
- [ ] Add draft/published lifecycle and publish timestamps
- [ ] Add page repository/service over Marten

### 1.3 Blog Model
- [ ] Implement blog post content type using shared block model
- [ ] Add tags/categories/author/date + list/detail query methods
- [ ] Add blog routes and page routes in frontend rendering pipeline

### 1.4 Rendering + Public Delivery
- [ ] Implement frontend Razor rendering pipeline for pages/blog
- [ ] Implement content read pipeline hooks (core + extension points)
- [ ] Add public Minimal API endpoints for pages/blog (`/api/v1/...`)
- [ ] Add output caching tags and invalidation hooks for page/blog publish

### 1.5 Admin CMS Essentials
- [ ] Implement basic admin shell for Pages and Blog management
- [ ] Create CRUD flows for pages/posts and block composition UI
- [ ] Add preview endpoint and publish/unpublish actions
- [ ] Add minimal audit events for content changes

MVP exit criteria: editors can create blocks, compose pages/blog posts, publish, and serve content via Razor + headless API.

## Phase 2 - Security and Authorization

- [ ] Implement permission definition model and provider registration
- [ ] Implement tenant-scoped roles and role-permission assignments
- [ ] Implement `IPermissionService` and dynamic policy provider (`Permission:*`)
- [ ] Protect admin endpoints/UI visibility by permission checks
- [ ] Add content-level authorization checks (view/edit/publish/delete)

Exit criteria: role-based access works per tenant and content operation.

## Phase 3 - Tenant Provisioning and Lifecycle

- [ ] Implement tenant provisioning service (`Create/Enable/Disable/Rebuild/Delete`)
- [ ] Add hostname uniqueness checks and bootstrap seed flow
- [ ] Implement shell rebuild triggers (modules/theme/localization/auth changes)
- [ ] Add tenant admin management UI/API
- [ ] Add lifecycle auditing and failure-state recovery path

Exit criteria: tenants can be provisioned and safely reconfigured without downtime.

## Phase 4 - Theme Engine

- [ ] Implement theme descriptor/discovery and theme selection per tenant
- [ ] Implement template lookup precedence (theme -> base theme -> module fallback)
- [ ] Add theme settings provider and persisted tenant theme settings
- [ ] Add static asset versioning/hash strategy and optional CDN config
- [ ] Support separate admin/frontend themes

Exit criteria: tenants can switch themes and override module templates predictably.

## Phase 5 - Media Platform

- [ ] Implement `IMediaStorageProvider` abstraction
- [ ] Implement local filesystem provider (dev default)
- [ ] Implement cloud providers (S3/Azure Blob)
- [ ] Build upload API + media metadata persistence
- [ ] Add background jobs for thumbnails/variants/metadata extraction

Exit criteria: tenant-isolated media upload, storage, processing, and retrieval.

## Phase 6 - Search and Indexing

- [ ] Implement `SearchDocument` and storage with pg_vector support
- [ ] Implement indexing pipeline on publish/update/delete events
- [ ] Implement semantic + keyword query services (hybrid ranking)
- [ ] Add admin search and public search APIs
- [ ] Add model/versioned re-embedding jobs

Exit criteria: published content is queryable via keyword and semantic search.

## Phase 7 - Caching Strategy (Triple Threat)

- [ ] Apply output cache policies for pages/blog route groups
- [ ] Integrate FusionCache for hot data and fail-safe reads
- [ ] Add tenant/culture/theme-scoped cache key conventions
- [ ] Implement cache tag eviction on content and media mutations
- [ ] Implement admin purge endpoint (`POST /admin/clear-cache`)

Exit criteria: stable low-latency reads with deterministic invalidation.

## Phase 8 - Plugin Packaging and Marketplace Readiness

- [ ] Define plugin manifest and validation rules
- [ ] Implement installer/unpacker and integrity verification hooks
- [ ] Implement install/update/uninstall flows
- [ ] Add tenant-scoped enablement and shell rebuild integration
- [ ] Add compatibility checks and migration execution hooks

Exit criteria: third-party modules can be installed and activated safely.

## Phase 9 - Operability, Observability, Deployment Hardening

- [ ] Add structured logs for tenant/module/content lifecycle events
- [ ] Add metrics (latency, cache hit rate, rebuilds, job failures, indexing lag)
- [ ] Add OpenTelemetry tracing for request, DB, cache, and background jobs
- [ ] Implement liveness/readiness checks for dependencies
- [ ] Prepare deployment/runbook and backup/restore playbooks

Exit criteria: production-ready diagnostics and operational safety.

## Cross-Cutting Workstreams (run alongside phases)

- [ ] Testing matrix: unit + integration + tenant-isolation + auth + caching
- [ ] Source generators for AOT/perf-sensitive discovery and registration paths (See [marten-aot.md](marten-aot.md) for strategy)
- [ ] Data migrations/versioning strategy for Marten documents
- [ ] Security review (permission bypass, tenant leakage, plugin trust model)
- [ ] Documentation updates per module/phase completion

## Suggested Delivery Milestones

- Milestone A: Phase 0 + Phase 1 (usable base CMS)
- Milestone B: Phase 2 + Phase 3 (secure multi-tenant CMS)
- Milestone C: Phase 4 + Phase 5 + Phase 6 (theming/media/search complete)
- Milestone D: Phase 7 + Phase 8 + Phase 9 (scale + ecosystem + production)
