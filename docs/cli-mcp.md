# Command line and MCP server

PromptOne has two local interfaces for trying the same structured-decision engine:

| Interface | Use it for | Transport |
|---|---|---|
| `promptone` | quick checks and interactive exploration from a terminal | command line |
| `promptone-mcp-server` | connecting an MCP client or agent to PromptOne's built-in tools | MCP over stdio |

Choose the CLI when you are the person reading the result. Choose MCP when another process needs
a stable tool interface. Both use PromptOne's `Router` to select a checkpoint and return typed
`choice`, `score`, and `noul` decisions; neither is an open-ended question-answering or text
generation interface.

For the routing decision and typed-question examples, see the README's [Route Mode quickstart](https://github.com/prompttechies-del/promptone#quickstart-route-mode-recommended).
For confidence and built-in workflows, see the README's [confidence gating](https://github.com/prompttechies-del/promptone#automated-confidence-gating) and [workflow presets](https://github.com/prompttechies-del/promptone#built-in-workflow-presets).

## 1. Command line

Installing the package installs the `promptone` entry point. Run `promptone --help` for the complete
option list.

```bash
python -m pip install promptone
promptone --help
```

### Evaluation CLI

The package also installs `promptone-evals`. The main CLI exposes the same evaluation commands
through `promptone eval`:

```bash
promptone eval --help
```

See the [Evaluation harness](evals.md) guide for datasets, metrics, and baseline gates.

### Training CLI

