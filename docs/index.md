# PromptOne

Multilingual, non-autoregressive System 1 decision engine: typed `choice`, `score` and `noul`
decisions over any state, in a single forward pass.

A `choice` question picks one of several options, a `score` question places the state on a
scale, and a `noul` question gives the probability that the answer is yes. PromptOne runs on your own
hardware, reads 100+ languages, and its `Router` picks the checkpoint for each request.

## Start here

Install PromptOne and make a first decision with the README's
[Installation](https://github.com/NandhaKishorM/promptone#installation) and
[Quickstart](https://github.com/NandhaKishorM/promptone#quickstart). They stay in the README so there
is one copy to keep current.

## Find a guide

| To | Read |
|---|---|
| get typed values back from a JSON schema or a pydantic model | [Schema-driven decisions](structured.md) |
| log, redact, cache or gate every decision without forking PromptOne | [Prediction hooks](hooks/index.md) |
| adopt PromptOne incrementally without granting execution permission | [Staged adoption](staged-adoption.md) |
| route, screen or triage inside a LangChain or LangGraph app | [LangChain & LangGraph](langchain.md) |
| run PromptOne from a terminal or connect its built-in MCP stdio server | [Command line and MCP server](cli-mcp.md) |
| hand each request to the checkpoint that fits its language and task | [Routing](routing.md) |
| call PromptOne over HTTP from a client written against the Jev wire protocol | [HTTP API](http-api.md) |
| run the SDK or the `promptone-serve` HTTP API in a container, on CPU or an NVIDIA GPU | [Docker quickstart](docker.md) |
| build for ARM64 hosts or DGX Spark | [ARM64 and DGX Spark containers](docker-platforms.md) |
| specialise a checkpoint for your own decisions | [Browser-agent fine-tuning example](finetune_browser_agent.md) and the [fine-tuning notebook](https://github.com/NandhaKishorM/promptone/blob/main/notebooks/promptone_finetune_typed_decisions_2xT4_kaggle.ipynb) |
| judge benchmark results and deployment limits | [Benchmarks and known limits](benchmarks.md) |
| look up a class, function or parameter | [Python API reference](reference/index.md) |
| score a labelled dataset, compare to a baseline, or gate a build on it | [Evaluation harness](evals.md) |

Confidence gating is in the
[README](https://github.com/NandhaKishorM/promptone#automated-confidence-gating) for now.
