# Stateful LLM Context Savings Benchmark

CR-0011 second-turn comparison using the official `LlmCompleteActionRequest`,
`ActionFrame`, and native NCP Tier-2 MessagePack codec. The stateful request
contains only the new tool result and user message; the committed prefix is
reused by the instrumented deterministic runtime.

| Gate | Stateless | Stateful | Savings |
| --- | ---: | ---: | ---: |
| Decoder `wire_input_bytes` | 756 | 586 | **22.5 %** |
| Runtime `evaluated_tokens` | 157 | 59 | **62.4 %** |
| Runtime `reused_tokens` | 0 | 99 | - |

- Strict native mode: `true`
- Protocol fallback enabled: `false`
- Ordered role/tool semantic parity: `true`
- Stateful second turn is delta-only: `true`

`wire_input_bytes` is read from the decoded NCP header payload length. It
excludes the NCP header, TLS/encryption framing, and response bytes.
`evaluated_tokens` is observed from the deterministic benchmark runtime's
tokenizer-agnostic `ceil(UTF-8 bytes / 4)` counter over canonical model messages.

```bash
dotnet run --project impl/dotnet/benchmarks/NPS.Benchmarks.TokenSavings -- --llm-context --emit
```
