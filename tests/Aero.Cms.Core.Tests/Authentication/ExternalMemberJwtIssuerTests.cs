using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Aero.Auth.Services;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberJwtIssuerTests
{
    [Test]
    public async Task Issuer_preserves_caller_cancellation_without_revoking_the_live_session()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var tokens = Substitute.For<IJwtTokenService>();
        tokens.GenerateAccessTokenAsync(
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<IEnumerable<Claim>?>(),
                cancellation.Token)
            .Returns(Task.FromCanceled<string>(cancellation.Token));
        var revocations = Substitute.For<IExternalMemberSessionRevocationService>();
        var issuer = new ExternalMemberJwtIssuer(tokens, revocations, TimeProvider.System);

        var exception = await CaptureCancellationAsync(async () =>
            await issuer.TryIssueAsync(Receipt(), cancellation.Token));

        await revocations.Received(0).RevokeAsync(
            Arg.Any<ExternalMemberSessionRevocationRequest>(),
            Arg.Any<CancellationToken>());
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.CancellationToken).IsEqualTo(cancellation.Token);
    }

    [Test]
    public async Task Issuer_fails_closed_and_revokes_the_live_session_when_token_generation_fails()
    {
        var tokens = Substitute.For<IJwtTokenService>();
        tokens.GenerateAccessTokenAsync(
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<IEnumerable<Claim>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("signing unavailable")));
        var revocations = Substitute.For<IExternalMemberSessionRevocationService>();
        var issuer = new ExternalMemberJwtIssuer(tokens, revocations, TimeProvider.System);

        var issued = await issuer.TryIssueAsync(Receipt(), CancellationToken.None);

        await Assert.That(issued).IsNull();
        await revocations.Received(1).RevokeAsync(
            Arg.Is<ExternalMemberSessionRevocationRequest>(request =>
                request.TenantId == 4 &&
                request.SiteId == 5 &&
                request.ExternalMemberId == 1 &&
                request.ExternalMemberSessionId == 3 &&
                request.Provider == LocalExternalMemberAuthentication.Provider &&
                request.SecurityVersion == 1),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Issuer_fails_closed_when_a_noncaller_cancellation_is_reported_by_token_generation()
    {
        using var callerCancellation = new CancellationTokenSource();
        using var unrelatedCancellation = new CancellationTokenSource();
        unrelatedCancellation.Cancel();
        var tokens = Substitute.For<IJwtTokenService>();
        tokens.GenerateAccessTokenAsync(
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<IEnumerable<Claim>?>(),
                callerCancellation.Token)
            .Returns(Task.FromCanceled<string>(unrelatedCancellation.Token));
        var revocations = Substitute.For<IExternalMemberSessionRevocationService>();
        var issuer = new ExternalMemberJwtIssuer(tokens, revocations, TimeProvider.System);

        var issued = await issuer.TryIssueAsync(Receipt(), callerCancellation.Token);

        await Assert.That(issued).IsNull();
        await revocations.Received(1).RevokeAsync(
            Arg.Any<ExternalMemberSessionRevocationRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Issuer_uses_empty_email_sentinel_and_returns_exact_minted_expiry()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("01234567890123456789012345678901")) { KeyId = "test" };
        var keys = Substitute.For<IJwtSigningKeyStore>();
        keys.GetSigningCredentialsAsync(Arg.Any<CancellationToken>()).Returns(new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        keys.GetCurrentKeyIdAsync(Arg.Any<CancellationToken>()).Returns("test");
        keys.GetValidationKeysAsync(Arg.Any<CancellationToken>()).Returns(new[] { key });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:AccessTokenLifetimeSeconds"] = "60" }).Build();
        var signer = new JwtTokenService(keys, Substitute.For<ILogger<JwtTokenService>>(), configuration);
        var issuer = new ExternalMemberJwtIssuer(signer, Substitute.For<IExternalMemberSessionRevocationService>(), TimeProvider.System);
        var receipt = new ExternalMemberIssuanceReceipt(1, 2, 3, 4, 5, LocalExternalMemberAuthentication.Provider, 1, DateTimeOffset.UtcNow.AddHours(1), "/");

        var issued = await issuer.TryIssueAsync(receipt, CancellationToken.None);

        await Assert.That(issued).IsNotNull();
        var validated = await signer.ValidateAccessTokenAsync(issued!.Value, CancellationToken.None);
        var emails = validated.Principal!.Claims.Where(c => c.Type is "email" or ClaimTypes.Email).ToArray();
        await Assert.That(emails.Length).IsEqualTo(1);
        await Assert.That(emails[0].Value).IsEqualTo(string.Empty);
        var jwtExpiry = new JwtSecurityTokenHandler().ReadJwtToken(issued.Value).ValidTo;
        await Assert.That(issued.ExpiresAt).IsEqualTo(new DateTimeOffset(jwtExpiry));
    }

    private static ExternalMemberIssuanceReceipt Receipt() => new(
        1,
        2,
        3,
        4,
        5,
        LocalExternalMemberAuthentication.Provider,
        1,
        DateTimeOffset.UtcNow.AddHours(1),
        "/");

    private static async Task<OperationCanceledException?> CaptureCancellationAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (OperationCanceledException exception)
        {
            return exception;
        }
    }
}
