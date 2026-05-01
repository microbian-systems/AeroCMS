using TUnit.Core;
using Aero.Cms.Abstractions.Services;
using Aero.Cms.Modules.Security;
using Aero.EfCore;
using Aero.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Aero.Auth.Services;
using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
namespace Aero.Cms.Core.Tests.Services;

public class ApiKeyServiceTests
{
    private AeroDbContext _dbContext = null!;
    private IApiKeyFactory _apiKeyFactory = null!;
    private IApiKeyGenerator _apiKeyGenerator = null!;
    private ApiKeyService _service = null!;

    [Before(Test)]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<AeroDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var factory = new LoggerFactory();
        var log = factory.CreateLogger<ApiKeyService>();

        _dbContext = new AeroDbContext(options);
        _apiKeyFactory = Substitute.For<IApiKeyFactory>();
        _apiKeyGenerator = Substitute.For<IApiKeyGenerator>();
        _service = new ApiKeyService(_dbContext, _apiKeyFactory, _apiKeyGenerator, log);
    }

    [After(Test)]
    public async Task TearDown()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
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
        result.Should().Be(apiKey);
        var account = await _dbContext.ApiAccounts.FirstOrDefaultAsync(x => x.Id == userId);
        account.Should().NotBeNull();
        account!.ApiKey.Should().Be(expectedHash);
        account.Email.Should().Be(email);
        account.Enabled.Should().BeTrue();
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
        result.Should().Be(generatedKey);
        var account = await _dbContext.ApiAccounts.FirstOrDefaultAsync(x => x.Id == userId);
        account.Should().NotBeNull();
        account!.ApiKey.Should().Be(generatedHash);
    }

    [Test]
    public async Task ValidateAsync_With_Valid_Key_Should_Return_UserId()
    {
        // Arrange
        long userId = 3;
        string apiKey = "valid-key";
        string hash = HashKey(apiKey);
        
        _dbContext.ApiAccounts.Add(new ApiAccountModel
        {
            Id = userId,
            ApiKey = hash, // Store hash
            Email = "test@test.com",
            Enabled = true,
            RefreshToken = "token",
            RefreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(1)
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ValidateAsync(apiKey);

        // Assert
        result.Should().Be(userId);
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
        result.Should().BeNull();
    }

    [Test]
    public async Task ValidateAsync_With_Disabled_Account_Should_Return_Null()
    {
        // Arrange
        long userId = 4;
        string apiKey = "disabled-key";
        _dbContext.ApiAccounts.Add(new ApiAccountModel
        {
            Id = userId,
            ApiKey = apiKey,
            Email = "disabled@test.com",
            Enabled = false,
            RefreshToken = "token",
            RefreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(1)
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ValidateAsync(apiKey);

        // Assert
        result.Should().BeNull();
    }
}