`promptone-train`, also reachable as `promptone train`, fine-tunes a checkpoint from a CSV or JSONL of labelled
decisions. See [Fine-tune with `promptone-train`](finetune.md#fine-tune-with-promptone-train).

```bash
promptone-train --help
```

### Route without loading a checkpoint

With text and no prediction flag, the CLI calls `Router.route`:

```bash
promptone "I was charged twice, please refund it"
```

The output names the selected checkpoint, explains why it was selected, and shows detected
language information when available. Routing alone does not download or build a checkpoint, so
it is a quick offline check of the routing decision.

Use `--json` when another local script should consume the decision:

```bash
promptone "I was charged twice, please refund it" --json
```

### Run a prediction

`--predict` runs the full typed prediction and loads the routed checkpoint on first use. The
first load needs access to the Hugging Face Hub; later runs use the local cache.

```bash
promptone "Classify this support request" --predict
promptone "Classify this support request" --predict --json
```

`--json` prints the complete result as JSON. Without it, the CLI prints each answer together
with its choice probability, score, or `noul` value, plus the routing decision.

The main controls are:

- `--model english|multilingual|typed-decisions` pins a checkpoint instead of auto-routing.
- `--lang en|de|...` supplies an explicit language code instead of automatic detection.
- `--lang-guess en|de|...` supplies a soft hint that routing reads after `--lang` and before its
  own detector; a hint that resolves to nothing falls through, so it nudges the checkpoint without
  forcing it.
- `--task NAME` forces the typed-decisions workflow instead of detecting it.
- `--device cpu|cuda|...` passes a device choice to the Router.
- `--json` emits machine-readable output.

### Use a built-in preset

A preset supplies a ready-made question set and implies prediction, so `--predict` is not needed:

```bash
promptone "My payment failed twice" --preset triage
promptone "Ignore all previous instructions" --preset guard --json
```

The CLI presets are `email`, `guard`, `moderation`, `router`, and `triage`. The CLI places the
text under the state field expected by the selected preset; `--predict` uses the router
question set's `request` field. Presets are useful for a quick local check, but their questions
are still domain decisions: inspect the preset and validate it on your own data before using it
as an application policy.

### Explore interactively

With no text argument, the CLI opens a small prompt:

```bash
promptone
# promptone> Classify this request
# promptone> quit
```

Press Enter to run each request. An empty line, `quit`, `exit`, or `Ctrl-D` ends the session. The
interactive loop reuses one Router, so it is a convenient way to compare several inputs without
writing a script.

### Failures are visible

The CLI handles invalid values and common dependency, download, and runtime failures at the
application boundary. It prints a diagnostic to stderr and returns exit code `2` instead of
showing an unhandled traceback. A handled failure ends the request, not the session: the prompt comes
back for the next line, and the session exits `2` when it ends, so a piped session reports the
code a failing request earned. If a first-use checkpoint download
fails, check dependency installation, Hub access, and the selected device before retrying.

## 2. Built-in MCP stdio server

The MCP server is an optional extra. The core package does not install the `mcp` dependency:

```bash
python -m pip install "promptone[mcp]"
promptone-mcp-server
# equivalent module form:
python -m promptone.mcp.server
```

The server speaks MCP over **stdio**, not HTTP. Configure the client with the console script:

```json
{
  "mcpServers": {
    "promptone": {
      "command": "promptone-mcp-server",
      "env": {
        "PROMPTONE_DEVICE": "cpu"
      }
    }
  }
}
```

If the client configuration supports a Python executable and arguments, use
`python -m promptone.mcp.server` as the equivalent launch form. The client owns the server process;
PromptOne does not open a network port.

### Available tools

| Tool | What it does | Main inputs |
|---|---|---|
| `promptone_status` | Reports the device per loaded checkpoint (`checkpoint_devices`), the configured or actual device, CUDA availability, loaded checkpoints, preload state, readiness, and package versions. | none |
| `promptone_route` | Selects a checkpoint and returns its model, repository, and reason without running a forward pass. | `state`, `questions`, optional `model`, `task`, `lang`, `lang_guess` |
| `promptone_predict` | Runs typed questions and returns answers, routing metadata, latency, and the answering device when readable. | `state`, `questions`, optional `model` (`auto`, `english`, `multilingual`, or `typed-decisions`), `task`, `lang`, `lang_guess`, `max_len`, `head_max_len`, `min_confidence` |
| `promptone_shortlist` | Shortlists a many-option choice question, then answers it and returns the shortlist metadata. | `state`, `questions`, optional `model`, `k` (default `20`), `task`, `lang`, `lang_guess`, `max_len`, `head_max_len`, `min_confidence` |
| `promptone_tournament` | Narrows large choices in balanced rounds using the decision model, then answers the complete request. In-process only. | `state`, `questions`, optional `model`, `group_size` (default `16`), `task`, `lang`, `lang_guess`, `max_len`, `head_max_len`, `min_confidence` |
| `promptone_preset` | Runs a built-in workflow using its built-in question set. | `preset`, `state`, optional `task`, `lang`, `lang_guess`, `max_len`, `head_max_len`, `min_confidence` |
| `promptone_predict_batch` | Answers many requests in one call. Requests are routed first and grouped by checkpoint, so matching question schemas share forward passes; answers come back in input order. | `requests`, each `{state, questions, model?, task?, lang?, lang_guess?, max_len?, head_max_len?}`, optional `batch_size`, `hooks_timeout`, `min_confidence`, `sort_by_length` |
| `promptone_route_batch` | Decides which checkpoint would answer each request, with no forward pass and no checkpoint load. | `requests`, each `{state, questions, model?, task?, lang?, lang_guess?}`, optional `hooks_timeout` |
| `promptone_decide` | Answers a JSON-schema-shaped decision in one forward pass and returns the decided values with per-field confidence, instead of an answer map to parse. Schema properties may be enum choices, booleans, or integers with a minimum and maximum; free strings, arrays, and nested objects are rejected by path. | `state`, `schema`, optional `model`, `min_confidence` |
| `promptone_predict_long` | Scans a state longer than the context window in overlapping windows, then answers with per-answer window attribution and the window count. | `state`, `questions`, optional `model`, `window`, `stride`, `task`, `lang` |

On `promptone_route`, `promptone_predict`, `promptone_shortlist`, `promptone_tournament`, and `promptone_decide`, `model` may be omitted or set to `null`; either form resolves to `auto`.

The three batch and schema tools exist because the same operations are available on the SDK and
`promptone-serve`: handling many requests, or serving a caller that already knows the answer shape,
does not require dropping to Python. For the schema-driven form in more depth, see
[Schema-driven decisions](structured.md).

The shared guardrail says to narrow choice questions with more than 20 options using
`promptone_shortlist` or `promptone_tournament`. `promptone_shortlist` keeps the `k` most likely labels before the forward pass; its
default is `k=20`. It uses mean-pooled embeddings from the answering checkpoint's own encoder,
so it does not download a second model, and returns the kept labels, cosine scores, `k`,
option count, and whether the question passed through unshortlisted for each shortlisted
question. A question with `k` at or past its option count is passed through unchanged, so
its cosine scores come back `null` rather than empty. A question that narrows cannot keep
a caller-supplied `option_order`: the order covers every option while the answer covers the
kept labels only, and remapping it onto them would move options between slots, so the order
is dropped and the question is named under `option_order_dropped` with its old and kept
option counts.

`promptone_tournament` uses the decision model itself to narrow choices, without an embedding pass.
With the default `group_size=16`, 40 labels form balanced groups of 13, 13 and 14. One forward
pass answers all three groups; their winners meet in a final pass that also answers the rest
of the request. Larger sets can take more elimination rounds. The checkpoint is selected from
the original request and pinned for every round, keeping routing controls and detected-language
calibration consistent. Token budgets and `min_confidence` reach every pass.
A missing or out-of-group winner returns `internal_error` before another pass, so a broken
round cannot silently advance a label the model did not select from that group.

The result has the usual `answers`, `routing`, `latency_ms` and readable `device`, plus
`tournament` metadata for each choice: `labels` are the finalists in criteria order, `n` is the
original option count, and `rounds` counts elimination rounds before the final pass. A choice
that fits `group_size` reports `rounds: 0`; non-choice questions have no tournament entry.
Probabilities and confidence come from the final pass over the finalists only, so a threshold
fitted on the full label set needs to be measured again. `latency_ms` covers routing and all
passes. Narrowing reports a discarded `option_order` under `option_order_dropped` with `n` and
`kept` counts, just as shortlisting does; passthrough questions keep their order.

For a small example, call `promptone_tournament` with these arguments. `group_size=2` forces one
elimination round here; keep the default 16 for a larger catalog unless you have measured another size.

```json
{
  "state": {"body": "Please refund the duplicate charge."},
  "questions": {
    "department": {
      "type": "choice",
      "instructions": "Which team should handle the `body`?",
      "criteria": {
        "billing": "payments, invoices, refunds",
        "technical": "bugs, outages, integrations",
        "account": "login, passwords, profile settings",
        "sales": "pricing, upgrades, product enquiries"
      }
    }
  },
  "group_size": 2
}
```

This makes two prediction calls and returns two finalists with `n: 4` and `rounds: 1`.
`group_size` must be an integer of at least 2; invalid values return `invalid_group_size`.
The tool is available only when the MCP server loads checkpoints in-process.

When the state itself does not fit one window, `promptone_predict` answers from its first window and
never reads the rest. `promptone_predict_long` tokenizes the state once, scores it in overlapping
windows sized by `window` (default: the checkpoint budget) stepped by `stride` (default: half
the window), and aggregates per question -- strongest window for `noul`, most-confident window
for `choice`/`score`. Each answer names its deciding window, and the result counts the windows
scored. There is no `min_confidence` here and no `max_len`/`head_max_len`: core's scan takes
neither.

`state` must be a non-empty JSON object. `questions` must be a non-empty object whose values use
PromptOne's typed question schema. `promptone_preset` accepts the same five presets the CLI does: `email`,
`guard`, `moderation`, `triage`, and the router workflow, whose canonical name on this surface is
`model_router`. `router` is accepted as an alias and names the same preset, so the CLI spelling
works here too; the canonical key is the one that comes back in the result. Given a state of
exactly one string, `promptone_preset` places it under the field that preset's questions name, the
same placement the CLI does, so a caller does not have to guess the key. Anything richer than one
string is the caller's own shape and is passed through untouched.

Every single-request tool takes the same per-call routing controls the batch requests do. Alongside
`model`, a request may set `task` (name a checkpoint by the work), `lang` (force a language code),
and `lang_guess` (a soft language hint that sits below `lang` and above the built-in detector, so a
probable-but-uncertain code can nudge which checkpoint is chosen without forcing it the way `lang`
does). `lang_guess` only participates in routing, so like `task` it is refused on a call that pins
`model` -- a pinned checkpoint has nothing left to route. `promptone_predict`, `promptone_shortlist`, and `promptone_tournament` also
take `max_len`/`head_max_len` for the answering token budget and `min_confidence` for the abstention
gate.

A prediction call has the same shape as the SDK's typed call:

```json
{
  "state": {
    "body": "I was billed twice for the same plan. Please reverse the duplicate charge."
  },
  "questions": {
    "department": {
      "type": "choice",
      "instructions": "Which team should handle this request?",
      "criteria": {
        "billing": "payments, invoices, refunds, duplicate charges",
        "technical": "bugs, outages, integration problems"
      }
    },
    "urgent": {
      "type": "noul",
      "instructions": "Does the user need immediate help?"
    }
  }
}
```

The tool response is JSON containing the typed `answers`, the `routing` decision, and timing
information. Do not treat a high-confidence answer as permission to perform an external action;
the application or agent remains responsible for policy, review, and side effects.

### Startup and environment

The MCP server keeps a resident Router and serializes first-time construction. By default it
preloads `english` and `multilingual`; `typed-decisions` stays lazy. A preload failure is
reported at startup and retried on the next tool call, so inspect `promptone_status` before assuming
the server is ready.

| Variable | Default | Meaning |
|---|---|---|
| `PROMPTONE_DEVICE` | automatic | The device checkpoints are asked to build on, such as `cpu` or `cuda`. Not the value verbatim: `promptone.mcp.device.env_device` strips and lower-cases it, so `CUDA:0` works, and a string PyTorch cannot parse is refused with an error naming this variable. So is a string PyTorch parses into a *different* device: an index it cannot keep is read back as another one (`cuda:258` builds GPU 2), and the same refusal names both the value and what PyTorch would have used. Because there is no startup to refuse, that comes back as a `router construction failed` tool error. It is a preference, not a guarantee: a GPU that cannot be had falls back to CPU silently, and `promptone_status` reports the loaded checkpoint's real device. |
| `PROMPTONE_PRELOAD` | `1` | Build the configured checkpoints at startup. Set to `0` for lazy loading. |
| `PROMPTONE_MODELS` | `english,multilingual` | Comma-separated checkpoints to preload. An empty value keeps the MCP default rather than preloading every checkpoint. |
| `PROMPTONE_THREADS` | PyTorch default | Caps Torch intra-op threads for CPU inference; keep it at or below the physical core count. |
| `PROMPTONE_AUTO_TASK` | `0` | Set to `1` to let a request auto-route to the `typed-decisions` checkpoint. Same meaning as in `promptone.serve`; it does not preload that checkpoint, so `PROMPTONE_MODELS` still decides what is built at startup. |
| `PROMPTONE_DEFAULT_MODEL` | `multilingual` | The checkpoint a state with no language evidence falls back to, same meaning as in `promptone.serve`. Unlike `promptone.serve`, an unresolvable name does not stop the server: it comes back as a `router construction failed` tool error on the next call, because a stdio server has no startup to refuse. |
| `PROMPTONE_BASE_URL` | unset | Send predictions to a `promptone-serve` on your own hardware instead of loading checkpoints in each MCP process. A bare `host:port` is read as HTTP. |
| `PROMPTONE_REMOTE_TIMEOUT` | `300` | HTTP timeout in seconds when `PROMPTONE_BASE_URL` is set, including the server's cold load. Invalid or non-positive values use the default. |
| `PROMPTONE_USER_AGENT` | `promptone/<version>` | User-Agent for remote HTTP requests. Read when the remote Router is constructed; empty or space/tab-only values use the default. Invalid header values raise a configuration error. Ignored in local mode. |
| `PROMPTONE_API_KEY` | unset | Bearer token sent with every remote request when `PROMPTONE_BASE_URL` is set. Required by a `promptone-serve` that set its own `PROMPTONE_API_KEY`; ignored in local mode. |

### Share one model server across MCP sessions

Run one local HTTP server and point each MCP client's environment at it:

```bash
PROMPTONE_HOST=127.0.0.1 PROMPTONE_PRELOAD=0 PROMPTONE_IDLE_UNLOAD_SECONDS=300 promptone-serve
```

```json
{
  "mcpServers": {
    "promptone": {
      "command": "promptone-mcp-server",
      "env": {"PROMPTONE_BASE_URL": "http://127.0.0.1:8000"}
    }
  }
}
```

Install `promptone[serve]` where the HTTP server runs. MCP still uses stdio with the editor; its
prediction tools use HTTP to reach your server. `promptone_predict`, `promptone_predict_batch`, `promptone_decide`
and `promptone_preset` use the server's original state, instructions and option descriptions.
`promptone_predict_batch` sends one `/v1/systemone` request per item -- every batch, not only a
heterogeneous one -- preserving input order; `batch_size` and `sort_by_length` are accepted and
ignored, so they do not change the server's execution. `promptone_status` reports the
server's `/health`; `promptone_route` and `promptone_route_batch` stay local and need no model or HTTP request.
The MCP process imports no torch and loads no checkpoint, including when `PROMPTONE_THREADS` or
`PROMPTONE_PRELOAD` is set.

Set the same `PROMPTONE_API_KEY` in both processes when the server requires a bearer token. Keep
`PROMPTONE_DEFAULT_MODEL` and `PROMPTONE_AUTO_TASK` aligned so local routing previews match the server's
actual routing. Device and preload settings belong to the HTTP server. The first call after an
idle unload waits for a cold load; raise `PROMPTONE_REMOTE_TIMEOUT` if that takes longer than 300 seconds.

Remote requests identify the library as `promptone/<promptone.__version__>`. An operator can use that
prefix in an allowlist, or set `PROMPTONE_USER_AGENT` for a deployment-specific identity. This header
does not replace bearer authentication or guarantee acceptance by a proxy's security policy.
When embedding `RemoteRouter` directly, its keyword-only `user_agent` argument takes precedence
over the environment. `None` reads the environment; an explicit empty or space/tab-only string
selects the default. The selected value is checked before surrounding spaces/tabs are trimmed:
CR/LF, other ASCII controls except tab, DEL, and non-Latin-1 characters raise an error naming
`PROMPTONE_USER_AGENT`. Unlike an invalid timeout, an invalid header is never silently replaced.
Each Router keeps the value resolved at construction; environment changes require a new Router
or a restarted MCP process. An explicit request header also takes precedence over a UA set on
a custom urllib opener; use this setting instead of that workaround.

`promptone_shortlist`, `promptone_tournament`, and prediction hook overrides return `unsupported_remote`, since these
paths run in the model process. For shared-server tournament selection, enable
[`PROMPTONE_TOURNAMENT_GROUP` on the HTTP server](http-api.md#large-choices-promptone_tournament_group) and call
`promptone_predict`: HTTP keeps probabilities for every original label (zero for eliminated labels),
reports `x_tournament`, and counts usage across rounds. The in-process `promptone_tournament` returns
finalist-only probabilities and core's `tournament` metadata. HTTP errors retain the server's detail text as MCP tool errors. With
`PROMPTONE_BASE_URL` unset, the MCP server keeps loading checkpoints in its own process.

The stock `promptone-mcp-server` launcher creates its Router without installing hooks. Install prediction
hooks in the process that runs inference: the MCP process in local mode, or the HTTP server in
shared-server mode. A custom launcher can use `promptone.hooks.set_default_hooks` before building its Router. The environment variables
above configure model lifecycle, not hook registration. The client still decides when to call a
tool and what to do with the returned decision.

## 3. Shared boundaries and related guides

The CLI and MCP server are interfaces to the same typed decision engine:

- Use `choice` for a finite label set, `score` for an ordered rubric, and `noul` for the
  probability of true.
- Validate thresholds and presets on representative data; there is no universal adoption
  threshold.
- Keep irreversible or high-cost actions behind the application's review and fallback policy.
- The MCP server calls `Router.predict`, so hooks fire when a custom launcher installs them. See
  [Prediction hooks](hooks/index.md), [hook lifecycle](hooks/lifecycle.md), and
  [Tracing](hooks/tracing.md) for observability and `run_id` correlation.

This guide covers the local CLI and the built-in MCP stdio server. It does not document the
HTTP API, community wrappers, or an MCP protocol redesign.
