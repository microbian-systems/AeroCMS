# Aero.Cms Spec: Distributed Cache Strategy (Redis / Garnet / FusionCache)

## Goal

Define caching strategy across the CMS.

## Stack

- L1: per-node memory via FusionCache
- L2: distributed cache via Redis or Garnet
- L3: PostgreSQL / Marten

## Use Cases

- published content cache
- rendered shape/view cache
- route lookup cache
- tenant settings cache
- localization resource cache
- permission cache
- media metadata cache
- output caching for public APIs/pages

## Key Rules

1. Every key must be tenant-scoped.
2. Include culture when relevant.
3. Include theme when output/render varies by theme.
4. Avoid caching draft/private data in public output caches.
5. Invalidate by event and by TTL.

## Key Examples

- `tenant:site1:content:blog:123`
- `tenant:site1:culture:en:route:/blog/hello-world`
- `tenant:site1:theme:modern:shape:homepage`

## FusionCache Guidance

Use FusionCache for:
- request coalescing / stampede protection
- fail-safe stale responses
- soft/hard timeouts
- jitter
- background refresh

Reminder:
- default stampede protection is per-node
- distributed lock can be added later for extremely expensive rebuilds

## Redis vs Garnet

Both acceptable as L2 distributed stores when using supported primitives.
Most CMS caching workloads mainly need:
- strings
- hashes
- TTL
- counters
- distributed keys

## Output Caching

Use ASP.NET output caching for:
- public content APIs
- public rendered pages
- cacheable GET endpoints

Vary by:
- tenant
- culture
- auth state if relevant
- route/query where appropriate

## Invalidation Triggers

- content publish/unpublish
- route/slug changes
- theme change
- localization changes
- permission-sensitive content changes
- tenant settings changes

## Background Invalidation

Use TickerQ jobs for larger invalidation work:
- index rebuild + cache clear
- theme asset refresh
- tenant shell rebuild side effects

## Deliverables

1. key naming conventions
2. tenant/culture/theme vary rules
3. FusionCache integration
4. Redis/Garnet provider configuration
5. output caching policy definitions
6. invalidation service
7. tests
