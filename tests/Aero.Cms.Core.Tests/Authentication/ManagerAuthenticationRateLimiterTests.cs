using System.Net;
using Aero.Cms.Modules.Identity;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ManagerAuthenticationRateLimiterTests
{
    [Test]
    public async Task Local_login_is_limited_per_transport_address_and_window()
    {
        var clock = new TestTimeProvider();
        var limiter = new ManagerAuthenticationRateLimiter(clock);
        var firstAddress = CreateContext("192.0.2.10");

        for (var attempt = 0; attempt < 5; attempt++)
            await Assert.That(limiter.TryAcquireLocalLogin(firstAddress)).IsTrue();

        await Assert.That(limiter.TryAcquireLocalLogin(firstAddress)).IsFalse();
        await Assert.That(limiter.TryAcquireLocalLogin(CreateContext("192.0.2.11"))).IsTrue();

        clock.Advance(TimeSpan.FromMinutes(15));
        await Assert.That(limiter.TryAcquireLocalLogin(firstAddress)).IsTrue();
    }

    [Test]
    public async Task Federation_begin_has_an_independent_ten_request_budget()
    {
        var limiter = new ManagerAuthenticationRateLimiter(new TestTimeProvider());
        var context = CreateContext("192.0.2.20");

        for (var attempt = 0; attempt < 10; attempt++)
            await Assert.That(limiter.TryAcquireFederationBegin(context)).IsTrue();

        await Assert.That(limiter.TryAcquireFederationBegin(context)).IsFalse();
        await Assert.That(limiter.TryAcquireLocalLogin(context)).IsTrue();
    }

    private static DefaultHttpContext CreateContext(string address)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        return context;
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
