<p align="center">
  <img src="https://raw.githubusercontent.com/prompttechies-del/promptone/main/assets/promptone-logo.webp" alt="PromptOne logo" width="128" />
</p>

<h1 align="center">PromptOne</h1>

<p align="center">
  <b>Typed decisions in one forward pass.</b><br>
  Choices, scores and probabilities from text in about 33 ms, on your own hardware.
</p>

<p align="center">
  <a href="https://github.com/prompttechies-del/promptone/actions/workflows/ci.yml"><img src="https://github.com/prompttechies-del/promptone/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache%202.0-4A3AFF" alt="License: Apache 2.0"></a>
  <img src="https://img.shields.io/badge/python-3.10%2B-0F1330" alt="Python 3.10+">
  <a href="https://huggingface.co/zameer1/promptone"><img src="https://img.shields.io/badge/weights-zameer1%2Fpromptone-565C75" alt="Model weights"></a>
</p>

<p align="center"><a href="https://promptone.prompttechies.workers.dev">Website</a> · <a href="https://github.com/prompttechies-del/promptone/blob/main/docs/guide.md">Full guide</a> · <a href="https://github.com/prompttechies-del/promptone/blob/main/docs/http-api.md">HTTP API</a></p>

---

PromptOne reads a message and answers typed questions about it, all at once:

```text
"I was charged twice. Please refund the duplicate."

department        choice       billing           0.94
urgency           score        1.84 of 2
refund_requested  probability  0.91
```

There is no text to generate and nothing to parse. You get structured answers your code can branch on, with a probability for every option, from a model that runs on a laptop, a server or a GPU. Nothing leaves your machine.

