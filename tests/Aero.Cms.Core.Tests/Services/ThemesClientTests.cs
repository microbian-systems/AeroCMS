using TUnit.Core;
using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
namespace Aero.Cms.Core.Tests.Services;

public class ThemesClientTests
{
    private HttpClient _httpClient = null!;
    private MockHttpMessageHandler _handler = null!;
    private ThemesHttpClient _client = null!;

    [Before(Test)]
    public void Setup()
    {
        _handler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost") };
        _client = new ThemesHttpClient(_httpClient, Substitute.For<ILogger<ThemesHttpClient>>());
    }

    [After(Test)]
    public void Cleanup()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }

    [Test]
    public async Task GetAllAsync_Should_Return_Themes()
    {
        // Arrange
        var themes = (IReadOnlyList<ThemeSummary>)new List<ThemeSummary>
        {
            new("theme1", "Theme 1", "1.0.0", "Author 1", null, true),
            new("theme2", "Theme 2", "1.0.0", "Author 2", null, false)
        };
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(themes)
        };

        // Act
        var result = await _client.GetAllAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        if (result is Result<IReadOnlyList<ThemeSummary>, AeroError>.Ok okResult)
        {
            okResult.Value.Should().HaveCount(2);
            okResult.Value[0].Name.Should().Be("Theme 1");
        }
        _handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/v1/admin/themes");
    }

    [Test]
    public async Task GetByIdAsync_Should_Return_Theme_Detail()
    {
        // Arrange
        var theme = new ThemeDetail("theme1", "Theme 1", "1.0.0", "Author 1", "Description 1", null, true, new List<ThemeAsset>(), DateTime.UtcNow);
        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(theme)
        };

        // Act
        var result = await _client.GetByIdAsync("theme1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        if (result is Result<ThemeDetail, AeroError>.Ok okResult)
        {
            okResult.Value.Name.Should().Be("Theme 1");
        }
        _handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/v1/admin/themes/details/theme1");
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