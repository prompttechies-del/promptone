# PromptOne Models: Obtaining the ONNX Artifacts

The PromptOne Rust SDK ([README](https://github.com/prompttechies-del/promptone/blob/main/promptone-rust/README.md)), a Rust port of the PromptOne Python SDK, runs
**ONNX** models through ONNX Runtime, not PyTorch weights, so no Python process is needed at runtime. This
guide explains how to get those ONNX artifacts: either convert them yourself
from the published PyTorch checkpoints (works today), or let the SDK download
them once they are published.

**Quick path:** create a venv, `pip install -e .` plus the exporter's
dependencies, run `python promptone-rust/tools/export_onnx.py --model all`, and set `PROMPTONE_ONNX_ROOT` to the
resulting `onnx/` folder. [Section 1](#1-export-the-onnx-model-step-by-step)
walks through each step.

The ONNX export is not on Hugging Face yet, so exporting it yourself is the
only way to get the artifacts today. Downloading
([section 6](#6-downloading-future-once-published)) will work without code
changes once they are published.

---

## 1. Export the ONNX model (step by step)

You need Python 3.9+ and about 2.5 GB free per checkpoint (6.7 GB for all
three, counting the Hugging Face download cache; see step 3). Run everything
from the **repository root** unless a step says otherwise.

### Step 1: Create a virtual environment and install

The dynamo-based ONNX exporter requires `torch >= 2.9`.
Tested versions (from the repo's `.venv`):

| Package | Tested version |
|---|---|
| `torch` | 2.14.0 |
| `onnx` | 1.23.0 |
| `onnxscript` | 0.7.2 |
| `onnxruntime` | 1.30.0 |
| `transformers` | 5.17.0 |
| `safetensors` | 0.8.0 |
| `huggingface_hub` | 1.32.0 |
| `numpy` | 2.5.3 |

The `promptone` Python package itself must be importable, so the commands below
also install the repository with `pip install -e .`.

```powershell
# PowerShell
python -m venv .venv
.venv\Scripts\Activate.ps1
pip install "torch>=2.9" onnx onnxscript onnxruntime transformers safetensors huggingface_hub numpy
pip install -e .
```

```bash
# Bash / WSL
python -m venv .venv && source .venv/bin/activate
pip install "torch>=2.9" onnx onnxscript onnxruntime transformers safetensors huggingface_hub numpy
pip install -e .
```

### Step 2 (optional): Hugging Face authentication

The `zameer1/promptone` checkpoints are public and need no token. For
gated or private repos, supply a token:

```powershell
$env:HF_TOKEN = "hf_..."          # PowerShell — persists for the session
```

```bash
export HF_TOKEN="hf_..."           # Bash
```

Or pass `--token hf_...` directly to the script.

### Step 3 (optional): Disk space and HF\_HOME

The exporter calls `huggingface_hub.snapshot_download` to fetch the PyTorch
weights. If the default HuggingFace cache (`%LOCALAPPDATA%\HuggingFace` on
Windows, `~/.cache/huggingface` elsewhere) is on a volume without enough
space, redirect it with `HF_HOME`.

| Checkpoint | PyTorch weights (HF cache) | ONNX output | Total |
|---|---|---|---|
| `multilingual` | ~644 MB | ~1.2 GB | ~1.9 GB |
| `english` | ~842 MB | ~1.6 GB | ~2.5 GB |
| `typed-decisions` | ~842 MB | ~1.6 GB | ~2.5 GB |
| all three | ~2.3 GB | ~4.4 GB | ~6.7 GB |

```powershell
$env:HF_HOME = "C:\hf_cache"      # PowerShell
```

```bash
export HF_HOME=/d/hf_cache         # Bash
```

### Step 4: Export

The script downloads the PyTorch checkpoint from `zameer1/promptone`
(public, no token needed) and writes `onnx/<checkpoint>/` under the repository
root. The multilingual checkpoint takes about 9 minutes on CPU.

```powershell
# Export the multilingual checkpoint to onnx/multilingual/  (~9 min on CPU)
python promptone-rust/tools/export_onnx.py --model multilingual

# Export and verify ONNX parity against PyTorch (~9 min + ~5 min verify)
python promptone-rust/tools/export_onnx.py --model multilingual --verify

# Export typed-decisions with parity check (~30 min total)
python promptone-rust/tools/export_onnx.py --model typed-decisions --verify

# Export all three checkpoints
python promptone-rust/tools/export_onnx.py --model all

# Export to a custom directory (no --force needed; leaves onnx/ untouched)
python promptone-rust/tools/export_onnx.py --model multilingual --out C:\tmp\promptone-test --verify

# Re-export, overwriting an existing artifact
python promptone-rust/tools/export_onnx.py --model multilingual --force

# Export a local checkpoint directory (must contain rl_agent_config.json)
python promptone-rust/tools/export_onnx.py C:\path\to\checkpoint --out onnx\my-model
```

The script refuses to overwrite `<out>/<name>/model.onnx` if it already exists.
Pass `--force` to overwrite. The check happens before any download, so a
refusal wastes no time or bandwidth.

The `--verify` flag runs a self-check against the `fixtures.npz` reference
outputs and a 36-combination shape sweep (`6 seq_lens × 2 batch_sizes × 3
marker_counts`). Skip it if you just need the files quickly; run it before
publishing.

### Step 5: Point the SDK at the export

The quickest way — no code changes.

**Single checkpoint (legacy, multilingual only):**

```powershell
# PowerShell
$env:PROMPTONE_ONNX_DIR = "C:\models\promptone\multilingual"
```

```bash
# Bash
export PROMPTONE_ONNX_DIR=/c/models/promptone/multilingual
```

**All three checkpoints at once:**

If you export all three under a common parent directory (e.g. `onnx/multilingual`,
`onnx/english`, `onnx/typed-decisions`), point `PROMPTONE_ONNX_ROOT` at the parent:

```powershell
# PowerShell
$env:PROMPTONE_ONNX_ROOT = "C:\models\promptone"
```

```bash
# Bash
export PROMPTONE_ONNX_ROOT=/c/models/promptone
```

The SDK appends the checkpoint subdirectory name automatically
(`multilingual`, `english`, or `typed-decisions`).

Use absolute paths. Relative paths depend on the working directory, which
is not reliable across test runners and hosts.

Or pass the checkpoint folder in code with `PromptOneOptions::model_directory` /
`PromptOneEngine::from_directory(...)` (see [section 7](#7-using-the-artifact-in-code-and-tests)).

### Step 6: Check it works

Run the quickstart sample against the export. From `promptone-rust/`, the default
export location is `../onnx/<checkpoint>`:

```bash
cargo run --release -p promptone-sample -- --model-dir ../onnx/multilingual
cargo run --release -p promptone-sample -- --checkpoint english --model-dir ../onnx/english
cargo run --release -p promptone-sample -- --checkpoint typed-decisions --model-dir ../onnx/typed-decisions
```

---

## 2. The three checkpoints

All three are published under the bundle repo `zameer1/promptone`; the
exporter downloads from there by default.

| Name | Bundle subfolder | Encoder | Params | max\_len | head\_max\_len | model.onnx | model.onnx.data |
|---|---|---|---|---|---|---|---|
| `english` | (root) | `answerdotai/ModernBERT-large` | 421 M | 512 | 192 | 3.2 MB | 1.57 GB |
| `multilingual` | `multilingual` | `jhu-clsp/mmBERT-base` | 322 M | 1024 | 256 | 2.5 MB | 1.20 GB |
| `typed-decisions` | `typed-decisions` | `answerdotai/ModernBERT-large` | 421 M | 1024 | 256 | 2.6 MB | 1.57 GB |

Each checkpoint also has a standalone repo (`zameer1/promptone-multilingual`,
etc.) that mirrors the same weights.

---

## 3. SDK support status

**All three checkpoints are fully supported by the Rust SDK.**

`HfTokenizer` resolves special-token ids from `tokenizer_config.json` +
`tokenizer.json` at load time, so it works for both the mmBERT-style tokens
(`<pad>=0`, `<bos>=2`, `<eos>=1`, `<unk>=3`, `<mask>=4`) and the ModernBERT
ByteLevel-BPE tokens (`[PAD]=50283`, `[CLS]=50281`, `[SEP]=50282`,
`[UNK]=50280`, `[MASK]=50284`). Select a checkpoint with `PromptOneOptions::checkpoint`
or point `PromptOneOptions::model_directory` / `PROMPTONE_ONNX_DIR` at the artifact
directory directly.

```rust,no_run
use promptone::{PromptOneCheckpoint, PromptOneEngine, PromptOneOptions};

// explicit checkpoint selection
let engine = PromptOneEngine::create(PromptOneOptions {
    checkpoint: PromptOneCheckpoint::English,
    ..Default::default()
})?;

// or point directly at the directory
let engine = PromptOneEngine::from_directory("/models/promptone/english")?;
```

---

## 4. Output layout

The Rust SDK auto-detects two different export layouts for a checkpoint directory. Whichever one
is present, only that directory needs to be pointed at — `ModelArtifacts::layout()` reports which
one it found.

### Fused layout (`export_onnx.py`, this section's exporter)

Each model is written to `<out>/<name>/`:

```
<out>/<name>/
    model.onnx              # ONNX graph (~2.5–3.2 MB, opset 18)
    model.onnx.data         # External weight sidecar (~1.2–1.6 GB)
    rl_agent_config.json    # Sequence lengths and temperature calibration
    tokenizer/
        tokenizer.json      # HuggingFace tokenizer (post_processor kept intact)
        tokenizer_config.json   # optional but recommended; fetched by download
    fixtures.npz            # PyTorch reference outputs (used by --verify; not needed by SDK)
```

Inputs: `input_ids`, `attention_mask`, `marker_pos`, `marker_mask`, `qtype` (shape `[B]`).
Outputs: `logits`, `act_logits`.

**`model.onnx` and `model.onnx.data` must stay in the same directory.** ONNX
Runtime resolves the external-data file relative to the graph file, which is
also why the API takes a *directory* rather than a file. Moving `model.onnx`
alone produces a baffling "missing initializer" error.

**Tokenizer post\_processor:** `tokenizer.json` retains its `TemplateProcessing`
post\_processor. The Rust SDK needs no stripped copy: the `tokenizers` crate
encodes without special tokens directly.

### Split layout (promptone-ts's exporter)

This is the layout produced by promptone-ts's own ONNX exporter — a graph split into a text encoder and
a small prediction head instead of one fused graph:

```
<out>/<name>/
    encoder.onnx             # text -> hidden states: inputs input_ids, attention_mask;
                             # output last_hidden_state
    encoder.onnx.data        # encoder weights (~1.2-1.6 GB)
    head.onnx                # hidden states + marker metadata -> logits: inputs hidden_states,
                             # marker_pos, marker_mask, qtype (shape [B, 1]), attention_mask;
                             # outputs logits, act_logits
    head.onnx.data           # head weights (~60-110 MB)
    rl_agent_config.json     # same schema as the fused layout
    tokenizer.json           # HuggingFace tokenizer, at the directory root (no tokenizer/ nesting)
```

Each `.data` file must stay next to its graph, for the same reason as `model.onnx.data`.

There is no sibling `tokenizer_config.json` in this layout. The SDK instead takes CLS and SEP
from `tokenizer.json`'s own `post_processor` template (`[CLS] $A [SEP]`, or `<bos> $A <eos>` for
multilingual), and resolves `pad`/`mask`/`unk` from `added_tokens` by matching the usual content
spellings (`[PAD]`/`[MASK]`/`[UNK]` or `<pad>`/`<mask>`/`<unk>`); see
`src/tokenization/hf_tokenizer.rs`. A spelling guess alone is not safe for CLS/SEP: the
multilingual vocabulary also contains `<s>`/`</s>`, which are not its CLS/SEP. `qtype`'s declared rank (`[B]` vs. `[B, 1]`) is read from each
graph's own input metadata rather than assumed from the layout, so the same binding code drives
both `head.onnx` here and `model.onnx` in the fused layout.

The two `ort` sessions (encoder, head) are run in sequence inside one `PromptOneEngine::run` call: the
encoder's `last_hidden_state` output is fed into the head's `hidden_states` input, with no
intermediate copy back into caller-visible state.

If a directory somehow has both a complete fused export and a complete split export, the fused
layout wins.

---

## 5. Where the engine looks

`ModelArtifacts::resolve` (called for you by `PromptOneEngine::create`) looks for the directory in this
order:

1. `PromptOneOptions::model_directory`
2. the `PROMPTONE_ONNX_DIR` environment variable — maps to the **multilingual** checkpoint only (legacy)
3. `PROMPTONE_ONNX_ROOT/<checkpoint-subdir>` — the recommended multi-checkpoint path (`multilingual`,
   `english`, or `typed-decisions`)
4. the local cache, `<cache_directory>/<checkpoint-subdir>`. `cache_directory` defaults to
   `%LOCALAPPDATA%\promptone\onnx` on Windows and `~/.cache/promptone/onnx` elsewhere.
5. a download into that cache, only if `PromptOneOptions::allow_download = true`

Once a directory is chosen, the SDK detects which of the two layouts from
[section 4](#4-output-layout) it holds: `model.onnx` present means fused; otherwise both
`encoder.onnx` and `head.onnx` must be present for the split layout. If a directory has neither —
or half of a split export (only one of `encoder.onnx` / `head.onnx`) — resolution fails with a
`PromptOneError::MissingFile` naming what was expected for both layouts, rather than a generic "model
not found". Downloading (step 5 above, and [section 6](#6-downloading-future-once-published))
handles either layout: it sends a `HEAD` request for `model.onnx` and fetches the fused files if it
exists, otherwise the split files if both `encoder.onnx` and `head.onnx` exist.

If none of these has the artifact, resolution fails with `PromptOneError::ArtifactsNotFound`, whose
message lists every location it tried and what to do next.

---

## 6. Downloading (future, once published)

**The ONNX artifacts have not been uploaded to HuggingFace yet.** The
`zameer1/promptone` repository ships PyTorch weights
(`model.safetensors`), which the SDK cannot load. Until the ONNX files are
uploaded, `allow_download: true` fails immediately with a 404 and an explanatory
message.

Downloading is **opt-in**. Nothing touches the network unless you ask for it.
Once the files are published (see section 8), enabling the download requires only:

```rust,no_run
use promptone::{ArtifactDownloadProgress, PromptOneEngine, PromptOneOptions};
use std::sync::Arc;

let engine = PromptOneEngine::create(PromptOneOptions {
    allow_download: true,          // fetch from Hugging Face into the cache, once
    // hugging_face_token: None is the default (falls back to HF_TOKEN); Some(String::new()) disables auth
    download_progress: Some(Arc::new(|p: ArtifactDownloadProgress| {
        eprintln!("{}: {}/{:?}", p.file_name, p.bytes_received, p.total_bytes);
    })),
    ..Default::default()
})?;
```

The SDK downloads the files of whichever layout the repository publishes (five for fused; for
split, the two graphs, their `.data` sidecars when present, `rl_agent_config.json` and
`tokenizer.json`) into the per-user cache, and skips the download on subsequent runs if the
directory is already complete:

- Windows: `%LOCALAPPDATA%\promptone\onnx\multilingual`
- Linux / macOS: `~/.cache/promptone/onnx/multilingual`

Override the cache root with `PromptOneOptions::cache_directory`.

Without the `download` cargo feature (`--no-default-features`), setting `allow_download = true`
returns a clear `PromptOneError::Download` explaining that the crate was built without it, rather than
silently failing to compile a network call.

---

## 7. Using the artifact in code and tests

### Create the engine in code

```rust,no_run
use promptone::{PromptOneCheckpoint, PromptOneEngine, PromptOneOptions};

// From a known directory (all checkpoints work)
let engine = PromptOneEngine::from_directory("/models/promptone/multilingual")?;

// English checkpoint via PromptOneOptions::checkpoint
let engine = PromptOneEngine::create(PromptOneOptions {
    checkpoint: PromptOneCheckpoint::English,
    model_directory: Some("/models/promptone/english".into()),
    intra_op_threads: Some(4),   // pin in containers with a CPU limit
    ..Default::default()
})?;
```

### Run the tests

Run from `promptone-rust/`. The tests find the repository-root `onnx/<checkpoint>`
folders by walking up from the crate, or use `PROMPTONE_ONNX_ROOT` / `PROMPTONE_ONNX_DIR`.
Checkpoints whose artifacts are missing are skipped with a `skip:` line;
model-backed tests should run in `--release`:

```bash
PROMPTONE_ONNX_ROOT=/c/models/promptone cargo test --release --workspace
```

---

## 8. Publishing to HuggingFace (maintainers only)

**Requires write access to `zameer1/promptone`.**

The SDK downloader fetches five files per checkpoint from `zameer1/promptone`.
A maintainer must upload the following paths for each checkpoint:

| Checkpoint | HF path in repo |
|---|---|
| `multilingual` | `multilingual/<files>` |
| `english` | `<files>` (bundle root — no subfolder) |
| `typed-decisions` | `typed-decisions/<files>` |

The five files per checkpoint are:
`model.onnx`, `model.onnx.data`, `rl_agent_config.json`,
`tokenizer/tokenizer.json`, `tokenizer/tokenizer_config.json`.

`onnx/` is gitignored (the weights exceed GitHub's 100 MB hard limit), so
upload must go through the HuggingFace Hub CLI or Python API directly.

Run from the repo root after completing `--verify`:

```bash
# huggingface-cli (pip install huggingface_hub)
huggingface-cli upload zameer1/promptone onnx/multilingual multilingual \
    --repo-type model --token hf_...
huggingface-cli upload zameer1/promptone onnx/english . \
    --repo-type model --token hf_...
huggingface-cli upload zameer1/promptone onnx/typed-decisions typed-decisions \
    --repo-type model --token hf_...
```

Once these files are live, `allow_download: true` with the matching checkpoint
works without any code changes.

---

## 9. Troubleshooting

**404 on download (`allow_download`)**
The ONNX artifacts have not been published yet (see section 8). Export locally with
`promptone-rust/tools/export_onnx.py` and point the SDK at the result via `PROMPTONE_ONNX_DIR` or
`PromptOneOptions::model_directory`.

**Low disk space during export**
Set `HF_HOME` to a volume with enough free space before running the exporter
(see the disk-space table in [section 1, step 3](#step-3-optional-disk-space-and-hf_home)). `--out` controls where the ONNX output lands.

**`model.onnx.data` not found / "missing initializer" in ONNX Runtime**
The sidecar must sit beside `model.onnx`. Copy both files together; the SDK
error message already reminds you of this: *"both model.onnx and model.onnx.data
must reside in the same directory."*

**"already exists, pass --force"**
`<out>/<name>/model.onnx` is present from a previous run. Use `--force` to
overwrite in place, or use `--out` to write to a separate directory.

**Wrong-checkpoint error (tokenizer/config mismatch)**
`tokenizer.json` and `tokenizer_config.json` are from different checkpoints
(e.g., the tokenizer from `english` with the config from `multilingual`). The
error message names the missing token. Keep the two files together as exported.

**Windows console encoding during export**
The dynamo exporter logs Unicode emoji (✅, etc.) that crash a non-UTF-8 Windows
console. The script reconfigures `stdout`/`stderr` to UTF-8 with replacement
automatically. Any unrecognised emoji appears as `?`; the export continues
normally.
