using Aero.Cms.Modules.Security;
using Aero.Models.Entities;
using Aero.Auth.Services;
using AeroDB.Sable;
using NSubstitute;
using Microsoft.Extensions.Logging;
using SurrealDb.Embedded.InMemory;
using Shouldly;

namespace Aero.Cms.Core.Tests.Services;

public class ApiKeyServiceTests
{
    private IDocumentStore _store = null!;
    private IDocumentSession _session = null!;
    private IApiKeyFactory _apiKeyFactory = null!;
    private IApiKeyGenerator _apiKeyGenerator = null!;
    private ApiKeyService _service = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _store = Documents.For(o =>
        {
            o.ClientFactory = () => new SurrealDbMemoryClient();
            o.Namespace = "test";
            o.Database = Guid.NewGuid().ToString();
            o.Schema.For<ApiAccountModel>();
        });
        await _store.InitializeAsync();

        var factory = new LoggerFactory();
        var log = factory.CreateLogger<ApiKeyService>();

        _session = await _store.OpenSessionAsync(new SessionOptions());
        _apiKeyFactory = Substitute.For<IApiKeyFactory>();
        _apiKeyGenerator = Substitute.For<IApiKeyGenerator>();
        _service = new ApiKeyService(_session, _apiKeyFactory, _apiKeyGenerator, log);
    }

    [After(Test)]
    public async Task TearDown()
    {
        if (_session != null)
        {
            await _session.DisposeAsync();
        }
        if (_store != null)
        {
            await _store.DisposeAsync();
        }
    }

    [Test]
    public async Task CreateKeyAsync_Should_Create_And_Store_Hashed_Key()
    {
        // Arrange
        long userId = 1;
        string email = "admin@aero.com";
        string apiKey = "test-api-key";
        var expectedHash = HashKey(apiKey);

        // Act
        var result = await _service.CreateKeyAsync(userId, email, apiKey);

        // Assert
        result.ShouldBe(apiKey);
        var account = await _session.Query<ApiAccountModel>().FirstOrDefaultAsync(x => x.Id == userId);
        account.ShouldNotBeNull();
        account.ApiKey.ShouldBe(expectedHash);
        account.Email.ShouldBe(email);
        account.Enabled.ShouldBeTrue();
    }

    [Test]
    public async Task CreateKeyAsync_With_Generated_Key_Should_Work()
    {
        // Arrange
        long userId = 2;
        string email = "user@aero.com";
        string generatedKey = "sk_live_abc123";
        string generatedHash = HashKey(generatedKey);
        
        _apiKeyGenerator.Generate(ApiKeyEnvironment.Live)
            .Returns(new GeneratedApiKey("abc123", generatedKey, generatedHash));

        // Act
        var result = await _service.CreateKeyAsync(userId, email, null);

        // Assert
        result.ShouldBe(generatedKey);
        var account = await _session.Query<ApiAccountModel>().FirstOrDefaultAsync(x => x.Id == userId);
        account.ShouldNotBeNull();
        account.ApiKey.ShouldBe(generatedHash);
    }

    [Test]
    public async Task ValidateAsync_With_Valid_Key_Should_Return_UserId()
    {
        // Arrange
        long userId = 3;
        string apiKey = "valid-key";
        string hash = HashKey(apiKey);
        
        _session.Store(new ApiAccountModel
        {
            Id = userId,
            ApiKey = hash,
            Email = "test@test.com",
            Enabled = true,
            CreatedBy = "test",
            ModifiedBy = "test",
            RefreshToken = "token",
            RefreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(1)
        });
        await _session.SaveChangesAsync();

        // Act
        var result = await _service.ValidateAsync(apiKey);

        // Assert
        result.ShouldBe(userId);
    }

    private static string HashKey(string apiKey)
    {
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    [Test]
    public async Task ValidateAsync_With_Invalid_Key_Should_Return_Null()
    {
        // Arrange
        string apiKey = "invalid-key";

        // Act
        var result = await _service.ValidateAsync(apiKey);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task ValidateAsync_With_Disabled_Account_Should_Return_Null()
    {
        // Arrange
        long userId = 4;
        string apiKey = "disabled-key";
        string hash = HashKey(apiKey);

        _session.Store(new ApiAccountModel
        {
            Id = userId,
            ApiKey = hash,
            Email = "disabled@test.com",
            Enabled = false,
            CreatedBy = "test",
            ModifiedBy = "test",
            RefreshToken = "token",
            RefreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(1)
        });
        await _session.SaveChangesAsync();

        // Act
        var result = await _service.ValidateAsync(apiKey);

        // Assert
        result.ShouldBeNull();
    }
}
