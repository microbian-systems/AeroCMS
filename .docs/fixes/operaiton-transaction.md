# Operation and Transaction Hardening

## Status

Deferred. The regression coverage has been restored, but the AST-era
transaction implementation has not yet been hardened.

## Context

AeroDB `develop` includes the AST/query-compiler refactor introduced by
`e8a5d53` (`refactor: establish Sable query compiler boundary`). A separate
branch, `codex/sable-transaction-hardening`, contains two commits that were not
merged into that refactored line:

- `126dc25` — `fix: harden Sable transaction escape paths`
- `47fb239` — `content types fix`

The dictionary-key escaping behavior from `47fb239` has been restored. The
larger transaction patch from `126dc25` was written against the pre-refactor
`DocumentSession`, so merging or cherry-picking it wholesale would risk
overwriting newer query-compiler, identity, and persistence work.

## Current Evidence

The stranded tests have been ported to the AST-era branch and build against
the local SurrealDB projects.

| Focused suite | Passed | Failed |
|---|---:|---:|
| Dictionary/write literals | 4 | 0 |
| Document transactions | 6 | 2 |
| Transaction escape paths | 1 | 7 |
| **Total** | **11** | **9** |

The nine failures demonstrate that:

1. `AfterCommitAsync` runs during `SaveChangesAsync` inside an explicit
   transaction instead of after the caller commits.
2. An earlier document write can survive when a later write fails.
3. A deferred patch can survive a failed `BeforeCommitAsync` listener.
4. Queued SQL can survive a failed `BeforeCommitAsync` listener.
5. Initial and projection-generated events can survive rollback.
6. A failed multi-event append can persist the events serialized before the
   failure.
7. A rejected cross-database save does not reliably discard pending events and
   projection work.
8. Deferred patch response errors are not propagated.
9. Events appended inside an explicit transaction can survive rollback.

## Decision

Use a hybrid port:

- Reuse the behavioral contract and regression tests from `126dc25`.
- Copy small, structurally compatible helpers where their ownership has not
  changed.
- Adapt the central transaction orchestration to the current AST-era
  `DocumentSession`; do not replace it with the old implementation.

### Code that can be borrowed directly or nearly directly

- `EnsureAllOks()` checks on deferred patch and queued-operation responses.
- Transaction-aware expected-version event append behavior.
- The `OperationSession` concept for routing every mutation through the active
  auto or explicit transaction.
- Transaction state cleanup and listener timing rules.
- The restored transaction and escape-path tests.

### Code that must be adapted

- `SaveChangesAsync` ownership of automatic versus explicit transactions.
- Event mutation routing through the active transaction.
- Deferral of `AfterCommitAsync` until an explicit commit succeeds.
- Restoration of in-memory versions after a rejected or rolled-back write.
- Cleanup of pending events, projections, queued SQL, patches, graph
  operations, and storage operations after rejection or rollback.
- Integration with AST/query-compiler execution and current identity handling.

## Planned Work

1. Introduce or restore an operation-session boundary that represents the
   active mutation transaction.
2. Route deferred patches and queued SQL through that boundary.
3. Restore response validation for every deferred database mutation.
4. Route event appends through the active auto or explicit transaction.
5. Make multi-event append atomic.
6. Defer explicit-transaction commit listeners until the actual commit.
7. Restore rollback cleanup and in-memory version snapshots.
8. Run each focused suite after every bounded change.
9. Run the wider AeroDB and AeroCMS persistence suites after the focused
   regressions pass.

## Acceptance Criteria

- All 20 focused tests pass.
- No mutation path bypasses the active operation transaction.
- Failed patch and queued-SQL responses fail the save.
- `AfterCommitAsync` is never called for a rolled-back transaction.
- Failed saves cannot leave partial documents, events, projections, patches,
  graph mutations, or queued SQL behind.
- Cross-database rejection clears all rejected pending work before the session
  can be reused.
- The AeroDB and AeroCMS projects build without new warnings or dependency
  conflicts.

## Non-goals

- Replacing the AST/query compiler.
- Restoring the pre-refactor `DocumentSession`.
- Adding backward-compatibility shims for obsolete development behavior.
- Changing public content APIs as part of the transaction repair.
