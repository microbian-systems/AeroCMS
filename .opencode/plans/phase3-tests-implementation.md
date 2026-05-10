# Phase 3 Tests — Remaining Implementation

## Already Created (4 files, 8 tests)

| File | Tests | Status |
|---|---|---|
| `tests/.../Services/PageContentServiceTests.cs` | SaveAsync stamps SiteId, CreateAsync stamps SiteId, DeleteAsync own site succeeds, DeleteAsync cross-site rejected | ✅ Created & compiling |
| `tests/.../Services/BlogPostContentServiceTests.cs` | SaveAsync stamps SiteId, DeleteAsync own site succeeds, DeleteAsync cross-site rejected | ✅ Created & compiling |
| `tests/.../Services/DocsServiceTests.cs` | SaveAsync stamps SiteId, ToViewModel maps SiteId, DeleteAsync own site, DeleteAsync cross-site rejected | ✅ Created & compiling |
| `tests/.../Services/SlugRegistryTests.cs` | ReserveAsync stamps SiteId | ✅ Created & compiling |

**One extra fix found during testing**: DocsService.DeleteAsync was missing site ownership guard — added in `DocsService.cs` (returns `AeroError.CreateError` on cross-site delete).

**One csproj change**: Added `Aero.Cms.Modules.Docs` reference to test project.

## Still Needed (1 test)

### File: `tests/.../Integration/SitePipelineChainTests.cs`

Add one more test method and one helper overload:

**Test: `SiteResolution_SetsSiteSlice_ForDownstreamMiddleware`**
- Arrange: Mock `ISiteLookupService` returning a site with `Id=42`, `TenantId=420`
- Arrange: Create host with a capture callback terminal middleware
- Act: Send request with `Host: testsite.com`
- Assert: Response 200 OK
- Assert: Captured `IAeroSiteSlice` is not null
- Assert: Captured `SiteId == 42`
- Assert: Captured `TenantId == 420`

**Helper overload: `CreateHostWithCaptureAsync`**
Same as `CreateHostAsync` but accepts `Action<HttpContext> captureAction` as third parameter. The terminal middleware runs the capture before writing the response.

```csharp
private static async Task<IHost> CreateHostWithCaptureAsync(
    ISiteLookupService siteLookup,
    IAliasRuleCache aliasCache,
    Action<HttpContext> captureAction)
{
    var builder = WebApplication.CreateBuilder([]);
    builder.Services.AddSingleton(siteLookup);
    builder.Services.AddSingleton(aliasCache);
    builder.Services.AddSingleton<AliasRewriteRule>(sp =>
        new AliasRewriteRule(
            aliasCache,
            sp,
            Substitute.For<Microsoft.Extensions.Logging.ILogger<AliasRewriteRule>>()));
    builder.WebHost.UseTestServer();
    var app = builder.Build();
    app.UseMiddleware<SiteResolutionMiddleware>();
    var rule = app.Services.GetRequiredService<AliasRewriteRule>();
    var rewriteOptions = new RewriteOptions().Add(rule);
    app.UseRewriter(rewriteOptions);
    app.Run(async context =>
    {
        captureAction(context);
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync("OK");
    });
    await app.StartAsync();
    return app;
}
```

## Verification

1. `dotnet build` — 0 errors
2. `dotnet test` — confirm no regression (91-94 tests passing, only pre-existing failures)
