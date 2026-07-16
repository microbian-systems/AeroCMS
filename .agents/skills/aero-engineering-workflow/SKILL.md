---
name: aero-engineering-workflow
description: Coordinate non-trivial AeroCMS engineering work through evidence, architecture, isolated implementation, and independent review.
---

# AeroCMS Engineering Workflow

Use this workflow for cross-cutting features, persistence changes, module
boundaries, multi-file behavior changes, risky regressions, or work that can be
safely divided between isolated worktrees. Do not use it for a simple answer,
read-only lookup, or a trivial mechanical edit.

## Roles

The parent Orchestrator is the sole coordinator. Specialists report only to the
Orchestrator and must not communicate directly with each other.

- `explorer`: Read-only codebase evidence.
- `architect` / `architect_deep`: Read-only design and bounded work units.
- `fixer`: One implementation work unit in an assigned worktree.
- `oracle` / `oracle_deep`: Independent read-only acceptance review.

## State Machine

1. **Understanding**: create a Task Brief with outcome, constraints, affected
   areas, non-goals, risk, and verification expectations.
2. **Exploring**: send independent read-only questions to Explorers; consolidate
   their reports before the next phase.
3. **Architecting**: send consolidated evidence to Architect. Require exact
   work units, allowed files, dependencies, acceptance criteria, and tests.
4. **Implementing**: send each Fixer one non-overlapping work unit. Parallel
   Fixers require distinct worktrees and branches; never run parallel writers
   in the same checkout.
5. **Oracle review**: Oracle inspects the actual resulting code, diff, and test
   evidence against the request and plan.
6. **Correction loop**: route each Oracle blocking finding to a new bounded
   Fixer work unit, then repeat Oracle review.
7. **Completion**: report completed work, verification results, advisories, and
   intentionally deferred work. Never hide failures or deviations.

## Reasoning Escalation

Use `architect` and `oracle` at `high` reasoning effort by default. Select
`architect_deep` or `oracle_deep` at `xhigh` only when deeper verification is
materially likely to improve correctness: cross-project architecture,
concurrency, distributed consistency, security, compiler/source-generator or
LINQ-provider internals, unclear root cause, or repeated failed reviews.

Do not select XHigh merely because a task is large.

## AeroCMS Constraints

- Read the applicable `AGENTS.md` and relevant project documentation first.
- For .NET questions, use Microsoft Learn before other documentation sources.
- Prefer `Result<T>` / `Option<T>` for business and data-access flows.
- Use TUnit for tests; do not introduce Moq, xUnit, NUnit, or MSTest.
- Do not use npm, Newtonsoft.Json, reflection-based discovery, GUID primary
  keys, or commits unless explicitly authorized.
- Preserve the static Living Standard PageEditor boundary: forms, content-type
  rendering, and page composition are separate concerns unless the task
  explicitly changes that decision.
