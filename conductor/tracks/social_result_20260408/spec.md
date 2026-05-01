# Specification: Social Provider Result Standardisation

## Overview
Refactor all social media providers within the `Aero.Social/Providers/` directory to use the standard `Result<T, AeroError>` return type from `Aero.Core/Railway/Result.cs`. This track aims to correct previous failed attempts to standardise provider signatures and ensure a consistent, error-resilient API surface across all integrations.

## Scope
- **Target Files:** All `.cs` files in `Aero/src/Aero.Social/Providers/`.
- **Primary Focus:** `InstagramProvider.cs` and `InstagramStandaloneProvider.cs`.
- **Target Methods:** 
    - Authentication (e.g., `AuthenticateAsync`, `RefreshTokenAsync`, `GenerateAuthUrlAsync`).
    - Actions (e.g., `PostAsync`, `CommentAsync`).
    - Data Retrieval (e.g., `GetUserInfoAsync`, `AnalyticsAsync`).

## Functional Requirements
1. **Signature Update:** Change return types of all scoped methods from direct objects/Tasks to `Task<Result<T, AeroError>>`.
2. **Error Handling:** Replace internal exception-based error propagation with `AeroError` values returned via the `Result` type.
3. **Consistency:** Ensure all providers follow the same pattern for wrapping successful responses and handling failures.
4. **Prioritisation:** Complete the refactor for Instagram-related providers first.

## Non-Functional Requirements
1. **No Logic Bloat:** Do not add new features or modify core integration logic beyond what is required for the signature change.
2. **Backward Compatibility (Internal):** Update all internal consumers of these providers within the project to handle the new `Result` signatures.
3. **Test Integrity:** All existing tests must be updated to accommodate the new return types and must pass.

## Acceptance Criteria
- All 29+ providers compile successfully with the new signatures.
- No intentional `throw` statements exist in the refactored provider methods.
- All unit and integration tests for `Aero.Social` pass.
- Instagram providers are verified as working with the new `Result` pattern.

## Out of Scope
- Implementing new social media integrations.
- Modifying the underlying API endpoints or payload structures.
- Performance optimisations not directly related to the `Result` pattern.
