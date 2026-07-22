using System.Net;
using System.Text;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Services;

public class SitesClientTests
{
    [Test]
    public async Task UpdateThemeAsync_MapsHttpConflictToDomainConflict()
    {
        using var handler = new ConflictHttpMessageHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
        var client = new SitesHttpClient(
            httpClient,
            Substitute.For<ILogger<SitesHttpClient>>());

        var result = await client.UpdateThemeAsync(
            42,
            new UpdateSiteThemeRequest(3, "aero-safe", "1.0.0"));

        var failure = result as Result<SiteThemeSelectionViewModel, AeroError>.Failure;
        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Error).IsTypeOf<AeroError.Conflict>();
        await Assert.That(handler.LastRequest?.Method).IsEqualTo(HttpMethod.Put);
        await Assert.That(handler.LastRequest?.RequestUri?.PathAndQuery)
            .IsEqualTo("/api/v1/admin/sites/42/theme");
    }

    private sealed class ConflictHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    "The site theme changed concurrently.",
                    Encoding.UTF8,
                    "text/plain")
            });
        }
    }
}
