using System.Net;
using System.Net.Http.Json;
using System.Text;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Modules.Ai.Services;
using Aero.Core;
using Aero.Core.Railway;
using FluentAssertions;

namespace Aero.Cms.Modules.Ai.Tests;

public sealed class AiEnhancementStreamingTests
{
    [Test]
    public async Task Json_projector_decodes_fragmented_enhanced_text_without_waiting_for_completion()
    {
        var projector = new StreamingJsonStringProjector("enhancedText");
        var deltas = new[]
        {
            projector.Append("{\"rationale\":null,\"enh"),
            projector.Append("ancedText\":\"Hello\\nA "),
            projector.Append("\\u263"),
            projector.Append("A with an escaped \\\"quote\\\""),
            projector.Append("\",\"warnings\":[]}")
        };

        string.Concat(deltas).Should().Be("Hello\nA ☺ with an escaped \"quote\"");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Sse_parser_handles_fragmented_frames_and_stops_after_complete()
    {
        const string payload = """
            event: metadata
            data: {"kind":0,"correlationId":"trace-1","provider":"Test","model":"model"}

            event: delta
            data: {"kind":1,"text":"Hello ","correlationId":"trace-1"}

            event: delta
            data: {"kind":1,"text":"world","correlationId":"trace-1"}

            event: complete
            data: {"kind":2,"response":{"enhancedText":"Hello world","rationale":null,"warnings":[],"provider":"Test","model":"model","usage":null},"correlationId":"trace-1"}

            event: delta
            data: {"kind":1,"text":"ignored"}

            """;
        await using var stream = new ChunkedReadStream(Encoding.UTF8.GetBytes(payload), 3);

        var events = await CollectAsync(AiEnhancementSseParser.ParseAsync(stream));

        events.Select(item => item.Kind).Should().Equal(
            EnhanceContentEventKind.Metadata,
            EnhanceContentEventKind.Delta,
            EnhanceContentEventKind.Delta,
            EnhanceContentEventKind.Complete);
        string.Concat(events.Where(item => item.Kind == EnhanceContentEventKind.Delta)
            .Select(item => item.Text)).Should().Be("Hello world");
        events[^1].Response!.EnhancedText.Should().Be("Hello world");
    }

    [Test]
    public async Task Http_client_falls_back_to_buffered_enhancement_only_when_stream_is_unavailable()
    {
        var handler = new EnhancementFallbackHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://cms.test/") };
        var client = new AiHttpClient(
            httpClient,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AiHttpClient>.Instance);

        var result = await client.StreamEnhanceContentAsync(Request());
        var stream = result.Should().BeOfType<
            Result<IAsyncEnumerable<EnhanceContentEvent>, AeroError>.Ok>().Subject.Value;
        var events = await CollectAsync(stream);

        handler.RequestCount.Should().Be(2);
        events.Select(item => item.Kind).Should().Equal(
            EnhanceContentEventKind.Metadata,
            EnhanceContentEventKind.Complete);
        events[^1].Response!.EnhancedText.Should().Be("Fallback text");
    }

    private static EnhanceContentRequest Request()
        => new(
            "post",
            "body",
            "Original",
            "Improve this",
            "Title",
            null,
            "title",
            null,
            null);

    private static async Task<List<EnhanceContentEvent>> CollectAsync(
        IAsyncEnumerable<EnhanceContentEvent> source)
    {
        var items = new List<EnhanceContentEvent>();
        await foreach (var item in source)
        {
            items.Add(item);
        }

        return items;
    }

    private sealed class ChunkedReadStream(byte[] bytes, int chunkSize) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
            => base.ReadAsync(buffer, offset, Math.Min(count, chunkSize), cancellationToken);
    }

    private sealed class EnhancementFallbackHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (request.RequestUri!.AbsolutePath.EndsWith("/stream", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new EnhanceContentResponse(
                    "Fallback text",
                    null,
                    [],
                    "Test",
                    "model",
                    null))
            });
        }
    }
}
