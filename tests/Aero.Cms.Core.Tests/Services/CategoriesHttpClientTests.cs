using System.Net;
using System.Text.Json;
using Aero.Cms.Core.Http.Clients;
using Aero.Core.Railway;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Services;

public class CategoriesHttpClientTests
{
    [Test]
    public async Task GetAllAsync_ShouldReturnCategories()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger<CategoriesHttpClient>>();
        var categories = new List<CategorySummary>
        {
            new(1, "Test Category", "test-category", 0, null)
        };
        
        var json = JsonSerializer.Serialize(categories);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };

        var handler = new MockHttpMessageHandler(response);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/api/v1/admin/")
        };

        var client = new CategoriesHttpClient(httpClient, mockLogger);

        // Act
        var result = await client.GetAllAsync();

        // Assert
        result.Should().BeOfType<Result<string, IReadOnlyList<CategorySummary>>.Ok>();
        var ok = (Result<string, IReadOnlyList<CategorySummary>>.Ok)result;
        ok.Value.Count.Should().Be(1);
        ok.Value[0].Name.Should().Be("Test Category");
    }

    private class MockHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }
}
