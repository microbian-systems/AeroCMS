# AeroCMS Admin API Refactoring Plan

## Context

The Admin module needs to be refactored into a server-side API-only module, with all UI components moved to Blazor WASM. The architecture shift is:

- **Current**: Admin module contains Razor Pages UI + minimal APIs
- **Target**: Admin module = API-only, UI in Blazor WASM client

---

## TODOs

### 1. Create Admin Module IAeroModule Implementation
- [ ] Convert `Aero.Cms.Modules.Admin` to properly implement `IAeroModule`
- [ ] Create `AdminModule.cs` class extending `AeroModuleBase`
- [ ] Implement `ConfigureServices` to register API services
- [ ] Implement `RunAsync` to map API endpoints
- [ ] Add API module interface (`IApiModule` or similar)
- [ ] Move `PublishApi.cs` and `PreviewApi.cs` to `Api/` folder

### 2. Create Admin API Endpoints in Aero.Cms.Modules.Admin
- [ ] Move `BlogApi.cs` from `Aero.Cms.Modules.Blog\Api` to `Aero.Cms.Modules.Admin\Api`
- [ ] Create `PagesApi.cs` in `Aero.Cms.Modules.Admin\Api` (currently doesn't exist)
- [ ] Create `MediaApi.cs` in `Aero.Cms.Modules.Admin\Api`
- [ ] Create `DashboardApi.cs` in `Aero.Cms.Modules.Admin\Api`
- [ ] Create `NavigationsApi.cs` in `Aero.Cms.Modules.Admin\Api`
- [ ] Create `ModulesApi.cs` in `Aero.Cms.Modules.Admin\Api`
- [ ] Create `CategoriesApi.cs` in `Aero.Cms.Modules.Admin\Api`
- [ ] Create `TagsApi.cs` in `Aero.Cms.Modules.Admin\Api`
- [ ] Create `FilesApi.cs` in `Aero.Cms.Modules.Admin\Api`
- [ ] Create `UsersApi.cs` in `Aero.Cms.Modules.Admin\Api`
- [ ] Create `ThemesApi.cs` in `Aero.Cms.Modules.Admin\Api`
- [ ] Create `SettingsApi.cs` in `Aero.Cms.Modules.Admin\Api`
- [ ] Create `ProfileApi.cs` in `Aero.Cms.Modules.Admin\Api`

### 3. Create AeroClient Typed HTTP Clients in Aero.Cms.Core
- [ ] Create `Aero.Cms.Core/Http/Clients/` folder structure
- [ ] Create `AeroClientBase.cs` inheriting from `HttpClientBase`
- [ ] Create `BlogClient.cs` typed client
- [ ] Create `PagesClient.cs` typed client
- [ ] Create `MediaClient.cs` typed client
- [ ] Create `DashboardClient.cs` typed client
- [ ] Create `NavigationsClient.cs` typed client
- [ ] Create `ModulesClient.cs` typed client
- [ ] Create `CategoriesClient.cs` typed client
- [ ] Create `TagsClient.cs` typed client
- [ ] Create `FilesClient.cs` typed client
- [ ] Create `UsersClient.cs` typed client
- [ ] Create `ThemesClient.cs` typed client
- [ ] Create `SettingsClient.cs` typed client
- [ ] Create `ProfileClient.cs` typed client
- [ ] Register all clients in `AdminModule.ConfigureServices`

### 4. Migrate UI Components to Aero.Cms.Shared
- [ ] Create `Aero.Cms.Shared/Components/` folder
- [ ] Move `BlockPicker.razor` from Admin to Shared
- [ ] Move `BlockEditor.razor` from Admin to Shared
- [ ] Move `_Imports.razor` from Admin to Shared
- [ ] Update namespace references in moved components
- [ ] Add `Aero.Cms.Shared` reference to Blazor WASM project (future)

### 5. Module Storage During Initialization
- [ ] Modify `SetupCompletionService` to store loaded modules in Marten after setup completes
- [ ] Create `ModuleRegistryDocument` for storing enabled/disabled modules
- [ ] Update module discovery to read from stored registry
- [ ] Store modules BEFORE marking setup as complete

### 6. Disable SetupModule After Successful Setup
- [ ] Modify `SetupCompletionService` to set `SetupModule` as disabled after successful completion
- [ ] Create mechanism to mark module as disabled in registry
- [ ] Update `ModuleGraphService` to respect disabled modules

---

## Dependency Order

```
1. Admin Module Implementation (AdminModule.cs)
2. Admin APIs (move BlogApi + create others)
3. AeroClient Base + Typed Clients
4. Component Migration
5. Module Storage Logic
6. Disable SetupModule Logic
```

---

## Notepad Location
- `.sisyphus/notepads/admin-refactor/learnings.md` - Patterns discovered
- `.sisyphus/notepads/admin-refactor/decisions.md` - Architectural choices
- `.sisyphus/notepads/admin-refactor/issues.md` - Problems encountered

---

## Key Files

### Admin Module Structure (Target)
```
Aero.Cms.Modules.Admin/
├── AdminModule.cs           # New - IAeroModule implementation
├── Api/
│   ├── BlogApi.cs          # Moved from Blog module
│   ├── PagesApi.cs         # New
│   ├── MediaApi.cs          # New
│   ├── DashboardApi.cs     # New
│   ├── NavigationsApi.cs    # New
│   ├── ModulesApi.cs        # New
│   ├── CategoriesApi.cs     # New
│   ├── TagsApi.cs           # New
│   ├── FilesApi.cs          # New
│   ├── UsersApi.cs          # New
│   ├── ThemesApi.cs         # New
│   ├── SettingsApi.cs        # New
│   ├── ProfileApi.cs        # New
│   ├── PublishApi.cs         # Existing
│   └── PreviewApi.cs         # Existing
└── (Components removed - migrated to Shared)
```

### AeroClient Structure (Target)
```
Aero.Cms.Core/
└── Http/
    └── Clients/
        ├── AeroClientBase.cs
        ├── BlogClient.cs
        ├── PagesClient.cs
        ├── MediaClient.cs
        └── ... (other clients)
```

### Shared Components Structure (Target)
```
Aero.Cms.Shared/
└── Components/
    ├── BlockPicker.razor    # Moved from Admin
    ├── BlockEditor.razor    # Moved from Admin
    └── _Imports.razor      # Moved from Admin
```

---

## API Endpoint Conventions

Use minimal APIs with pattern:
```csharp
public static class BlogApi
{
    public static void MapBlogApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/blog/posts", ListPosts)
          .WithTags("Admin - Blog");
        // ...
    }
}
```

Endpoints under `/api/v1/admin/{resource}`

---

## Verification

1. All projects build without errors
2. Admin APIs are registered and respond correctly
3. AeroClient classes compile and are registered in DI
4. Components compile in Aero.Cms.Shared
5. Module storage logic works during setup
6. SetupModule is disabled after successful completion

---

## Final Verification Wave

- [ ] F1: Code Review - All new files follow existing patterns
- [ ] F2: Build Verification - `dotnet build` passes for all projects
- [ ] F3: API Verification - Admin APIs respond correctly
- [ ] F4: Integration - Setup completes and modules are stored
