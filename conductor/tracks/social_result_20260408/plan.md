# Implementation Plan: Social Provider Result Standardisation

## Phase 1: Foundation and Priority Providers (Instagram)
- [ ] Task: Audit `ISocialProvider` and `SocialProviderBase` for any missing `Result` return types.
- [ ] Task: Finalise refactor of `InstagramProvider.cs`.
    - [ ] Update `AuthenticateAsync`, `RefreshTokenAsync`, `GenerateAuthUrlAsync`.
    - [ ] Update `PostAsync`, `CommentAsync`, `AnalyticsAsync`.
    - [ ] Ensure all internal helper methods return `Result` where appropriate.
- [ ] Task: Finalise refactor of `InstagramStandaloneProvider.cs`.
    - [ ] Align with `InstagramProvider.cs` signatures.
- [ ] Task: Update and verify existing tests for Instagram providers in `Aero.Social.Tests`.
- [ ] Task: Conductor - User Manual Verification 'Phase 1: Foundation and Priority Providers (Instagram)' (Protocol in workflow.md)

## Phase 2: Bulk Refactor - Authentication Methods
- [ ] Task: Batch refactor `AuthenticateAsync` and `RefreshTokenAsync` for the following providers:
    - [ ] `GmbProvider.cs`, `KickProvider.cs`, `LemmyProvider.cs`.
    - [ ] `LinkedInPageProvider.cs`, `LinkedInProvider.cs`, `MastodonProvider.cs`.
    - [ ] `MediumProvider.cs`, `NostrProvider.cs`, `PinterestProvider.cs`.
    - [ ] `RedditProvider.cs`, `SlackProvider.cs`, `TelegramProvider.cs`.
    - [ ] `ThreadsProvider.cs`, `TikTokProvider.cs`, `TwitchProvider.cs`.
    - [ ] `VkProvider.cs`, `WordPressProvider.cs`, `XProvider.cs`, `YouTubeProvider.cs`.
    - [ ] `BlueskyProvider.cs`, `DevToProvider.cs`, `DiscordProvider.cs`, etc.
- [ ] Task: Conductor - User Manual Verification 'Phase 2: Bulk Refactor - Authentication Methods' (Protocol in workflow.md)

## Phase 3: Bulk Refactor - Action and Data Methods
- [ ] Task: Batch refactor `PostAsync`, `CommentAsync`, `AnalyticsAsync`, and `MentionAsync` for all providers.
    - [ ] Ensure `PostResponse[]` is correctly wrapped in `Result`.
    - [ ] Ensure `AnalyticsData[]?` is correctly wrapped in `Result`.
- [ ] Task: Conductor - User Manual Verification 'Phase 3: Bulk Refactor - Action and Data Methods' (Protocol in workflow.md)

## Phase 4: Consumer and Test Updates
- [ ] Task: Identify all internal consumers of `ISocialProvider` (e.g., Services, Controllers) and update them to handle `Result` types.
- [ ] Task: Update all remaining tests in `Aero.Social.Tests` and related test projects.
- [ ] Task: Fix any broken mocks in the test suites.
- [ ] Task: Conductor - User Manual Verification 'Phase 4: Consumer and Test Updates' (Protocol in workflow.md)

## Phase 5: Final Validation and Stabilization
- [ ] Task: Perform a full build of the solution to ensure no signature mismatches remain.
- [ ] Task: Run the entire `Aero.Social` test suite.
- [ ] Task: Verify that error messages from `AeroError` are correctly propagated to the UI layer (smoke test).
- [ ] Task: Conductor - User Manual Verification 'Phase 5: Final Validation and Stabilization' (Protocol in workflow.md)
