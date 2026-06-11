using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace ProxyTests;

/// <summary>
/// Tests for ChatStreamingService. These cover StreamAndCache (0%),
/// StreamNdjsonPassthrough (0%), and additional branches for StreamOllamaAndCache.
/// </summary>
public class ChatStreamingServiceTests
{
    private readonly ReasoningCacheService _cache = new();
    private readonly ChatStreamingService _sut;

    public ChatStreamingServiceTests()
    {
        _sut = new ChatStreamingService(_cache);
    }

    // ── StreamAndCache ──────────────────────────────────────────────────

    [Fact]
    public async Task StreamAndCache_PassthroughSSE_CopiesData()
    {
        string sseData = "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hi\"},\"finish_reason\":null}]}\n\n" +
                         "data: [DONE]\n\n";
        HttpResponseMessage upstream = CreateStreamResponse(sseData, "text/event-stream");
        using MemoryStream downstream = new();
        HttpResponse downstreamResponse = CreateDownstreamResponse(downstream);

        await _sut.StreamAndCache(upstream, downstreamResponse, CancellationToken.None);

        downstream.Seek(0, SeekOrigin.Begin);
        string result = new StreamReader(downstream).ReadToEnd();
        Assert.Contains("Hi", result);
        Assert.Contains("[DONE]", result);
    }

    [Fact]
    public async Task StreamAndCache_WithReasoningContent_CollectsReasoning()
    {
        string sseData = "data: {\"choices\":[{\"index\":0,\"delta\":{\"reasoning_content\":\"thinking step 1\"},\"finish_reason\":null}]}\n\n" +
                         "data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
                         "data: [DONE]\n\n";
        HttpResponseMessage upstream = CreateStreamResponse(sseData, "text/event-stream");
        using MemoryStream downstream = new();
        HttpResponse downstreamResponse = CreateDownstreamResponse(downstream);

        await _sut.StreamAndCache(upstream, downstreamResponse, CancellationToken.None);

        Assert.True(_cache.TryGet("assistant:0", out string? cached));
        Assert.Equal("thinking step 1", cached);
    }

    [Fact]
    public async Task StreamAndCache_WithToolCalls_DoesNotCacheEmptyReasoning()
    {
        // When finish_reason is hit but no reasoning_content was collected,
        // the cache should NOT be updated (only non-empty reasoning is stored).
        string sseData = "data: {\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"id\":\"call_789\",\"type\":\"function\"}]},\"finish_reason\":null}]}\n\n" +
                         "data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"tool_calls\"}]}\n\n" +
                         "data: [DONE]\n\n";
        HttpResponseMessage upstream = CreateStreamResponse(sseData, "text/event-stream");
        using MemoryStream downstream = new();
        HttpResponse downstreamResponse = CreateDownstreamResponse(downstream);

        await _sut.StreamAndCache(upstream, downstreamResponse, CancellationToken.None);

        // No reasoning content was collected, so cache should not contain this key
        Assert.False(_cache.TryGet("toolcall:call_789", out _));
    }

    [Fact]
    public async Task StreamAndCache_WithReasoningAndToolCalls_CachesWithToolCallKey()
    {
        string sseData = "data: {\"choices\":[{\"index\":0,\"delta\":{\"reasoning_content\":\"tool reasoning\",\"tool_calls\":[{\"id\":\"tc_1\"}]},\"finish_reason\":null}]}\n\n" +
                         "data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"tool_calls\"}]}\n\n" +
                         "data: [DONE]\n\n";
        HttpResponseMessage upstream = CreateStreamResponse(sseData, "text/event-stream");
        using MemoryStream downstream = new();
        HttpResponse downstreamResponse = CreateDownstreamResponse(downstream);

        await _sut.StreamAndCache(upstream, downstreamResponse, CancellationToken.None);

        Assert.True(_cache.TryGet("toolcall:tc_1", out string? cached));
        Assert.Equal("tool reasoning", cached);
    }

    [Fact]
    public async Task StreamAndCache_PassesThroughNonDataLines()
    {
        // StreamAndCache writes through all lines verbatim, including non-data lines
        string sseData = ":keepalive\n" +
                         "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hi\"},\"finish_reason\":null}]}\n\n" +
                         "data: [DONE]\n\n";
        HttpResponseMessage upstream = CreateStreamResponse(sseData, "text/event-stream");
        using MemoryStream downstream = new();
        HttpResponse downstreamResponse = CreateDownstreamResponse(downstream);

        await _sut.StreamAndCache(upstream, downstreamResponse, CancellationToken.None);

        downstream.Seek(0, SeekOrigin.Begin);
        string result = new StreamReader(downstream).ReadToEnd();
        // Non-data lines are written through as-is
        Assert.Contains("keepalive", result);
        Assert.Contains("Hi", result);
    }

