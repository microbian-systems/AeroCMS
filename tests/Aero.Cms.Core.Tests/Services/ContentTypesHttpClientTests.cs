using System.Net;
using System.Net.Http.Json;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aero.Cms.Core.Tests.Services;

public sealed class ContentTypesHttpClientTests
{
    [Test]
    public async Task Reference_options_uses_target_content_type_id_in_the_path()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://aero.test/") };
        var client = new ContentItemsHttpClient(httpClient, NullLogger<ContentItemsHttpClient>.Instance);

        await client.GetReferenceOptionsAsync(123456789, search: "wolf");

        await Assert.That(handler.RequestUri!.PathAndQuery)
            .StartsWith("/api/v1/admin/content-items/reference-options/123456789?");
    }
    [Test]
    public async Task Create_surfaces_problem_detail_as_a_validation_error()
    {
        using var handler = new ProblemDetailsHandler(
            "Reference field 'Species' must select a target content type.");
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://aero.test/")
        };
        var client = new ContentTypesHttpClient(
            httpClient,
            NullLogger<ContentTypesHttpClient>.Instance);

        var result = await client.CreateAsync(
            new CreateContentTypeRequest(
                "animal",
                "Animal",
                null,
                null,
                null,
                false,
                true,
                false,
                [],
                null,
                null));

        var failure = result as Result<ContentTypeDetail, AeroError>.Failure;
        await Assert.That(failure).IsNotNull();
        var validation = failure!.Error as AeroError.Validation;
        await Assert.That(validation).IsNotNull();
        await Assert.That(validation!.Errors).Contains(
            "Reference field 'Species' must select a target content type.");
    }

    private sealed class ProblemDetailsHandler(string detail)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    RequestMessage = request,
                    Content = JsonContent.Create(
                        new ProblemDetails
                        {
                            Title = "Failed to create content type",
                            Detail = detail,
                            Status = (int)HttpStatusCode.BadRequest
                        })
                });
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<ContentReferenceOption>()) });
        }
    }
}
