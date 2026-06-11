using System.Text.Json;

namespace ProxyTests;

/// <summary>
/// Direct unit tests for the static helper methods in <see cref="OllamaEndpoints"/>.
/// These cover ConvertOllamaToOpenAi, ReplaceModelInOllamaRequestBody, and
/// EnsureOllamaContentFromThinking — which had 0% coverage.
/// </summary>
public class OllamaEndpointConversionsTests
{
    // ── ConvertOllamaToOpenAi ────────────────────────────────────────────

    [Fact]
    public void ConvertOllamaToOpenAi_SimpleRequest_SetsModelAndStream()
    {
        string ollama = """{"model":"deepseek-v4-pro","messages":[{"role":"user","content":"hi"}],"stream":false}""";

        string result = OllamaEndpoints.ConvertOllamaToOpenAi(ollama, "upstream-model", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        JsonElement root = doc.RootElement;
        Assert.Equal("upstream-model", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("user", root.GetProperty("messages")[0].GetProperty("role").GetString());
    }

    [Fact]
    public void ConvertOllamaToOpenAi_WithOptions_ConvertsNumPredictToMaxTokens()
    {
        string ollama = """{"model":"m","messages":[],"options":{"num_predict":4096,"temperature":0.7,"num_ctx":8192}}""";

        string result = OllamaEndpoints.ConvertOllamaToOpenAi(ollama, "up", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        JsonElement root = doc.RootElement;
        Assert.Equal(4096, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0.7, root.GetProperty("temperature").GetDouble());
        // num_ctx is Ollama-specific and should be skipped
        Assert.False(root.TryGetProperty("num_ctx", out _));
    }

    [Fact]
    public void ConvertOllamaToOpenAi_WithMultipleOptions_PreservesCompatibleOptions()
    {
        string ollama = """{"model":"m","messages":[],"options":{"mirostat":1,"mirostat_tau":0.5,"seed":42}}""";

        string result = OllamaEndpoints.ConvertOllamaToOpenAi(ollama, "up", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        // Options that have no OpenAI equivalent should be skipped
        Assert.False(doc.RootElement.TryGetProperty("mirostat", out _));
        Assert.False(doc.RootElement.TryGetProperty("mirostat_tau", out _));
        Assert.False(doc.RootElement.TryGetProperty("seed", out _));
    }

    [Fact]
    public void ConvertOllamaToOpenAi_WithImages_ConvertsToMultiPartContent()
    {
        string ollama = """{"model":"m","messages":[{"role":"user","content":"describe","images":["iVBORw0KGgo="]}],"stream":false}""";

        string result = OllamaEndpoints.ConvertOllamaToOpenAi(ollama, "up", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        JsonElement content = doc.RootElement.GetProperty("messages")[0].GetProperty("content");
        Assert.Equal(JsonValueKind.Array, content.ValueKind);
        Assert.Equal(2, content.GetArrayLength());
        Assert.Equal("text", content[0].GetProperty("type").GetString());
        Assert.Equal("image_url", content[1].GetProperty("type").GetString());
        string url = content[1].GetProperty("image_url").GetProperty("url").GetString()!;
        Assert.StartsWith("data:image/png;base64,", url);
    }

    [Fact]
    public void ConvertOllamaToOpenAi_WithImagesHavingDataPrefix_DoesNotDoublePrefix()
    {
        string ollama = """{"model":"m","messages":[{"role":"user","content":"describe","images":["data:image/jpeg;base64,/9j/4AAQ"]}],"stream":false}""";

        string result = OllamaEndpoints.ConvertOllamaToOpenAi(ollama, "up", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        JsonElement img = doc.RootElement.GetProperty("messages")[0].GetProperty("content")[1];
        string url = img.GetProperty("image_url").GetProperty("url").GetString()!;
        Assert.Equal("data:image/jpeg;base64,/9j/4AAQ", url);
        // Should not be double-prefixed
        Assert.DoesNotContain("data:image/png;base64,data:", url);
    }

    [Fact]
    public void ConvertOllamaToOpenAi_WithImagesHavingHttpPrefix_KeepsUrl()
    {
        string ollama = """{"model":"m","messages":[{"role":"user","content":"describe","images":["https://example.com/img.png"]}],"stream":false}""";

        string result = OllamaEndpoints.ConvertOllamaToOpenAi(ollama, "up", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        JsonElement img = doc.RootElement.GetProperty("messages")[0].GetProperty("content")[1];
        string url = img.GetProperty("image_url").GetProperty("url").GetString()!;
        Assert.Equal("https://example.com/img.png", url);
    }

    [Fact]
    public void ConvertOllamaToOpenAi_NoMessages_OmitsMessagesArray()
    {
        string ollama = """{"model":"m","stream":false}""";

        string result = OllamaEndpoints.ConvertOllamaToOpenAi(ollama, "up", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.TryGetProperty("messages", out _));
    }

    [Fact]
    public void ConvertOllamaToOpenAi_WithTools_IncludesTools()
    {
        string ollama = """{"model":"m","messages":[],"stream":false,"tools":[{"type":"function"}]}""";

        string result = OllamaEndpoints.ConvertOllamaToOpenAi(ollama, "up", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("tools", out _));
    }

    [Fact]
    public void ConvertOllamaToOpenAi_NoOptionsBlock_CopiesTopLevelParams()
    {
        string ollama = """{"model":"m","messages":[],"stream":false,"temperature":0.5,"top_p":0.9,"keep_alive":"5m"}""";

        string result = OllamaEndpoints.ConvertOllamaToOpenAi(ollama, "up", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.Equal(0.5, doc.RootElement.GetProperty("temperature").GetDouble());
        Assert.Equal(0.9, doc.RootElement.GetProperty("top_p").GetDouble());
        // keep_alive is Ollama-specific and should be skipped
        Assert.False(doc.RootElement.TryGetProperty("keep_alive", out _));
        // model, stream, messages should not appear from the top-level copy
        Assert.Equal("up", doc.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public void ConvertOllamaToOpenAi_SkipsOllamaOnlyTopLevelParams()
    {
        string ollama = """{"model":"m","messages":[],"stream":false,"format":"json","raw":true}""";

        string result = OllamaEndpoints.ConvertOllamaToOpenAi(ollama, "up", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.TryGetProperty("format", out _));
        Assert.False(doc.RootElement.TryGetProperty("raw", out _));
    }

    [Fact]
    public void ConvertOllamaToOpenAi_ContentWithoutImages_DoesNotConvertToArray()
    {
        string ollama = """{"model":"m","messages":[{"role":"user","content":"plain text"}],"stream":false}""";

        string result = OllamaEndpoints.ConvertOllamaToOpenAi(ollama, "up", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        JsonElement content = doc.RootElement.GetProperty("messages")[0].GetProperty("content");
        Assert.Equal(JsonValueKind.String, content.ValueKind);
        Assert.Equal("plain text", content.GetString());
    }

    // ── ReplaceModelInOllamaRequestBody ──────────────────────────────────

    [Fact]
    public void ReplaceModelInOllamaRequestBody_WithModel_ReplacesIt()
    {
        string body = """{"model":"old","messages":[]}""";

        string result = OllamaEndpoints.ReplaceModelInOllamaRequestBody(body, "new-model");

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.Equal("new-model", doc.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public void ReplaceModelInOllamaRequestBody_WithoutModel_AddsIt()
    {
        string body = """{"messages":[]}""";

        string result = OllamaEndpoints.ReplaceModelInOllamaRequestBody(body, "added-model");

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.Equal("added-model", doc.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public void ReplaceModelInOllamaRequestBody_InvalidJson_ReturnsOriginal()
    {
        string body = "not-json";

        string result = OllamaEndpoints.ReplaceModelInOllamaRequestBody(body, "model");

        Assert.Equal(body, result);
    }

    [Fact]
    public void ReplaceModelInOllamaRequestBody_PreservesOtherFields()
    {
        string body = """{"model":"old","messages":[],"stream":true,"options":{"temperature":0.5}}""";

        string result = OllamaEndpoints.ReplaceModelInOllamaRequestBody(body, "new");

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.Equal("new", doc.RootElement.GetProperty("model").GetString());
        Assert.True(doc.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal(0.5, doc.RootElement.GetProperty("options").GetProperty("temperature").GetDouble());
    }

    // ── EnsureOllamaContentFromThinking ──────────────────────────────────

    [Fact]
    public void EnsureOllamaContentFromThinking_ContentPresent_ReturnsOriginal()
    {
        string response = """{"model":"m","message":{"role":"assistant","content":"hello"}}""";

        string result = OllamaEndpoints.EnsureOllamaContentFromThinking(response);

        Assert.Equal(response, result);
    }

    [Fact]
    public void EnsureOllamaContentFromThinking_EmptyContentWithThinking_CopiesThinkingToContent()
    {
        string response = """{"model":"m","message":{"role":"assistant","content":"","thinking":"deep reasoning"}}""";

        string result = OllamaEndpoints.EnsureOllamaContentFromThinking(response);

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.Equal("deep reasoning", doc.RootElement.GetProperty("message").GetProperty("content").GetString());
        Assert.True(doc.RootElement.GetProperty("message").TryGetProperty("thinking", out _));
    }

    [Fact]
    public void EnsureOllamaContentFromThinking_MissingContentWithThinking_CopiesThinkingToContent()
    {
        string response = """{"model":"m","message":{"role":"assistant","thinking":"deep reasoning"}}""";

        string result = OllamaEndpoints.EnsureOllamaContentFromThinking(response);

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.Equal("deep reasoning", doc.RootElement.GetProperty("message").GetProperty("content").GetString());
    }

    [Fact]
    public void EnsureOllamaContentFromThinking_NullContentWithThinking_CopiesThinkingToContent()
    {
        string response = """{"model":"m","message":{"role":"assistant","content":null,"thinking":"deep reasoning"}}""";

        string result = OllamaEndpoints.EnsureOllamaContentFromThinking(response);

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.Equal("deep reasoning", doc.RootElement.GetProperty("message").GetProperty("content").GetString());
    }

    [Fact]
    public void EnsureOllamaContentFromThinking_NoMessage_ReturnsOriginal()
    {
        string response = """{"model":"m"}""";

        string result = OllamaEndpoints.EnsureOllamaContentFromThinking(response);

        Assert.Equal(response, result);
    }

    [Fact]
    public void EnsureOllamaContentFromThinking_NoThinking_ReturnsOriginal()
    {
        string response = """{"model":"m","message":{"role":"assistant","content":""}}""";

        string result = OllamaEndpoints.EnsureOllamaContentFromThinking(response);

        Assert.Equal(response, result);
    }

    [Fact]
    public void EnsureOllamaContentFromThinking_EmptyThinking_ReturnsOriginal()
    {
        string response = """{"model":"m","message":{"role":"assistant","content":"","thinking":""}}""";

        string result = OllamaEndpoints.EnsureOllamaContentFromThinking(response);

        using JsonDocument doc = JsonDocument.Parse(result);
        // Should still have empty content because thinking is empty too
        Assert.Equal("", doc.RootElement.GetProperty("message").GetProperty("content").GetString());
    }

    [Fact]
    public void EnsureOllamaContentFromThinking_InvalidJson_ReturnsOriginal()
    {
        string response = "not-json";

        string result = OllamaEndpoints.EnsureOllamaContentFromThinking(response);

        Assert.Equal(response, result);
    }

    [Fact]
    public void EnsureOllamaContentFromThinking_PreservesOtherMessageFields()
    {
        string response = """{"model":"m","message":{"role":"assistant","content":"","thinking":"reasoning","tool_calls":[{"id":"call_1"}]}}""";

        string result = OllamaEndpoints.EnsureOllamaContentFromThinking(response);

        using JsonDocument doc = JsonDocument.Parse(result);
        JsonElement msg = doc.RootElement.GetProperty("message");
        Assert.Equal("reasoning", msg.GetProperty("content").GetString());
        Assert.True(msg.TryGetProperty("tool_calls", out _));
        Assert.Equal("call_1", msg.GetProperty("tool_calls")[0].GetProperty("id").GetString());
    }
}