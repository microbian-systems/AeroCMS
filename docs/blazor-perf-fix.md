# Blazor Page Loading Performance Fix Plan

**Author:** AI council session (alpha/beta/gamma)  
**Date:** 2026-06-16  
**Status:** Analysis complete, not yet implemented

---

## Root Cause Analysis

### MAUI Path (BlazorHybrid) — Two Critical Failures

| # | Factor | File | Problem |
|---|--------|------|---------|
| A | `Blazor.start()` never called | `wwwroot/index.html:32` | `autostart="false"` set but no manual `Blazor.start()` — **the app hangs forever on "Loading..."** |
| B | Three blocking CDN requests | `wwwroot/index.html:8-11` | Google Fonts (DNS + HTTP) + Tailwind browser runtime (300KB+ JS parse+exec) |
| C | No AOT compilation | `Aero.Cms.csproj` | Missing `<RunAOTCompilation>true</RunAOTCompilation>` — JIT-based startup |
| D | No styled splash | `wwwroot/index.html:30` | Just `<div id="app">Loading...</div>` — zero UX feedback |

### Web WASM Path — Payload & Architecture

| # | Factor | File | Problem |
|---|--------|------|---------|
| E | Full ICU data | `Web.Client.csproj:16` | `BlazorWebAssemblyLoadAllGlobalizationData=true` adds ~2.2 MB |
| F | Monaco editor loaded unconditionally | `App.razor:81-83` | 5+ MB of Monaco scripts on every page, even non-editor routes |
| G | No compression middleware | `AeroCmsExtensions.cs:225` | `_framework/` WASM served uncompressed (15 MB → could be 5 MB with Brotli) |
| H | No `BlazorCacheBootResources` | `Web.Client.csproj` (absent) | Repeat visits re-download entire WASM runtime |
| I | 13+ CSS links + 3+ scripts in `<head>` | `App.razor:39-61` | All render-blocking, no `defer`/`async`/non-blocking CSS load pattern |
| J | No lazy-loaded assemblies | `Web.Client.csproj` | All assemblies (15-25 MB) downloaded eagerly |
| K | No `InteractiveAuto` render mode | `AeroCmsExtensions.cs:274` | Users wait full WASM download before seeing any content |

---

## Prioritized Action Plan

### Week 1 — Emergency Items (~3 hours total)

#### 1. Fix MAUI `Blazor.start()` — app is BROKEN
- **File:** `src/Aero.Cms/wwwroot/index.html`
- **Change:** Add `Blazor.start()` after `autostart="false"` and a styled splash spinner
- **Impact:** 🔴 Critical — app currently cannot render
- **Effort:** 10 minutes

#### 2. Disable full ICU data (save 2.2 MB)
- **File:** `src/Aero.Cms.Web.Client/Aero.Cms.Web.Client.csproj`
- **Change:** Set `<BlazorWebAssemblyLoadAllGlobalizationData>false</BlazorWebAssemblyLoadAllGlobalizationData>`
- **Impact:** 🟢 High — 2.2 MB reduction on every WASM download
- **Effort:** 2 minutes
- **Note:** If Arabic/RTL cultures are needed, add satellite assembly for only those locales instead of all ICU data

#### 3. Add BlazorCacheBootResources (instant reloads)
- **File:** `src/Aero.Cms.Web.Client/Aero.Cms.Web.Client.csproj`
- **Change:** Add `<BlazorCacheBootResources>true</BlazorCacheBootResources>`
- **Impact:** 🟢 High — repeat visits load WASM from CacheStorage (near-instant)
- **Effort:** 2 minutes

#### 4. Remove Monaco editor from critical path
- **File:** `src/Aero.Cms.Web/Components/App.razor` (lines 81-83)
- **Change:** Remove 3 Monaco `<script>` tags. Create a `MonacoLoader.razor` component that dynamically injects scripts only when navigating to editor pages
- **Impact:** 🟢 High — saves 5+ MB from initial load on non-editor pages
- **Effort:** 1-2 hours