    // ── StreamNdjsonPassthrough ─────────────────────────────────────────

    [Fact]
    public async Task StreamNdjsonPassthrough_CopiesNdjsonLines()
    {
        string ndjson = "{\"model\":\"m\",\"message\":{\"content\":\"hi\"},\"done\":false}\n" +
                        "{\"model\":\"m\",\"message\":{\"content\":\"\"},\"done\":true}\n";
        HttpResponseMessage upstream = CreateStreamResponse(ndjson, "application/x-ndjson");
        using MemoryStream downstream = new();
        HttpResponse downstreamResponse = CreateDownstreamResponse(downstream);

        await _sut.StreamNdjsonPassthrough(upstream, downstreamResponse, CancellationToken.None);

        downstream.Seek(0, SeekOrigin.Begin);
        string result = new StreamReader(downstream).ReadToEnd();
        Assert.Equal(ndjson, result);
    }

    [Fact]
    public async Task StreamNdjsonPassthrough_EmptyStream_WritesNothing()
    {
        HttpResponseMessage upstream = CreateStreamResponse("", "application/x-ndjson");
        using MemoryStream downstream = new();
        HttpResponse downstreamResponse = CreateDownstreamResponse(downstream);

        await _sut.StreamNdjsonPassthrough(upstream, downstreamResponse, CancellationToken.None);

        downstream.Seek(0, SeekOrigin.Begin);
        string result = new StreamReader(downstream).ReadToEnd();
        Assert.Equal("", result);
    }

    // ── StreamOllamaAndCache ────────────────────────────────────────────

    [Fact]
    public async Task StreamOllamaAndCache_StreamsNdjsonAndCachesReasoning()
    {
        string sseData = "data: {\"choices\":[{\"index\":0,\"delta\":{\"reasoning_content\":\"step 1\"},\"finish_reason\":null}]}\n" +
                         "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"answer\"},\"finish_reason\":null}]}\n" +
                         "data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n" +
                         "data: [DONE]\n";
        HttpResponseMessage upstream = CreateStreamResponse(sseData, "text/event-stream");
        using MemoryStream downstream = new();
        HttpResponse downstreamResponse = CreateDownstreamResponse(downstream);

        await _sut.StreamOllamaAndCache(upstream, downstreamResponse, "test-model", CancellationToken.None);

        downstream.Seek(0, SeekOrigin.Begin);
        string result = new StreamReader(downstream).ReadToEnd();
        Assert.Contains("test-model", result);
        Assert.Contains("answer", result);
        Assert.Contains("done_reason", result);
        Assert.Contains("true", result);

        Assert.True(_cache.TryGet("assistant:0", out string? cached));
        Assert.Equal("step 1", cached);
    }

    [Fact]
    public async Task StreamOllamaAndCache_SkipsNonDataLines()
    {
        string sseData = "event: ping\n" +
                         "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"ok\"},\"finish_reason\":null}]}\n" +
                         "data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n" +
                         "data: [DONE]\n";
        HttpResponseMessage upstream = CreateStreamResponse(sseData, "text/event-stream");
        using MemoryStream downstream = new();
        HttpResponse downstreamResponse = CreateDownstreamResponse(downstream);

        await _sut.StreamOllamaAndCache(upstream, downstreamResponse, "m", CancellationToken.None);

        downstream.Seek(0, SeekOrigin.Begin);
        string result = new StreamReader(downstream).ReadToEnd();
        Assert.DoesNotContain("event: ping", result);
        Assert.Contains("ok", result);
    }

    [Fact]
    public async Task StreamOllamaAndCache_WithToolCalls_CachesWithToolCallKey()
    {
        string sseData = "data: {\"choices\":[{\"index\":0,\"delta\":{\"reasoning_content\":\"tool reasoning\",\"tool_calls\":[{\"id\":\"tc_2\",\"type\":\"function\",\"function\":{\"name\":\"fn\",\"arguments\":\"{}\"}}]},\"finish_reason\":null}]}\n" +
                         "data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"tool_calls\"}]}\n" +
                         "data: [DONE]\n";
        HttpResponseMessage upstream = CreateStreamResponse(sseData, "text/event-stream");
        using MemoryStream downstream = new();
        HttpResponse downstreamResponse = CreateDownstreamResponse(downstream);

        await _sut.StreamOllamaAndCache(upstream, downstreamResponse, "m", CancellationToken.None);

        Assert.True(_cache.TryGet("toolcall:tc_2", out string? cached));
        Assert.Equal("tool reasoning", cached);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static HttpResponseMessage CreateStreamResponse(string content, string contentType)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StreamContent(stream)
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return response;
    }

    private static HttpResponse CreateDownstreamResponse(Stream bodyStream)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = bodyStream;
        return context.Response;
    }
}