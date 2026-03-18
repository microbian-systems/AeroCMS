
# Modular ASP.NET Core Architecture Guide
Author: Generated for implementation
Target: ASP.NET Core (.NET 10+)

---

# Overview

This document describes how to implement a **modular ASP.NET Core platform** supporting:

- Pluggable modules
- Modular endpoint registration
- Multiple authentication systems (JWT, Cookies, API Keys)
- Module dependency ordering
- Modular service registration
- Modular endpoint registration
- Redis/Garnet compatible caching architecture

The goal is to allow a system similar to a **CMS or modular platform** where modules can be discovered and loaded dynamically.

Example modules:

- CoreModule
- UsersModule
- JwtAuthModule
- CookieAuthModule
- BlogModule
- MediaModule
- AdminModule

---

# Required Framework

Framework:

ASP.NET Core

Shared framework reference required in class libraries:

```
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

This provides access to:

- WebApplication
- IEndpointRouteBuilder
- IServiceCollection
- Authentication middleware
- Authorization middleware
- Minimal APIs

---

# Module Interface

Modules expose lifecycle hooks for service registration and endpoint registration.

```csharp
public interface IModule
{
    string Name { get; }
    string Version { get; }
    string Author { get; }
    IReadOnlyList<string> Dependencies { get; }

    void ConfigureServices(IServiceCollection services);

    void Init(IEndpointRouteBuilder endpoints);
}
```

Responsibilities:

ConfigureServices → register services into DI container

Init → register endpoints and routes

---

# Why Use IEndpointRouteBuilder Instead of WebApplication

WebApplication implements IEndpointRouteBuilder.

Using the interface allows modules to work with:

- WebApplication
- RouteGroupBuilder
- Test hosts
- Nested endpoint groups

Example:

```
module.Init(app);
```

or

```
var group = app.MapGroup("/blog");
module.Init(group);
```

---

# Example Module

```
public class BlogModule : IModule
{
    public string Name => "Blog";
    public string Version => "1.0";
    public string Author => "System";
    public IReadOnlyList<string> Dependencies => [];

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<BlogService>();
    }

    public void Init(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/blog");

        group.MapGet("/", () => "Blog home");

        group.MapGet("/posts", (BlogService svc) =>
        {
            return svc.GetPosts();
        });
    }
}
```

---

# Program Startup Using Modules

```
var builder = WebApplication.CreateBuilder(args);

var modules = new List<IModule>
{
    new BlogModule()
};

foreach (var module in modules)
{
    module.ConfigureServices(builder.Services);
}

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

foreach (var module in modules)
{
    module.Init(app);
}

app.Run();
```

---

# Authentication Architecture

Multiple authentication systems can run simultaneously.

Common combination:

- Cookie authentication (for browser users)
- JWT authentication (for APIs)
- API Key authentication (for integrations)

---

# Registering Multiple Authentication Schemes

```
builder.Services
    .AddAuthentication()
    .AddCookie("Cookies")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = ...
    });
```

Endpoints can require specific schemes.

---

# Cookie Protected Endpoint

```
endpoints.MapGet("/admin", () => "admin panel")
.RequireAuthorization(policy =>
{
    policy.AuthenticationSchemes.Add("Cookies");
});
```

---

# JWT Protected Endpoint

```
endpoints.MapGet("/api/products", () => "products")
.RequireAuthorization(policy =>
{
    policy.AuthenticationSchemes.Add("Bearer");
});
```

---

# Allow Either Cookie or JWT

```
.RequireAuthorization(policy =>
{
    policy.AuthenticationSchemes.Add("Cookies");
    policy.AuthenticationSchemes.Add("Bearer");
});
```

---

# JWT Authentication Module Example

```
public class JwtAuthModule : IModule
{
    public string Name => "JwtAuth";
    public string Version => "1.0";
    public string Author => "System";
    public IReadOnlyList<string> Dependencies => [];

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddAuthentication()
        .AddJwtBearer("Bearer", options =>
        {
            options.TokenValidationParameters = ...
        });

        services.AddAuthorization();
    }

    public void Init(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");

        group.MapPost("/login", (LoginRequest req) =>
        {
            var token = JwtTokenGenerator.Generate(req.Username);
            return Results.Ok(new { token });
        });
    }
}
```

---

# Cookie Authentication Module

```
public class CookieAuthModule : IModule
{
    public string Name => "CookieAuth";
    public string Version => "1.0";
    public string Author => "System";
    public IReadOnlyList<string> Dependencies => [];

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddAuthentication()
        .AddCookie("Cookies");
    }

    public void Init(IEndpointRouteBuilder endpoints)
    {
    }
}
```

---

# API Key Authentication Module

Custom handler pattern:

```
services.AddAuthentication()
.AddScheme<ApiKeyOptions, ApiKeyHandler>("ApiKey", null);
```

Used for:

- webhooks
- partner integrations
- internal APIs

---

# Typical Authentication Architecture

Admin UI

Cookie Authentication

API

JWT Authentication

External integrations

API Keys

Enterprise login

OpenID Connect

---

# Modular Route Group Pattern

Modules can define grouped endpoints.

```
var group = endpoints.MapGroup("/blog");

group.MapGet("/", ...);
group.MapGet("/posts", ...);
```

This keeps modules isolated.

---

# Module Dependency Ordering

Modules may depend on other modules.

Example:

```
Core
Users
Auth
Blog
Media
Admin
```

Dependencies property defines order.

Example:

```
IReadOnlyList<string> Dependencies => new[] { "Users" };
```

Loader should resolve dependency graph before initialization.

---

# Module Discovery (Recommended)

Modules can be discovered automatically.

Approach:

1. Scan assemblies
2. Find types implementing IModule
3. Instantiate modules
4. Order by dependencies
5. Initialize

---

# Recommended Cache Architecture

Use multi-layer caching.

L1 cache

Memory cache (per node)

L2 cache

Redis or Garnet distributed cache

L3

Database (Postgres / Marten)

---

# Recommended Stack

| Component | Technology |
|---|---|
| Framework | ASP.NET Core (.NET 10+) |
| **Routing** | **Minimal APIs** (Native AOT optimized) |
| **Rendering** | **Razor Slices** (Reflection-free templates) |
| **Caching** | **Triple Threat** (Output Cache + FusionCache + Marten) |
| L1 Cache | Memory cache (per node) |
| L2 Cache | Redis distributed cache |
| L3 / Store | Marten DB (PostgreSQL) |

---

# Cache Invalidation Strategy

1. **Tag-Based Eviction:** Use `IOutputCacheStore.EvictByTagAsync` to clear cached HTML responses when underlying content changes.
2. **Admin Purge:** Implement a `POST /admin/clear-cache` endpoint for manual intervention.
3. **Fail-Safe:** Leverage FusionCache to serve stale data if the persistent store is under high load or temporarily unavailable.


---

# Summary

This architecture enables:

- modular CMS platform
- pluggable authentication systems
- scalable caching
- reusable modules
- API and UI separation
- microservice compatibility

Modules can implement:

- endpoints
- services
- authentication
- background tasks
- event handlers

This pattern is used by large modular platforms such as:

Orchard Core
ABP Framework

---

END OF DOCUMENT
