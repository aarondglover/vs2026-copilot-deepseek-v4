# Provider connectivity status

Generated UTC: 2026-06-11T16:19:57.2776795Z

## Provider summary

| Provider | Catalog | Catalog models | Configured models | Chat OK | Avg latency |
|---|---:|---:|---:|---:|---:|
| cerebras | âœ… ok | 2 | 2 | 1/2 | 324ms |
| deepseek | âœ… ok | 2 | 3 | 2/3 | 1546ms |
| groq | âœ… ok | 16 | 5 | 5/5 | 331ms |
| moonshot | âœ… ok | 9 | 5 | 3/5 | 693ms |
| nvidia | âœ… ok | 120 | 5 | 4/5 | 3011ms |
| ollama | âœ… ok | 41 | 6 | 6/6 | 4654ms |
| openai | âšª missing_key | 0 | 5 | 0/5 | 0ms |
| openrouter | âœ… ok | 337 | 5 | 5/5 | 3774ms |

## Configured model checks

| Provider | Configured model | Catalog/chat model | Catalog | Chat | Latency | Sample/Error |
|---|---|---|---:|---:|---:|---|
| cerebras | `gpt-oss-120b` | `gpt-oss-120b` | ok | âŒ http_429 | 0ms | Error en el servidor remoto: (429) Too Many Requests. |
| cerebras | `zai-glm-4.7` | `zai-glm-4.7` | ok | âœ… ok | 324ms | 1.  **Analyze the Request:** The user wants me to reply with exactly the word "OK".  2.  **Identify the Constraint:** The |
| deepseek | `deepseek-coder-6.7b-instruct` | `deepseek-coder-6.7b-instruct` | ok | âŒ http_400 | 0ms | Error en el servidor remoto: (400) Solicitud incorrecta. |
| deepseek | `deepseek-v4-flash` | `deepseek-v4-flash` | ok | âœ… ok | 1150ms | OK |
| deepseek | `deepseek-v4-pro` | `deepseek-v4-pro` | ok | âœ… ok | 1943ms | We are asked: "Reply exactly OK". The instruction is clear: I must reply with exactly "OK". No additional text, no explanations. Just "OK |
| groq | `llama-3.3-70b-versatile` | `llama-3.3-70b-versatile` | ok | âœ… ok | 251ms | OK |
| groq | `meta-llama/llama-4-scout-17b-16e-instruct` | `meta-llama/llama-4-scout-17b-16e-instruct` | ok | âœ… ok | 315ms | OK |
| groq | `openai/gpt-oss-120b` | `openai/gpt-oss-120b` | ok | âœ… ok | 339ms | The user says: "Reply exactly OK". So we must respond with exactly "OK". No extra punctuation, no extra whitespace? Probably just " |
| groq | `openai/gpt-oss-20b` | `openai/gpt-oss-20b` | ok | âœ… ok | 307ms | OK |
| groq | `qwen/qwen3-32b` | `qwen/qwen3-32b` | ok | âœ… ok | 442ms | <think> Okay, the user wants me to reply exactly "OK". Let me make sure I understand the request correctly. They provided a query where the user says |
| moonshot | `kimi-k2.5` | `kimi-k2.5` | ok | âŒ http_400 | 0ms | Error en el servidor remoto: (400) Solicitud incorrecta. |
| moonshot | `kimi-k2.6` | `kimi-k2.6` | ok | âŒ http_400 | 0ms | Error en el servidor remoto: (400) Solicitud incorrecta. |
| moonshot | `moonshot-v1-128k` | `moonshot-v1-128k` | ok | âœ… ok | 751ms | OK |
| moonshot | `moonshot-v1-32k` | `moonshot-v1-32k` | ok | âœ… ok | 666ms | OK |
| moonshot | `moonshot-v1-auto` | `moonshot-v1-auto` | ok | âœ… ok | 663ms | OK |
| nvidia | `moonshotai/kimi-k2.6` | `moonshotai/kimi-k2.6` | ok | âœ… ok | 623ms |  OK |
| nvidia | `nvidia/nemotron-3-super-120b-a12b` | `nvidia/nemotron-3-super-120b-a12b` | ok | âœ… ok | 6803ms | We need to reply exactly "OK". No extra spaces? The user says "Reply exactly OK". So output must be exactly "OK". Probably no newline? |
| nvidia | `openai/gpt-oss-120b` | `openai/gpt-oss-120b` | ok | âœ… ok | 376ms | The user says: "Reply exactly OK". So we must respond with exactly "OK". No extra punctuation, no extra whitespace? Probably just " |
| nvidia | `qwen/qwen3.5-397b-a17b` | `qwen/qwen3.5-397b-a17b` | ok | âœ… ok | 4241ms | OK |
| nvidia | `qwen/qwen3-coder-480b-a35b-instruct` | `qwen/qwen3-coder-480b-a35b-instruct` | ok | âŒ http_410 | 0ms | Error en el servidor remoto: (410) Desaparecido. |
| ollama | `deepseek-v4-pro` | `deepseek-v4-pro` | ok | âœ… ok | 19399ms | OK |
| ollama | `devstral-2:123b` | `devstral-2:123b` | ok | âœ… ok | 1011ms | OK |
| ollama | `kimi-k2.6` | `kimi-k2.6` | ok | âœ… ok | 4034ms | OK |
| ollama | `mistral` | `mistral-large-3:675b` | ok | âœ… ok | 911ms | OK |
| ollama | `qwen3-coder:480b` | `qwen3-coder:480b` | ok | âœ… ok | 998ms | OK |
| ollama | `qwen3-coder-next` | `qwen3-coder-next` | ok | âœ… ok | 1573ms | OK |
| openai | `gpt-4.1` | `` | missing_key | âšª not_tested | 0ms | missing API key |
| openai | `gpt-4o` | `` | missing_key | âšª not_tested | 0ms | missing API key |
| openai | `gpt-5` | `` | missing_key | âšª not_tested | 0ms | missing API key |
| openai | `gpt-5-mini` | `` | missing_key | âšª not_tested | 0ms | missing API key |
| openai | `gpt-oss-120b` | `` | missing_key | âšª not_tested | 0ms | missing API key |
| openrouter | `deepseek/deepseek-v4-pro` | `deepseek/deepseek-v4-pro` | ok | âœ… ok | 1706ms | OK |
| openrouter | `moonshotai/kimi-k2.6` | `moonshotai/kimi-k2.6` | ok | âœ… ok | 3250ms | OK |
| openrouter | `nvidia/nemotron-3-super-120b-a12b` | `nvidia/nemotron-3-super-120b-a12b` | ok | âœ… ok | 6481ms | OK |
| openrouter | `nvidia/nemotron-3-ultra-550b-a55b` | `nvidia/nemotron-3-ultra-550b-a55b` | ok | âœ… ok | 6418ms | OK |
| openrouter | `qwen/qwen3-coder` | `qwen/qwen3-coder` | ok | âœ… ok | 1015ms | OK |