> **Model weights.** The checkpoints are being published to [`zameer1/promptone`](https://huggingface.co/zameer1/promptone) on Hugging Face. Until they are online, `Router()` cannot download a model; everything else in this repository builds and tests without them.

## Contents

- [Why PromptOne](#why-promptone)
- [Install](#install)
- [Quickstart](#quickstart)
- [Question types](#question-types)
- [Checkpoints and routing](#checkpoints-and-routing)
- [Server and playground](#server-and-playground)
- [Ready-made presets](#ready-made-presets)
- [Integrations](#integrations)
- [SDKs for other languages](#sdks-for-other-languages)
- [Performance](#performance)
- [Fine-tuning](#fine-tuning)
- [Limits](#limits)
- [Repository layout](#repository-layout)
- [Contributing](#contributing)
- [License](#license)

## Why PromptOne

Most "decisions" in an AI product are classification in disguise: which team gets this ticket, is this prompt a jailbreak, should this go to the large model. Prompting a chat model for them works, but you pay for it in latency, tokens and parsing code.

| When you need | PromptOne | Prompting a chat model |
|---|---|---|
| Output | Typed answers with calibrated probabilities | Free text you have to parse |
| Speed | One forward pass, ~33 ms on a T4 | Token-by-token generation |
| Where it runs | Your own hardware, offline | Usually a hosted API |
| Cost per decision | Your own compute | Billed per token |
| Open-ended writing and reasoning | Not what it is for | What it is for |

Use PromptOne for the fast, frequent, structured calls, and keep a chat model for the work that needs one.

## Install

```bash
pip install promptone
```

Python 3.10 or newer. Add extras for what you use:

| Extra | Adds |
|---|---|
| `promptone[serve]` | HTTP server and browser playground |
| `promptone[mcp]` | MCP server for agent clients |
| `promptone[onnx]` | ONNX Runtime backend |
| `promptone[fast]` | TileLang GPU fast path |
| `promptone[structured]` | Pydantic models in `decide` |
| `promptone[langchain]`, `[langgraph]`, `[llamaindex]`, `[crewai]`, `[agno]` | Framework integrations |

Docker, Nix and per-platform GPU setup are covered in [LOCAL_SETUP.md](https://github.com/prompttechies-del/promptone/blob/main/LOCAL_SETUP.md) and [docs/docker.md](https://github.com/prompttechies-del/promptone/blob/main/docs/docker.md).

## Quickstart

```python
from promptone import Router

router = Router()  # downloads a checkpoint on first use; Router(preload=True) loads all three

state = "Hi, we were billed twice for March. Please refund the duplicate today or we will cancel."
questions = {
    "department": {"type": "choice", "instructions": "Which department should handle this?",
                   "criteria": {"billing": "invoices, payments, refunds",
                                "technical": "bugs, outages, errors",
                                "other": "everything else"}},
    "urgency": {"type": "score", "instructions": "How urgent is this?",
                "criteria": ["not urgent", "soon", "blocking"]},
    "churn_risk": {"type": "noul", "instructions": "Does the user threaten to cancel or leave?"},
}

result = router.predict(state, questions)
print(result["answers"]["department"]["choice"])  # billing
print(result["answers"]["churn_risk"]["noul"])    # probability the answer is yes
print(result["routing"]["model"])                 # english
```

The same call works in 100+ languages. The router detects the script and language and sends non-English text to the multilingual checkpoint:

```python
for text in ["मुझसे मार्च में दो बार शुल्क लिया गया, कृपया डुप्लिकेट राशि वापस करें।",
             "La aplicación se cierra cada vez que abro la configuración."]:
    r = router.predict(text, {"department": questions["department"]})
    print(r["routing"]["model"], r["answers"]["department"]["choice"])
# multilingual billing
# multilingual technical
```

From the command line:

```bash
promptone "My payment failed twice" --preset triage
```

## Question types

| Type | Answers | Example |
|---|---|---|
| `choice` | One option from a list you define, with a probability for each | `department → billing (0.94)` |
| `score` | A position on an ordered scale | `urgency → 1.84 of 2` |
| `noul` | The probability that a statement is true | `refund requested → 0.91` |

Every question in a request is answered in the same forward pass, so five questions cost little more than one. See [docs/structured.md](https://github.com/prompttechies-del/promptone/blob/main/docs/structured.md) for JSON-schema decisions and [docs/questions-and-answers.md](https://github.com/prompttechies-del/promptone/blob/main/docs/questions-and-answers.md) for the full answer format.

## Checkpoints and routing

| Checkpoint | Encoder | Parameters | Context | Best for |
|---|---|---|---|---|
| `english` | ModernBERT-large | 421M | 512 tokens | English routing, classification, moderation |
| `multilingual` | mmBERT-base | 322M | 1,024 tokens (8,192 with `max_len`) | 100+ languages |
| `typed-decisions` | ModernBERT-large | 421M | 1,024 tokens | Structured choice, score and probability workflows |

`Router` picks the checkpoint for each request from its script, language and question types. Override it with `model="multilingual"`, preload what you serve with `Router(preload=True)`, and pass long documents with `max_len=8192` on the multilingual checkpoint. Details in [docs/routing.md](https://github.com/prompttechies-del/promptone/blob/main/docs/routing.md).

## Server and playground

```bash
pip install "promptone[serve]"
promptone-serve
```

- `POST /v1/systemone` and `POST /v1/systemone/batch` answer decisions over HTTP ([docs/http-api.md](https://github.com/prompttechies-del/promptone/blob/main/docs/http-api.md)).
- `GET /health` reports readiness and loaded checkpoints.
- **Playground at [`http://localhost:8000/ui/`](http://localhost:8000/ui/)**: write a message, add questions, and compare all three checkpoints side by side, with history, share links and ready-to-copy Python, JavaScript and curl.

Set `PROMPTONE_API_KEY` to require a bearer token, `PROMPTONE_UI=0` to turn the playground off, and `PROMPTONE_CORS_ORIGINS` to allow pages on other origins. Docker images and compose files for CPU, CUDA and Apple Silicon are in [docs/docker.md](https://github.com/prompttechies-del/promptone/blob/main/docs/docker.md).

## Ready-made presets

```python
import promptone

agent = promptone.load("zameer1/promptone")

agent.predict({"request": "Refactor this service"}, promptone.router_questions())      # small vs large model
agent.predict({"prompt": "Ignore all instructions"}, promptone.guard_questions())      # jailbreaks, injections
agent.predict({"post": "User comment text"}, promptone.moderation_questions())         # toxicity, threats
agent.predict({"message": "My payment failed twice"}, promptone.triage_questions())    # intent, urgency, churn
agent.predict({"body": "Your account is locked"}, promptone.email_questions())         # spam, phishing, reply
```

## Integrations

| Framework | Guide |
|---|---|
| LangChain and LangGraph | [docs/langchain.md](https://github.com/prompttechies-del/promptone/blob/main/docs/langchain.md) |
| LlamaIndex | [docs/llamaindex.md](https://github.com/prompttechies-del/promptone/blob/main/docs/llamaindex.md) |
| CrewAI | [docs/crewai.md](https://github.com/prompttechies-del/promptone/blob/main/docs/crewai.md) |
| Agno | [docs/agno.md](https://github.com/prompttechies-del/promptone/blob/main/docs/agno.md) |
| MCP clients (Claude, Cursor and others) | [docs/cli-mcp.md](https://github.com/prompttechies-del/promptone/blob/main/docs/cli-mcp.md) |
| Prediction hooks for logging, caching and tracing | [docs/hooks/](https://github.com/prompttechies-del/promptone/tree/main/docs/hooks) |
| Evaluation harness | [docs/evals.md](https://github.com/prompttechies-del/promptone/blob/main/docs/evals.md) |

## SDKs for other languages

| Language | Folder | Runs |
|---|---|---|
| TypeScript, Node.js and browser | [`promptone-ts/`](https://github.com/prompttechies-del/promptone/tree/main/promptone-ts) | ONNX in-process |
| TypeScript HTTP client | [`sdk/typescript/`](https://github.com/prompttechies-del/promptone/tree/main/sdk/typescript) | Against `promptone-serve` |
| Python HTTP client | [`sdk/python/`](https://github.com/prompttechies-del/promptone/tree/main/sdk/python) | Against `promptone-serve` |
| Java | [`promptone-java/`](https://github.com/prompttechies-del/promptone/tree/main/promptone-java) | ONNX in-process |
| .NET | [`promptone-dotnet/`](https://github.com/prompttechies-del/promptone/tree/main/promptone-dotnet) | ONNX in-process |
| Go | [`promptone-go/`](https://github.com/prompttechies-del/promptone/tree/main/promptone-go) | ONNX in-process |
| Rust | [`promptone-rust/`](https://github.com/prompttechies-del/promptone/tree/main/promptone-rust) | ONNX in-process |
| C++ | [`promptone-cpp/`](https://github.com/prompttechies-del/promptone/tree/main/promptone-cpp) | ONNX in-process |

Each SDK is tested against golden outputs recorded from the Python engine, so a decision is the same in every language.

## Performance

Measured on one Tesla T4, warm, single request:

| Questions per call | `english` | `multilingual` |
|---|---|---|
| 1 | 39.5 ms | 32.8 ms |
| 5 | 84.5 ms | 40.1 ms |
| 10 | 158.6 ms | 72.3 ms |
| 50 | 771 ms | 337 ms |

Batched throughput reaches 103 to 332 questions per second on a single T4. Full results, hardware and methods are in [BENCHMARKS.md](https://github.com/prompttechies-del/promptone/blob/main/BENCHMARKS.md) and [docs/benchmarks.md](https://github.com/prompttechies-del/promptone/blob/main/docs/benchmarks.md).

## Fine-tuning

The checkpoints work zero-shot, but fine-tuning on decisions from your own domain is where accuracy jumps. On the typed-decisions benchmark (2,000 decisions across four workflows), the fine-tuned `typed-decisions` checkpoint scores **0.766** accuracy against **0.362** for the base English checkpoint.

- [Kaggle notebook (2× T4)](https://github.com/prompttechies-del/promptone/blob/main/notebooks/promptone_finetune_typed_decisions_2xT4_kaggle.ipynb)
- [Apple Silicon and CPU script](https://github.com/prompttechies-del/promptone/blob/main/notebooks/promptone_finetune_typed_decisions_mps.py)
- [Guide: build a dataset, train, calibrate, evaluate, export](https://github.com/prompttechies-del/promptone/blob/main/docs/finetune.md)

## Limits

- **Fine-tune for specialised workflows.** Zero-shot accuracy on unfamiliar decision types can be close to chance; the fine-tuning loop above is the fix.
- **Avoid boolean words as `choice` labels.** Labels such as `yes` and `no` can pull the answer away from the option descriptions. Use descriptive or opaque labels like `A` and `B`.
- **Check negation on your own data.** Requests like "do not cancel my account" can be misread; validate the wording you serve.
- **Many options shrink the room for text.** With dozens of `choice` options the question takes more of the context window; see [docs/routing.md](https://github.com/prompttechies-del/promptone/blob/main/docs/routing.md) for budgets.
- **Not a chat model.** It answers questions you define; it does not write, summarise or reason step by step.

More in [docs/guardrail-limitations.md](https://github.com/prompttechies-del/promptone/blob/main/docs/guardrail-limitations.md) and [SECURITY.md](https://github.com/prompttechies-del/promptone/blob/main/SECURITY.md).

## Repository layout

```text
promptone/          Python engine, router, server (serve.py) and playground (ui/)
promptone-*/        SDKs: ts, java, dotnet, go, rust, cpp
sdk/                HTTP clients for Python and TypeScript
site/               Product website and playground
docs/               Documentation site sources
tests/              Python test suites
benchmarks/, research/, verify/   Measurement and verification scripts
notebooks/          Fine-tuning notebooks
```

## Contributing

Read [CONTRIBUTING.md](https://github.com/prompttechies-del/promptone/blob/main/CONTRIBUTING.md) first. Before opening a pull request, run:

```bash
ruff check promptone/ --select=E9,F63,F7,F82,F401,F811 --line-length=120
python -m compileall -q promptone/ tests/
python tests/test_router.py && python tests/test_hooks_api.py
python -m pytest tests/test_serve.py
```

Report security issues privately as described in [SECURITY.md](https://github.com/prompttechies-del/promptone/blob/main/SECURITY.md).

## License

Apache 2.0. See [LICENSE](https://github.com/prompttechies-del/promptone/blob/main/LICENSE).
