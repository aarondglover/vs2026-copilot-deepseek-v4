using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace ProxyTests;

/// <summary>
/// Direct unit tests for the static helper methods in <see cref="OpenAiEndpoints"/>.
/// These cover BuildOllamaChatRequest, ConvertOllamaChatToOpenAiCompletion, and
/// ExtractProviderHint — which had 0% coverage.
/// </summary>
public class OpenAiEndpointHelpersTests
{
    // ── BuildOllamaChatRequest ─────────────────────────────────────────────

    [Fact]
    public void BuildOllamaChatRequest_SimpleRequest_SetsModelAndStream()
    {
        string openAi = """{"model":"gpt-4","messages":[{"role":"user","content":"hi"}],"stream":false}""";

        string result = OpenAiEndpoints.BuildOllamaChatRequest(openAi, "ollama-model", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        JsonElement root = doc.RootElement;
        Assert.Equal("ollama-model", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.True(root.TryGetProperty("messages", out _));
    }

    [Fact]
    public void BuildOllamaChatRequest_WithTools_IncludesTools()
    {
        string openAi = """{"model":"m","messages":[],"tools":[{"type":"function","function":{"name":"ping"}}],"stream":false}""";

        string result = OpenAiEndpoints.BuildOllamaChatRequest(openAi, "m", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("tools", out _));
    }

    [Fact]
    public void BuildOllamaChatRequest_WithTemperatureTopPMaxTokens_MapsToOptions()
    {
        string openAi = """{"model":"m","messages":[],"temperature":0.7,"top_p":0.9,"max_tokens":4096,"stream":false}""";

        string result = OpenAiEndpoints.BuildOllamaChatRequest(openAi, "m", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        JsonElement options = doc.RootElement.GetProperty("options");
        Assert.Equal(0.7, options.GetProperty("temperature").GetDouble());
        Assert.Equal(0.9, options.GetProperty("top_p").GetDouble());
        Assert.Equal(4096, options.GetProperty("num_predict").GetInt32());
    }

    [Fact]
    public void BuildOllamaChatRequest_WithPartialOptions_OnlyIncludesPresent()
    {
        string openAi = """{"model":"m","messages":[],"max_tokens":2048,"stream":false}""";

        string result = OpenAiEndpoints.BuildOllamaChatRequest(openAi, "m", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        JsonElement options = doc.RootElement.GetProperty("options");
        Assert.Equal(2048, options.GetProperty("num_predict").GetInt32());
        Assert.False(options.TryGetProperty("temperature", out _));
        Assert.False(options.TryGetProperty("top_p", out _));
    }

    [Fact]
    public void BuildOllamaChatRequest_NoExtraParams_NoOptionsBlock()
    {
        string openAi = """{"model":"m","messages":[],"stream":false}""";

        string result = OpenAiEndpoints.BuildOllamaChatRequest(openAi, "m", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.TryGetProperty("options", out _));
    }

    [Fact]
    public void BuildOllamaChatRequest_NoMessages_OmitsMessages()
    {
        string openAi = """{"model":"m","stream":false}""";

        string result = OpenAiEndpoints.BuildOllamaChatRequest(openAi, "m", false);

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.TryGetProperty("messages", out _));
    }

    // ── ConvertOllamaChatToOpenAiCompletion ───────────────────────────────

    [Fact]
    public void ConvertOllamaChatToOpenAiCompletion_SimpleResponse_ReturnsOpenAiFormat()
    {
        string ollama = """{"model":"m","message":{"role":"assistant","content":"hello"},"done":true,"done_reason":"stop"}""";

        string result = OpenAiEndpoints.ConvertOllamaChatToOpenAiCompletion(ollama, "effective-model");

        using JsonDocument doc = JsonDocument.Parse(result);
        JsonElement root = doc.RootElement;
        Assert.Equal("chat.completion", root.GetProperty("object").GetString());
        Assert.Equal("effective-model", root.GetProperty("model").GetString());
        Assert.Equal("assistant", root.GetProperty("choices")[0].GetProperty("message").GetProperty("role").GetString());
        Assert.Equal("hello", root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString());
        Assert.Equal("stop", root.GetProperty("choices")[0].GetProperty("finish_reason").GetString());
    }

    [Fact]
    public void ConvertOllamaChatToOpenAiCompletion_EmptyContentWithThinking_FallsBackToThinking()
    {
        string ollama = """{"model":"m","message":{"role":"assistant","content":"","thinking":"reasoning text"},"done":true}""";

        string result = OpenAiEndpoints.ConvertOllamaChatToOpenAiCompletion(ollama, "m");

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.Equal("reasoning text", doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString());
    }

    [Fact]
    public void ConvertOllamaChatToOpenAiCompletion_MissingContentWithThinking_FallsBackToThinking()
    {
        string ollama = """{"model":"m","message":{"role":"assistant","thinking":"reasoning"},"done":true}""";

        string result = OpenAiEndpoints.ConvertOllamaChatToOpenAiCompletion(ollama, "m");

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.Equal("reasoning", doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString());
    }

    [Fact]
    public void ConvertOllamaChatToOpenAiCompletion_ContentPresent_DoesNotUseThinking()
    {
        string ollama = """{"model":"m","message":{"role":"assistant","content":"visible","thinking":"hidden"},"done":true}""";

        string result = OpenAiEndpoints.ConvertOllamaChatToOpenAiCompletion(ollama, "m");

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.Equal("visible", doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString());
    }

    [Fact]
    public void ConvertOllamaChatToOpenAiCompletion_WithToolCalls_IncludesToolCalls()
    {
        string ollama = """{"model":"m","message":{"role":"assistant","content":"","tool_calls":[{"id":"call_1","type":"function"}]},"done":true}""";

        string result = OpenAiEndpoints.ConvertOllamaChatToOpenAiCompletion(ollama, "m");

        using JsonDocument doc = JsonDocument.Parse(result);
        JsonElement msg = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
        Assert.True(msg.TryGetProperty("tool_calls", out JsonElement tcs));
        Assert.Equal("call_1", tcs[0].GetProperty("id").GetString());
    }

    [Fact]
    public void ConvertOllamaChatToOpenAiCompletion_NoDoneReason_DefaultsToStop()
    {
        string ollama = """{"model":"m","message":{"role":"assistant","content":"hi"},"done":true}""";

        string result = OpenAiEndpoints.ConvertOllamaChatToOpenAiCompletion(ollama, "m");

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.Equal("stop", doc.RootElement.GetProperty("choices")[0].GetProperty("finish_reason").GetString());
    }

    [Fact]
    public void ConvertOllamaChatToOpenAiCompletion_NoMessage_EmptyContent()
    {
        string ollama = """{"model":"m","done":true}""";

        string result = OpenAiEndpoints.ConvertOllamaChatToOpenAiCompletion(ollama, "m");

        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.Equal("", doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString());
    }

    // ── ExtractProviderHint ──────────────────────────────────────────────

    [Fact]
    public void ExtractProviderHint_NullModel_ReturnsNull()
    {
        var registry = CreateRegistryWithProviders();

        var result = OpenAiEndpoints.ExtractProviderHint(null, registry);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractProviderHint_EmptyModel_ReturnsNull()
    {
        var registry = CreateRegistryWithProviders();

        var result = OpenAiEndpoints.ExtractProviderHint("", registry);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractProviderHint_NoSlash_ReturnsNull()
    {
        var registry = CreateRegistryWithProviders();

        var result = OpenAiEndpoints.ExtractProviderHint("model-only", registry);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractProviderHint_SlashAtEnd_ReturnsNull()
    {
        var registry = CreateRegistryWithProviders();

        var result = OpenAiEndpoints.ExtractProviderHint("provider/", registry);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractProviderHint_WithValidProviderHint_ReturnsProvider()
    {
        var registry = CreateRegistryWithProviders();

        ProviderInfo? result = OpenAiEndpoints.ExtractProviderHint("deepseek/deepseek-v4-pro", registry);

        Assert.NotNull(result);
        Assert.Equal("deepseek", result.Value.Name);
    }

    [Fact]
    public void ExtractProviderHint_WithUnknownProvider_ReturnsNull()
    {
        var registry = CreateRegistryWithProviders();

        var result = OpenAiEndpoints.ExtractProviderHint("unknown/model", registry);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractProviderHint_CaseInsensitiveMatch_ReturnsProvider()
    {
        var registry = CreateRegistryWithProviders();

        ProviderInfo? result = OpenAiEndpoints.ExtractProviderHint("DeepSeek/deepseek-v4-pro", registry);

        Assert.NotNull(result);
        Assert.Equal("deepseek", result.Value.Name);
    }

    private static ProviderRegistry CreateRegistryWithProviders()
    {
        Environment.SetEnvironmentVariable("PROVIDER_DEEPSEEK_API_KEY", "test-key");
        Environment.SetEnvironmentVariable("PROVIDER_DEEPSEEK_BASE_URL", "http://localhost");

        ProviderHttpClientFactory factory = new();
        return new ProviderRegistry(factory);
    }
}