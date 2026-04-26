using TUnit.Core;
using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Core.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
namespace Aero.Cms.Core.Tests.Services;

public class SettingsHttpClientTests
{
    private HttpClient _httpClient = null!;
    private MockHttpMessageHandler _handler = null!;
    private SettingsHttpClient _client = null!;

    [Before(Test)]
    public void Setup()
    {
        _handler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost") };
        _client = new SettingsHttpClient(_httpClient, Substitute.For<ILogger<SettingsHttpClient>>());
    }

    [After(Test)]
    public void Cleanup()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }

    [Test]
    public async Task GetAllAsync_Should_Return_Settings()
    {
        // Arrange
        var settings = (IReadOnlyList<SettingSummary>)new List<SettingSummary>
        {
            new("Key1", "Cat1", "Desc1"),
            new("Key2", "Cat2", "Desc2")
        };
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(settings)
        };

        // Act
        var result = await _client.GetAllAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        if (result is Result<IReadOnlyList<SettingSummary>, AeroError>.Ok okResult)
        {
            okResult.Value.Should().HaveCount(2);
            okResult.Value[0].Key.Should().Be("Key1");
        }
        _handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/v1/admin/settings");
    }

    [Test]
    public async Task GetByKeyAsync_Should_Return_Setting_Detail()
    {
        // Arrange
        var setting = new SettingDetail("Key1", "Val1", "Cat1", "Desc1", "string", DateTime.UtcNow);
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(setting)
        };

        // Act
        var result = await _client.GetByKeyAsync("Key1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        if (result is Result<SettingDetail, AeroError>.Ok okResult)
        {
            okResult.Value.Key.Should().Be("Key1");
        }
        _handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/v1/admin/settings/key/Key1");
    }

    [Test]
    public async Task SetAsync_Should_Post_And_Return_Detail()
    {
        // Arrange
        var request = new SetSettingRequest("Key1", "Val1", "Cat1", "string");
        var response = new SettingDetail("Key1", "Val1", "Cat1", null, "string", DateTime.UtcNow);
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(response)
        };

        // Act
        var result = await _client.SetAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/v1/admin/settings");
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }
    }
}