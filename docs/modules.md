# AeroCMS Module System

This document describes how modules function within AeroCMS and lists the currently required modules and their integration details.

## 1. Overview

Modules in AeroCMS are independently deployable assemblies (Razor Class Libraries) that contribute logic, blocks, admin UI, and pipeline hooks to the system. They are loaded dynamically via the `ModuleLoader` and registered in the `ModuleRegistry`.

### Core Contract: `IModule`

Every module must implement the `IModule` interface:

```csharp
public interface IModule
{
    string Name { get; }
    string Version { get; }
    string Author { get; }
    IReadOnlyList<string> Dependencies { get; }
    void ConfigureServices(IServiceCollection services);
    void Configure(IModuleBuilder builder);
}
```

### Module Capabilities

Through the `IModuleBuilder`, a module can:
- **Add Blocks**: Register new `BlockBase` types and their corresponding `IBlockRenderer`.
- **Add Pipeline Hooks**: Intercept page reads (`IPageReadHook`), saves (`IPageSaveHook`), and block rendering (`IBlockRenderHook`).
- **Add Event Handlers**: Subscribe to system events like `PagePublishedEvent` or `FormSubmittedEvent`.
- **Add Admin UI**: Register sections and views in the central Admin dashboard.

---

## 2. Required Modules

The following modules are part of the core ecosystem and must be initialized.

### 2.1 Security Modules

AeroCMS supports pluggable security via the `IAeroSecurity` interface.

- **Security (Identity)**
  - **Function**: Handles authentication and authorization using ASP.NET Core Identity.
  - **Storage**: Uses Entity Framework Core with a dedicated PostgreSQL database context.
  - **Registration**: Registers identity services, roles, and permission-based authorization filters.
- **Simple Security**
  - **Function**: An optional, lightweight security module for basic authentication scenarios (e.g., API keys or simple password checks).

### 2.2 Analytics Modules

Analytics modules are implemented as event handlers that listen for `PageReadCompletedEvent` and other relevant interactions.

- **Facebook Pixel**: Injects the Pixel tracking script and records conversion events.
- **Google Analytics (GA4)**: Handles page views and custom events via gtag.js.
- **LinkedIn Insight**: Injects the Insight Tag for professional demographic tracking.
- **Posthog**: Provides deep product analytics and session recording.
- **Microsoft Clarity**: Enables heatmaps and session replay integration.

### 2.3 Rate Limiting

- **Implementation**: Uses standard `Microsoft.AspNetCore.RateLimiting` middleware.
- **Configuration**: Provides a module to register global and per-endpoint rate-limiting policies (Fixed Window, Sliding Window, Token Bucket, Concurrency).
- **Control**: Allows admin-level configuration of limits per IP or Authenticated User.

### 2.4 Rewrite Module

- **Implementation**: Wraps `Microsoft.AspNetCore.Rewrite` to provide dynamic redirection logic.
- **Database Integration**: Automatically creates 301/302 redirect rules in the database whenever a published page's slug is changed.
- **Manual Rules**: Allows administrators to manually add regex or simple path-based rewrite rules via the Admin UI.
- **Hook**: Plugs into `IPageSaveHook` to detect slug changes during the publishing workflow.

---

## 3. Directory Structure

Modules are located in the `src/Modules` directory:

```text
/Modules
  /Security
    /Aero.Cms.Modules.Security
    /Aero.Cms.Modules.SimpleSecurity
  /Analytics
    /Aero.Cms.Modules.Analytics.Facebook
    /Aero.Cms.Modules.Analytics.Google
    /Aero.Cms.Modules.Analytics.LinkedIn
    /Aero.Cms.Modules.Analytics.Posthog
    /Aero.Cms.Modules.Analytics.Clarity
  /Infrastructure
    /Aero.Cms.Modules.RateLimiting
    /Aero.Cms.Modules.Rewrite
```
