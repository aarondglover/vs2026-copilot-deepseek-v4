# CLAUDE.md — AI Assistant Session Memory

This file provides guidance to Claude Code, GitHub Copilot, Cursor, and other AI code assistants working with this repository.

## Hard Constraints (Never Violate)

### Branching Rule
- **Never** touch `main`. It is the protected release branch.
- **All work happens on `develop`**. Feature branches branch off `develop` and merge back into `develop`.
- **Never** push directly to `develop` — always use a feature branch and PR.

### Workflow: Feature Branches

```
main          ─── (protected, release only)
develop       ─── merge ← feature/* branches only
feature/xxx   ──┬─ branch off develop, PR back into develop
```

**Steps for every contribution:**
1. `git fetch origin && git checkout develop && git pull origin develop`
2. `git checkout -b feature/<short-description>`
3. Make changes, commit with Conventional Commits
4. `git push origin feature/<short-description>`
5. Create a PR into `develop` (GitHub UI or `gh pr create`)
6. After merge, delete the branch
7. Never merge `main` into `develop` (main is behind)

### Conventional Commits (Required)
Every merge commit message must follow:
```
<type>(<scope>): <description>

[optional body]
```
Types: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`, `perf`, `ci`
Scopes: `deepseek`, `openai`, `nvidia`, `groq`, `openrouter`, `moonshot`, `cerebras`, `ollama`, `config`, `test`, `docs`, `infra`, `script`

Examples:
```
feat(deepseek): add deepseek-coder-6.7b-instruct model config
fix(config): correct nvidia nemotron-3-super max_output_tokens
test(moonshot): add override_client_params kimi-k2.5 tests
docs(infra): update architecture diagram for ollamacloud
```

### Never Confuse Credentials
- **Cloud provider keys** (`PROVIDER_DEEPSEEK_API_KEY`, `PROVIDER_NVIDIA_API_KEY`, etc.) go in `.env` — never in code.
- **Proxy API key** (`PROXY_API_KEY`) is optional and unrelated to upstream keys.
- `.env` is git-ignored. Only `.env.example` is tracked.

---

## Build & Test

```bash
# Build
dotnet build

# Run all tests (342 tests, xUnit + WebApplicationFactory)
dotnet test

# Run specific test suite by class name
dotnet test --filter "ClassName=EndpointTests"
dotnet test --filter "ClassName=ParameterValidationTests"
dotnet test --filter "ClassName=ModelSelectionStoreTests"
dotnet test --filter "ClassName=OverrideClientParamsTests"
dotnet test --filter "ClassName=ProviderModelHintTests"
dotnet test --filter "ClassName=RequestTransformerTests"

# Run single test by method name
dotnet test --filter "TestMethodName=MySpecificTest"

# Run with detailed output
dotnet test --verbosity detailed

