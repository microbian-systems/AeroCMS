using System.Security.Cryptography;
using System.Text;
using Aero.Auth.Services;
using Aero.Cms.Abstractions.Security;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Security;
using AeroDB.Sable;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using SurrealDb.Embedded.InMemory;

namespace Aero.Cms.Core.Tests.Services;

public sealed class ApiKeyServiceTests
{
    private IDocumentStore _store = null!;
    private IDocumentSession _session = null!;
    private IApiKeyGenerator _apiKeyGenerator = null!;
    private ApiKeyService _service = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _store = Documents.For(options =>
        {
            options.ClientFactory = () => new SurrealDbMemoryClient();
            options.Namespace = "test";
            options.Database = Guid.NewGuid().ToString();
            options.Schema.For<ApiKeyDocument>().UniqueIndex(key => key.SecretHash);
        });
        await _store.InitializeAsync();

        _session = await _store.OpenSessionAsync(new SessionOptions());
        _apiKeyGenerator = Substitute.For<IApiKeyGenerator>();
        _service = new ApiKeyService(
            _session,
            _apiKeyGenerator,
            NullLogger<ApiKeyService>.Instance);
    }

    [After(Test)]
    public async Task TearDown()
    {
        await _session.DisposeAsync();
        await _store.DisposeAsync();
    }

    [Test]
    public async Task Create_user_session_key_persists_only_the_hash_and_validates_the_owner()
    {
        const long userId = 17;
        const string rawApiKey = "sk_live_user-session";

        var returned = await _service.CreateKeyAsync(
            userId,
            "admin@aero.test",
            rawApiKey);

        returned.ShouldBe(rawApiKey);
        var persistedKeys = await _session.Query<ApiKeyDocument>().ToListAsync();
        var persisted = persistedKeys.Single();
        persisted.UserId.ShouldBe(userId);
        persisted.SecretHash.ShouldBe(HashKey(rawApiKey));
        persisted.SecretHash.ShouldNotBe(rawApiKey);
        persisted.CredentialKind.ShouldBe(AeroApiKeyCredentialKind.UserSession);
        persisted.ExpiresAt.ShouldNotBeNull();
        persisted.ExpiresAt!.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        var validation = await _service.ValidateAsync(rawApiKey);
        validation.ShouldNotBeNull();
        validation.UserId.ShouldBe(userId);
        validation.CredentialKind.ShouldBe(AeroApiKeyCredentialKind.UserSession);
        validation.AllowedSiteIds.ShouldBeEmpty();
    }

    [Test]
    public async Task Create_scoped_key_normalizes_and_preserves_its_tenant_site_and_crud_capabilities()
    {
        const string rawApiKey = "sk_test_scoped";
        var secretHash = HashKey(rawApiKey);
        _apiKeyGenerator.Generate(ApiKeyEnvironment.Test)
            .Returns(new GeneratedApiKey("generated", rawApiKey, secretHash));

        var issued = await _service.CreateScopedKeyAsync(
            new CreateScopedApiKeyRequest(
                UserId: 23,
                TenantId: 31,
                AllowedSiteIds: [52, 41, 52],
                Name: "  publishing agent  ",
                IsTest: true,
                McpServer: true,
                IsAdministrator: false,
                Permissions: ["pages:ur", "docs:r"],
                ExpiresAt: DateTimeOffset.UtcNow.AddDays(7),
                CreatedBy: "23"));

        issued.RawApiKey.ShouldBe(rawApiKey);
        var persisted = await _session.LoadAsync<ApiKeyDocument>(issued.KeyId);
        persisted.ShouldNotBeNull();
        persisted.SecretHash.ShouldBe(secretHash);
        persisted.SecretHash.ShouldNotBe(rawApiKey);
        persisted.Name.ShouldBe("publishing agent");
        persisted.TenantId.ShouldBe(31);
        persisted.AllowedSiteIds.ShouldBe([41, 52]);
        persisted.McpServer.ShouldBeTrue();
        persisted.IsAdministrator.ShouldBeFalse();
        persisted.Permissions.ShouldBe(["docs:R", "pages:RU"]);

        var validation = await _service.ValidateAsync(rawApiKey);
        validation.ShouldNotBeNull();
        validation.TenantId.ShouldBe(31);
        validation.AllowedSiteIds.ShouldBe([41, 52]);
        validation.Permissions.ShouldBe(["docs:R", "pages:RU"]);
    }

    [Test]
    public async Task Create_non_admin_mcp_key_without_read_capability_is_rejected()
    {
        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            _service.CreateScopedKeyAsync(
                new CreateScopedApiKeyRequest(
                    UserId: 23,
                    TenantId: 31,
                    AllowedSiteIds: [41],
                    Name: "write only",
                    IsTest: false,
                    McpServer: true,
                    IsAdministrator: false,
                    Permissions: ["pages:C"],
                    ExpiresAt: null,
                    CreatedBy: "23")));

        exception.Message.ShouldContain("read permission");
        (await _session.Query<ApiKeyDocument>().ToListAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task Expired_and_revoked_keys_do_not_validate()
    {
        const string expiredRawKey = "sk_live_expired";
        _session.Store(new ApiKeyDocument
        {
            UserId = 4,
            SecretHash = HashKey(expiredRawKey),
            CredentialKind = AeroApiKeyCredentialKind.UserSession,
            Name = "expired",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedBy = "test"
        });
        await _session.SaveChangesAsync();

        (await _service.ValidateAsync(expiredRawKey)).ShouldBeNull();

        const string activeRawKey = "sk_live_active";
        _apiKeyGenerator.Generate(ApiKeyEnvironment.Live)
            .Returns(new GeneratedApiKey("active", activeRawKey, HashKey(activeRawKey)));
        var issued = await _service.CreateScopedKeyAsync(
            new CreateScopedApiKeyRequest(
                4,
                8,
                [12],
                "revocable",
                false,
                true,
                false,
                ["pages:R"],
                null,
                "4"));

        (await _service.RevokeAsync(issued.KeyId, 4, 8, "4")).ShouldBeTrue();
        (await _service.ValidateAsync(activeRawKey)).ShouldBeNull();
    }

    [Test]
    public async Task Listing_and_revocation_are_constrained_to_owner_and_tenant()
    {
        _apiKeyGenerator.Generate(ApiKeyEnvironment.Live)
            .Returns(
                new GeneratedApiKey("one", "sk_live_one", HashKey("sk_live_one")),
                new GeneratedApiKey("two", "sk_live_two", HashKey("sk_live_two")));

        var first = await _service.CreateScopedKeyAsync(
            new CreateScopedApiKeyRequest(
                9, 101, [1001], "first", false, true, false, ["pages:R"], null, "9"));
        await _service.CreateScopedKeyAsync(
            new CreateScopedApiKeyRequest(
                9, 202, [2002], "second", false, true, false, ["docs:R"], null, "9"));

        var tenantKeys = await _service.ListAsync(9, 101);
        tenantKeys.Count.ShouldBe(1);
        tenantKeys[0].KeyId.ShouldBe(first.KeyId);

        (await _service.RevokeAsync(first.KeyId, 10, 101, "10")).ShouldBeFalse();
        (await _service.RevokeAsync(first.KeyId, 9, 202, "9")).ShouldBeFalse();
        (await _service.RevokeAsync(first.KeyId, 9, 101, "9")).ShouldBeTrue();
    }

    private static string HashKey(string apiKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
