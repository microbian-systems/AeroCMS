# Aero.Cms Spec: Operability, Observability, Health, and Deployment

## Goal

Define the operational requirements for running Aero.Cms in production.

## Logging

All major events should be structured and include:
- tenant
- module
- request ID / trace ID
- user ID where applicable
- shell ID/version if useful

## Metrics

Track:
- request latency
- cache hit/miss
- shell rebuild count
- job queue depth
- job failures/retries
- content publish latency
- search indexing latency
- tenant count
- module enablement failures

## Tracing

Use OpenTelemetry for:
- HTTP requests
- DB calls
- cache calls
- background jobs
- shell rebuild flow

## Health Checks

Health checks should cover:
- database
- distributed cache
- media storage
- job backend
- tenant store
- plugin discovery folder accessibility if applicable

Expose:
- liveness
- readiness

## Deployment Topology

Typical deployment:
- load balancer
- 2+ CMS app nodes
- Redis or Garnet
- PostgreSQL
- object storage
- 1+ TickerQ worker nodes

## Configuration Sources

Support:
- appsettings
- environment variables
- secret provider / key vault
- tenant settings store

## Safe Startup

Startup should:
1. validate module graph
2. validate essential configuration
3. warm critical services optionally
4. not eagerly load all tenant shells if there are many tenants
5. log discovery and effective module counts

## Disaster Recovery

Plan for:
- tenant DB backup/restore
- media backup/restore
- configuration backup
- plugin package recovery
- cache warmup after outage

## Deliverables

1. logging schema
2. metrics list
3. tracing instrumentation plan
4. health checks
5. deployment reference topology
6. backup/restore checklist
7. operational runbook starter
