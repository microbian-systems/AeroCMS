using Aero.Cms.Modules.OutputCache.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class CmsOutputCachePolicyRequestRevalidationTests
{
    [Test]
    public async Task Ordinary_eligible_request_allows_lookup_storage_and_locking()
    {
        var context = await ApplyPolicyAsync();

        await Assert.That(context.AllowCacheLookup).IsTrue();
        await Assert.That(context.AllowCacheStorage).IsTrue();
        await Assert.That(context.AllowLocking).IsTrue();
    }

    [Test]
    public async Task Cache_control_no_cache_disables_lookup_but_allows_storage_and_locking()
    {
        var context = await ApplyPolicyAsync(
            configureRequest: request => request.Headers.CacheControl = "public, No-Cache");

        await Assert.That(context.AllowCacheLookup).IsFalse();
        await Assert.That(context.AllowCacheStorage).IsTrue();
        await Assert.That(context.AllowLocking).IsTrue();
    }

    [Test]
    public async Task Cache_control_max_age_zero_disables_lookup_but_allows_storage_and_locking()
    {
        var context = await ApplyPolicyAsync(
            configureRequest: request => request.Headers.CacheControl = "max-age = 0");

        await Assert.That(context.AllowCacheLookup).IsFalse();
        await Assert.That(context.AllowCacheStorage).IsTrue();
        await Assert.That(context.AllowLocking).IsTrue();
    }

    [Test]
    public async Task Cache_control_max_age_zero_with_leading_zeroes_disables_lookup()
    {
        var context = await ApplyPolicyAsync(
            configureRequest: request => request.Headers.CacheControl = "max-age=00");

        await Assert.That(context.AllowCacheLookup).IsFalse();
        await Assert.That(context.AllowCacheStorage).IsTrue();
        await Assert.That(context.AllowLocking).IsTrue();
    }

    [Test]
    public async Task Combined_cache_control_directives_parse_exact_directive_boundaries()
    {
        var context = await ApplyPolicyAsync(
            configureRequest: request =>
                request.Headers.CacheControl = "public, max-age=0, must-revalidate");

        await Assert.That(context.AllowCacheLookup).IsFalse();
        await Assert.That(context.AllowCacheStorage).IsTrue();
        await Assert.That(context.AllowLocking).IsTrue();
    }

    [Test]
    public async Task Pragma_no_cache_disables_lookup_but_allows_storage_and_locking()
    {
        var context = await ApplyPolicyAsync(
            configureRequest: request => request.Headers.Pragma = "custom, NO-CACHE");

        await Assert.That(context.AllowCacheLookup).IsFalse();
        await Assert.That(context.AllowCacheStorage).IsTrue();
        await Assert.That(context.AllowLocking).IsTrue();
    }

    [Test]
    public async Task Inexact_directive_names_and_invalid_delta_seconds_do_not_disable_lookup()
    {
        var context = await ApplyPolicyAsync(
            configureRequest: request =>
                request.Headers.CacheControl =
                    "x-no-cache, x-max-age=0, max-age=-0, max-age=invalid");

        await Assert.That(context.AllowCacheLookup).IsTrue();
        await Assert.That(context.AllowCacheStorage).IsTrue();
        await Assert.That(context.AllowLocking).IsTrue();
    }

    [Test]
    public async Task Ineligible_request_keeps_lookup_and_storage_disabled()
    {
        var context = await ApplyPolicyAsync(
            method: HttpMethods.Post,
            configureRequest: request => request.Headers.CacheControl = "no-cache");

        await Assert.That(context.AllowCacheLookup).IsFalse();
        await Assert.That(context.AllowCacheStorage).IsFalse();
        await Assert.That(context.AllowLocking).IsTrue();
    }

    private static async Task<OutputCacheContext> ApplyPolicyAsync(
        string method = "GET",
        Action<HttpRequest>? configureRequest = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.test");
        httpContext.Request.Path = "/public-page";
        configureRequest?.Invoke(httpContext.Request);

        var context = new OutputCacheContext { HttpContext = httpContext };
        await ((IOutputCachePolicy)CmsOutputCachePolicy.Instance)
            .CacheRequestAsync(context, CancellationToken.None);
        return context;
    }
}