## Copilot BYOM list from proxy `/api/tags`

Proxy status: ok

- `deepseek/deepseek-v4-pro (openrouter):latest`
- `deepseek-coder-6.7b-instruct (deepseek):latest`
- `deepseek-v4-flash (deepseek):latest`
- `deepseek-v4-pro (deepseek):latest`
- `deepseek-v4-pro (ollama):latest`
- `devstral-2:123b (ollama):latest`
- `gpt-oss-120b (cerebras):latest`
- `kimi-k2.5 (moonshot):latest`
- `kimi-k2.6 (moonshot):latest`
- `kimi-k2.6 (ollama):latest`
- `llama-3.3-70b-versatile (groq):latest`
- `meta-llama/llama-4-scout-17b-16e-instruct (groq):latest`
- `mistral (ollama):latest`
- `moonshotai/kimi-k2.6 (nvidia):latest`
- `moonshotai/kimi-k2.6 (openrouter):latest`
- `moonshot-v1-128k (moonshot):latest`
- `moonshot-v1-32k (moonshot):latest`
- `moonshot-v1-auto (moonshot):latest`
- `nvidia/nemotron-3-super-120b-a12b (nvidia):latest`
- `nvidia/nemotron-3-super-120b-a12b (openrouter):latest`
- `nvidia/nemotron-3-ultra-550b-a55b (openrouter):latest`
- `openai/gpt-oss-120b (groq):latest`
- `openai/gpt-oss-120b (nvidia):latest`
- `openai/gpt-oss-20b (groq):latest`
- `qwen/qwen3.5-397b-a17b (nvidia):latest`
- `qwen/qwen3-32b (groq):latest`
- `qwen/qwen3-coder (openrouter):latest`
- `qwen/qwen3-coder-480b-a35b-instruct (nvidia):latest`
- `qwen3-coder:480b (ollama):latest`
- `qwen3-coder-next (ollama):latest`
- `zai-glm-4.7 (cerebras):latest`
