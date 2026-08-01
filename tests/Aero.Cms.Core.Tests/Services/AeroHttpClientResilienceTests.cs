using System.Net;
using Aero.Cms.Abstractions.Http;
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Core.Tests.Services;

public sealed class AeroHttpClientResilienceTests
{
    [Test]
    public async Task Content_type_post_is_not_retried_after_a_server_error()
    {
        var handler = new CountingServerErrorHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAeroHttpClients(new Uri("https://localhost/api/v1/"));
        services.AddHttpClient<IContentTypesHttpClient, ContentTypesHttpClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IContentTypesHttpClient>();

        _ = await client.CreateAsync(new CreateContentTypeRequest(
            "article",
            "Article",
            null,
            null,
            null,
            false,
            false,
            false,
            [],
            null,
            null));

        await Assert.That(handler.Attempts).IsEqualTo(1);
    }

    private sealed class CountingServerErrorHandler : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                RequestMessage = request
            });
        }
    }
}