#### 5. Add response compression middleware
- **File:** `src/Aero.Cms.Web.Bootstrap/AeroCmsExtensions.cs`
- **Change:** Add `services.AddResponseCompression(options => options.Providers.Add<BrotliCompressionProvider>().Add<GzipCompressionProvider>())` then `app.UseResponseCompression()` before `app.UseStaticFiles()`
- **Impact:** 🟢 High — reduces WASM wire size by 60-75%
- **Effort:** 5 minutes

#### 6. Fix E2E test timeouts
- **Files:** `playwright.config.js` (if exists) / `tests/Aero.Cms.E2E.Tests/EditorSmokeTests.cs`
- **Changes:**
  - Increase default timeout to 120 seconds
  - Add retries: 2 in CI, 1 locally
  - Wait for app-specific selectors instead of network idle state
  - Pre-warm WASM assets by verifying `/_framework/blazor.web.js` returns 200 before tests start
  - Tag fast vs slow tests for separate CI jobs
- **Impact:** 🟡 Medium — reliable CI, fewer false failures
- **Effort:** 1-2 hours

### Weeks 2-3 — Payload Reduction

#### 7. Move scripts to end of body
- **File:** `src/Aero.Cms.Web/Components/App.razor` (lines 77-85)
- **Change:** Move tippy.js, popper.js, nav-tooltip.js, page-editor-shortcuts.js to before `</body>` with `defer`. Only `blazor.web.js` needs bottom placement for `Blazor.start()` timing
- **Impact:** 🟡 Medium — non-blocking script loading
- **Effort:** 30 minutes

#### 8. Migrate CDN Tailwind to build-time CSS
- **File:** Both `src/Aero.Cms/wwwroot/index.html` (MAUI) and `src/Aero.Cms.Web/Components/App.razor`
- **Change:** Replace CDN `<script src="https://cdn.jsdelivr.net/npm/@tailwindcss/browser@4">` with build-time Tailwind CSS pipeline (PostCSS + `tailwindcss` CLI or MSBuild task)
- **Impact:** 🟡 Medium — eliminates 300KB+ JS parse+exec, enables offline use
- **Effort:** 2-4 days

#### 9. Add non-blocking CSS loading
- **File:** `src/Aero.Cms.Web/Components/App.razor`
- **Change:** Use `media="print" onload="this.media='all'"` pattern for theme CSS files to prevent render blocking
- **Impact:** 🟡 Medium — improved perceived load time
- **Effort:** 30 minutes

### Weeks 3-4 — Architecture

#### 10. Enable MAUI AOT compilation
- **File:** `src/Aero.Cms/Aero.Cms.csproj`
- **Change:** Add `<RunAOTCompilation>true</RunAOTCompilation>`
- **Impact:** 🟢 High — 30-50% faster native startup
- **Effort:** 1-3 days (includes CI/CD validation)

#### 11. Configure WebOptimizer CSS/JS bundles
- **File:** `src/Aero.Cms.Modules.WebOptimizer/WebOptimizerModule.cs`
- **Change:** Replace the TODO placeholder with explicit bundles for manager CSS, editor CSS, and core JS
- **Impact:** 🟡 Medium — fewer HTTP requests, minification
- **Effort:** 4-8 hours

#### 12. Add InteractiveAuto render mode
- **File:** `src/Aero.Cms.Web.Bootstrap/AeroCmsExtensions.cs` (lines 134-136, 272-274)
- **Change:** Switch from `InteractiveWebAssembly` to `InteractiveAuto` for most routes, use `InteractiveServer` for editor pages that need low-latency interactivity
- **Impact:** 🟢 High — users see server-rendered content immediately while WASM loads in background
- **Effort:** 1-2 days (includes testing render mode transitions)

### Month 2+ — Structural

| Change | Effort | Benefit |
|--------|--------|---------|
| CDN-hosted WASM framework (Azure CDN / Cloudflare R2) | 2-4 weeks | Edge-served WASM, global latency reduction |
| Streaming rendering + skeleton screens | 2-3 weeks | 200-500ms first paint instead of full page wait |
| Source-generated JSON (trim-safe) | 1-2 weeks | 10-20% smaller WASM, enables aggressive trimming |
| Server-side prerendering with persistent state | 1-2 weeks | Eliminates duplicate WASM API calls after hydration |
| PWA Service Worker | 1-2 weeks | Cache-first for `_framework/`, `_content/`, CDN assets |