# Run the proxy locally (port 11434 default)
dotnet run
```

Tests live in `tests/ProxyTests/`. The project targets **.NET 10.0** and uses `WebApplication.CreateSlimBuilder()`.

---

## What This Is

A high-performance ASP.NET Core **minimal API proxy** that bridges GitHub Copilot, Cursor, Continue.dev, Visual Studio BYOM, and Ollama clients to **8 AI providers** through two API surfaces:

| API Surface | URL Prefix | Used By |
|---|---|---|
| OpenAI-compatible | `/v1/*` | Copilot, Cursor, Continue.dev, OpenAI SDKs |
| Ollama-compatible | `/api/*` | VS 2026 BYOM, native Ollama clients |

**Providers (8):** DeepSeek, OpenAI, NVIDIA NIM, Groq, OpenRouter, Ollama Cloud, Moonshot/Kimi, Cerebras.

**Primary use case:** GitHub Copilot inside Visual Studio 2026 producing code completions and code chat. All curated model configs are optimised for this workload.

---

## Architecture

### Service Registration (all Singletons)

Every service is registered as a **singleton** in `Program.cs`. The entire DI graph:

```
ProviderHttpClientFactory  →  Creates/caches per-provider HttpClient with auth headers
ProviderRegistry           →  Resolves model name → ordered list of provider candidates;
                              ResolveModel() does 3-level "provider/model" hint resolution
ModelSelectionStore        →  Loads/parses config/model-selection/*.json (incl. override_client_params)
ModelCatalogService        →  Fetches live model catalogs from all providers on startup;
                              resolves cross-provider collisions by (priority asc, provider order asc)
ReasoningCacheService      →  Caches DeepSeek reasoning_content for multi-turn conversations
RequestTransformer         →  Injects defaults + filters unsupported params per provider;
                              honours override_client_params=true force-mode
OllamaResponseBuilder      →  Converts OpenAI JSON response → Ollama NDJSON format
ChatStreamingService       →  Handles SSE streaming + on-the-fly format conversion
ProviderBenchmarkService   →  Background HostedService monitoring provider health
```

### Endpoint Structure

- `Endpoints/OpenAiEndpoints.cs` — Maps `/v1/models`, `/v1/chat/completions`
- `Endpoints/OllamaEndpoints.cs` — Maps `/api/version`, `/api/tags`, `/api/show`, `/api/chat`
- `Endpoints/HealthEndpoints.cs` — Maps `/health`
- Middleware lives in `Infrastructure/ProxyAuthenticationMiddleware.cs`

### Request Lifecycle

1. **Request arrives** → endpoint handler parses model name
2. **Model validated** → `ModelCatalogService.AvailableModels` (populated at startup)
3. **Defaults injected** → `RequestTransformer.ApplyExecutionDefaults()` reads config from `ModelSelectionStore` and injects temperature, max_tokens, reasoning_effort, etc. for the requested model
4. **Provider resolved** → `ProviderRegistry.ResolveCandidates(model)` returns ordered list of providers to try
5. **Forward to upstream** → via `ChatStreamingService` (streaming) or direct HTTP (non-streaming)
6. **Response converted** → if Ollama endpoint, `OllamaResponseBuilder` maps OpenAI → Ollama format
7. **Failover** → non-streaming requests retry next candidate on failure; streaming does NOT failover (headers already sent)

---

## Model Configuration

Model metadata lives in `config/model-selection/{provider}.json` (8 files). Each file maps model names to execution defaults:

```json
{
  "provider": "deepseek",
  "models": [
    {
      "match": "deepseek-v4-pro",
      "priority": 1,
      "enabled": true,
      "execution": {
        "context_length": 1048576,
        "max_output_tokens": 384000,
        "temperature": 0.2,
        "max_tokens": 8192,
        "reasoning_effort": "high",
        "timeout_seconds": 180
      }
    }
  ]
}
```

- **Adding a new model:** edit the JSON for its provider + restart (no hot reload)
- **Adding a new provider:** create JSON + add provider to `ProviderRegistry.DiscoverProviders` + add HttpClient factory in `ProviderHttpClientFactory.cs`
- Models with `"enabled": false` are excluded from `/v1/models` and `/api/tags`
- The `execution.override_client_params` flag (bool, default `false`) controls force-mode: when `true`, the proxy overwrites client-supplied `temperature` / `top_p` / `max_tokens` / `reasoning_effort` with the configured value

### Current Enabled Models (2026-06-11)

Each provider exposes 5 enabled models maximum (DeepSeek exposes 2 enabled + 1 disabled, Cerebras exposes 2):

| Provider | Enabled Models |
|---|---|
| **DeepSeek** (2 enabled) | `deepseek-v4-pro`, `deepseek-coder-6.7b-instruct` |
| **OpenAI** (5) | `gpt-5`, `gpt-5-mini`, `gpt-4.1`, `gpt-4o`, `gpt-oss-120b` |
| **NVIDIA NIM** (5) | `qwen/qwen3-coder-480b-a35b-instruct`, `moonshotai/kimi-k2.6`, `nvidia/nemotron-3-super-120b-a12b`, `openai/gpt-oss-120b`, `qwen/qwen3.5-397b-a17b` |
| **Groq** (5) | `llama-3.3-70b-versatile`, `qwen/qwen3-32b`, `meta-llama/llama-4-scout-17b-16e-instruct`, `openai/gpt-oss-120b`, `openai/gpt-oss-20b` |
| **OpenRouter** (5) | `qwen/qwen3-coder`, `nvidia/nemotron-3-super-120b-a12b`, `nvidia/nemotron-3-ultra-550b-a55b`, `moonshotai/kimi-k2.6`, `deepseek/deepseek-v4-pro` |
| **Moonshot/Kimi** (5) | `kimi-k2.6`, `kimi-k2.5`, `moonshot-v1-128k`, `moonshot-v1-auto`, `moonshot-v1-32k` |
| **Cerebras** (2) | `zai-glm-4.7`, `gpt-oss-120b` |
| **Ollama Cloud** (5) | `qwen3-coder:480b`, `qwen3-coder-next`, `devstral-2:123b`, `kimi-k2.6`, `deepseek-v4-pro` |
| **Local Ollama** (1) | `mistral` (bare substring match) |

### Parameter Filtering Rules

`RequestTransformer.ApplyExecutionDefaults()` strips unsupported parameters per provider:

- `top_k` → removed for DeepSeek, OpenAI, Moonshot/Kimi; kept for NVIDIA, Groq, OpenRouter
- `reasoning_effort` → only DeepSeek and OpenAI o-series; removed for NVIDIA, Groq, Moonshot/Kimi
- `top_p` → omitted when `reasoning_effort` is set (DeepSeek API rule)
- `tools`/`tool_choice` → kept for most; removed for Groq
- `function_call` → removed for all (deprecated)
- `override_client_params=true` → force-overwrite client values with configured ones

### Force-Mode (override_client_params)

Currently active for:
- Moonshot `kimi-k2.6` and `kimi-k2.5` — temperature=1.0 forced (Kimi K2.x rejects anything else)
- Ollama Cloud `kimi-k2.6` — inherits the same force-mode rule

---

## Key Files Reference

| File | Purpose |
|---|---|
| `Program.cs` | Entry point, DI registration, endpoint mapping, env-var discovery |
| `Services/ProviderRegistry.cs` | Model → provider resolution; 3-level `provider/model` hint resolver; `ResolveCandidates` |
| `Services/RequestTransformer.cs` | Parameter filtering + default injection; `override_client_params` force-mode |
| `Services/ModelCatalogService.cs` | Live model catalog from all providers; cross-provider collision resolution |
| `Services/ModelSelectionStore.cs` | JSON config loader for model defaults; parses `override_client_params` |
| `Services/ChatStreamingService.cs` | SSE/NDJSON streaming handler |
| `Services/ProviderHttpClientFactory.cs` | HttpClient creation with auth headers |
| `Models/ModelExecutionConfig.cs` | record struct with `OverrideClientParams` field |
| `Models/ProviderInfo.cs` | record struct `(Name, ApiKey, BaseUrl, Client)` |
| `Infrastructure/ProxyAuthenticationMiddleware.cs` | Optional bearer token auth |
| `config/model-selection/` | Per-provider model JSON configs (8 files) |

Further detail is available in `docs/ARCHITECTURE.md`, `docs/AGENTS.md`, `docs/API.md`, `docs/CONFIGURATION.md`, `docs/TESTING.md`, and `docs/DEPLOYMENT.md`.