---

## E2E Test Timeout Mitigations

| Fix | Details |
|-----|---------|
| Increase timeout | Set global timeout to 120 seconds with retries: `retries: CI ? 2 : 1` |
| Change wait strategy | Use `waitForSelector('.app-loaded')` instead of `waitForLoadState('networkidle')` |
| Pre-warm WASM assets | In `PlaywrightE2EFixture.cs`, verify `/_framework/blazor.web.js` returns HTTP 200 before test suite starts |
| Tag by speed | Tag fast smoke tests (palette, drag) separately from slow tests (full page save/publish) |
| Blazor circuit timeout | Increase Blazor Server circuit timeout in E2E fixture config |

---

## MAUI-Specific Optimizations

| Fix | File | Change |
|-----|------|--------|
| AOT | `Aero.Cms.csproj` | `<RunAOTCompilation>true</RunAOTCompilation>` |
| Styled splash screen | `wwwroot/index.html` | Follow Web splash pattern with spinner + smooth transition |
| Bundle Tailwind locally | `wwwroot/index.html` | Serve from `_content/` instead of CDN (offline-capable) |
| Lazy DI registrations | `MauiProgram.cs` | Defer block editor and catalog registrations to first-use |
| Blazor.start() fix | `wwwroot/index.html` | Add missing `Blazor.start()` call after `autostart="false"` |

---

## WASM-Specific Optimizations

| Fix | File | Change |
|-----|------|--------|
| Bundle + pre-compress assets | `Web.csproj` | Build target to create `.br`/`.gz` copies of `_framework/` output |
| Conditional Monaco loading | New `MonacoLoader.razor` | Dynamic JS import only when route requires code editor |
| Non-blocking CSS loading | `App.razor` | `media="print" onload="this.media='all'"` for theme CSS |
| Reduce DI overhead | `Web.Client/Program.cs` | Deferred initialization for heavy services |
| PWA Service Worker | New `service-worker.js` | Cache-first for `_framework/`, `_content/`, CDN assets |
| ICU data: localized only | `Web.Client.csproj` | Replace `true` with satellite assembly for needed cultures |

---

## File Reference Index

| Path | Relevance |
|------|-----------|
| `src/Aero.Cms/wwwroot/index.html` | MAUI BlazorWebView host — BROKEN, splash, CDN scripts |
| `src/Aero.Cms/MauiProgram.cs` | MAUI startup DI — lazy registration candidate |
| `src/Aero.Cms/Aero.Cms.csproj` | MAUI AOT, trimming, XAML settings |
| `src/Aero.Cms/MainPage.xaml` | BlazorWebView root component mapping |
| `src/Aero.Cms.Web/Components/App.razor` | Server root HTML — render modes, splash, Monaco scripts |
| `src/Aero.Cms.Web.Client/Aero.Cms.Web.Client.csproj` | WASM trimming, SIMD, ICU, cache settings |
| `src/Aero.Cms.Web.Client/Program.cs` | WASM client services — deferred init candidate |
| `src/Aero.Cms.Web.Bootstrap/AeroCmsExtensions.cs` | RenderMode config, compression, middleware pipeline |
| `src/Aero.Cms.Shared/wwwroot/splash.js` | Splash screen control |
| `src/Aero.Cms.Shared/wwwroot/aero-manager.css` | Splash CSS (lines 1386-1414) |
| `src/Aero.Cms.Modules.WebOptimizer/WebOptimizerModule.cs` | CSS/JS minification + bundling TODO |
| `tests/Aero.Cms.E2E.Tests/PlaywrightE2EFixture.cs` | E2E server fixture — pre-warm, timeout config |
| `tests/Aero.Cms.E2E.Tests/EditorSmokeTests.cs` | E2E test cases — retry, wait strategy |

---

*Generated by AI council session. 2 of 3 councillors timed out analyzing this project's load profile — consistent with the performance problems identified.*
