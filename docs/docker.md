# Docker quickstart

Run the SDK without installing Python or PyTorch on your host. For the CPU
quickstart, allow 8 GB of RAM and 10 GB of free disk, with Docker Engine or
Docker Desktop and Compose v2 or newer.

From the repository root:

```bash
docker compose run --build --rm promptone
```

This builds the checkout, runs the [sample request](https://github.com/prompttechies-del/promptone/blob/main/examples/docker/request.json)
on CPU and prints JSON covering `choice`, `score` and `noul`. The first request
downloads the selected public Hugging Face checkpoint; no account is needed.
Allow several minutes for its first download.
Weights stay in a named volume. Subsequent runs use `docker compose run --rm promptone`.

Predictions and confidence still need evaluation on your workload. See the
[benchmark limits](https://github.com/prompttechies-del/promptone/blob/main/BENCHMARKS.md).

For ARM64 hosts, DGX Spark and Apple Silicon, see
[ARM64 and DGX Spark containers](docker-platforms.md).

## NVIDIA GPU / CUDA

Install a compatible NVIDIA driver and configure Docker with the
[NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html).
The GPU image uses PyTorch CUDA 13.0 wheels. Check your GPU's compute capability
and driver against [PyTorch's supported builds](https://pytorch.org/get-started/locally/);
older cards may require a different build. Allow additional disk space for CUDA
layers. VRAM needs depend on the checkpoint, batch size and input length.

```bash
docker compose -f compose.yaml -f compose.cuda.yaml run --build --rm promptone
```

The override selects GPU `0` and defaults to `PROMPTONE_DEVICE=cuda`. Set
`PROMPTONE_GPU_ID` to another host index or UUID. That GPU appears as device `0`
inside the container. Check access without downloading weights:

```bash
docker compose -f compose.yaml -f compose.cuda.yaml run --rm promptone python -c \
  'import torch; assert torch.cuda.is_available(); print(torch.cuda.get_device_name(0)); print(torch.ones(1, device="cuda").cpu())'
```

The sample rejects unavailable CUDA before loading a checkpoint. PromptOne can still
fall back to CPU after a memory or inference error, so inspect its warnings.
Rebuild when switching between CPU and CUDA configurations.

The image sets `TORCH_DISABLE_NATIVE_JIT=1`. PyTorch 2.14 otherwise replaces some eager
CUDA ops with Triton kernels that it compiles on the first inference, which needs a C
compiler the slim image does not carry: the container reports healthy and then fails every
request (#365). The stock kernels give the same answers at the same latency. Set the same
variable on a bare-metal install if `predict` fails with `Failed to find C compiler`.

This uses [Compose GPU reservations](https://docs.docker.com/compose/how-tos/gpu-support/).
Windows requires Docker Desktop's supported WSL2 GPU setup. Apple MPS,
AMD/ROCm and Intel GPU containers are outside this quickstart; use CPU unless
you configure and validate another backend.

## Configuration

Set Compose variables in your shell, a local `.env` file, or the service's
`environment` block. Don't commit secrets in `.env`. Runtime variables also
work with `docker run -e`; Compose-only settings are identified below.

| Variable | Default | Purpose |
| --- | --- | --- |
| `PROMPTONE_DEVICE` | `cpu` / `cuda` | Device selected by the base / GPU configuration |
| `PROMPTONE_CUDA_AMP` | unset (checkpoint's `amp_dtype`) | `fp16`/`float16` or `bf16`/`bfloat16` for the CUDA forward; anything else is ignored. Not cosmetic: the README's threshold section measures bf16 flipping 3 of 864 argmaxes on the parity set where fp16 flips none |
| `PROMPTONE_CPU_AMP` | unset | `bf16` or `bfloat16` opts the CPU forward into bf16; anything else leaves it fp32. No fp16 spelling turns it on either: CPU autocast has no fp16 fast path that beats fp32, so bf16 is the only reduced precision core offers on this device |
| `PROMPTONE_MODEL` | `auto` | Router alias: `auto`, `english`, `multilingual`, `typed-decisions` |
| `PROMPTONE_MODEL_PATH` | unset | Compatible checkpoint path inside the container |
| `PROMPTONE_REVISION` | unset | Hub commit, branch or tag used for every checkpoint download, or `reviewed` for the reviewed SHAs in `promptone/revisions.py`; a `revision=` argument still wins |
| `PROMPTONE_SHA256_DIGESTS` | unset | JSON digests verified before weights are parsed and loaded: `{artifact: digest}` for every checkpoint, or `{model: {artifact: digest}}` per checkpoint. See [Security](security.md) |
| `PROMPTONE_REQUEST_FILE` | bundled request | JSON request path inside the container |
| `OMP_NUM_THREADS` | `4` | CPU threads; keep within available cores |
| `HF_TOKEN` / `HF_TOKEN_FILE` | unset | Optional Hugging Face credential |
| `PROMPTONE_API_KEY` / `PROMPTONE_API_KEY_FILE` | unset | **`promptone-serve` only:** require `Authorization: Bearer <key>` |
| `PROMPTONE_PORT` | `8000` | **`promptone-serve` only:** container port, and the host port published for it |
| `HF_HUB_OFFLINE` | `0` | `1` uses only cached checkpoints |
| `HF_HOME` | `/home/promptone/.cache/huggingface` | Cache path; see mount requirement below |
| `PROMPTONE_CACHE_VOLUME` | project model cache | **Compose only:** named cache volume |
| `PROMPTONE_GPU_ID` | `0` | **Compose only:** NVIDIA device index or UUID |
| `PROMPTONE_TORCH_INDEX` | `cu130` (CUDA) / `cpu` | **Compose build:** PyTorch wheel index. `cu126` also carries the pinned torch; `cu128` does not |
| `PROMPTONE_TORCH_VERSION` | `2.14.0` | **Compose build:** pinned PyTorch version |

Compose forwards the runtime variables except `HF_HOME`, which stays aligned
with its fixed cache mount, and except `PROMPTONE_MPS_AMP_MIN_ROWS`, the MPS row gate,
which no image here can reach because no container here can select MPS.
If overriding `HF_HOME` in `docker run` or your own
Compose file, provide a matching mount writable by UID 10001. Direct Docker
builds select PyTorch with `--build-arg TORCH_INDEX=cu130`; runtime `-e` cannot
change the installed wheel.

```bash
PROMPTONE_MODEL=english OMP_NUM_THREADS=2 docker compose run --build --rm promptone

docker build -t promptone:local .
docker run --rm -e PROMPTONE_MODEL=english -e OMP_NUM_THREADS=2 \
  -v promptone-model-cache:/home/promptone/.cache/huggingface promptone:local
```

For your own request:

```bash
docker compose run --rm --volume "$PWD/request.json:/inputs/request.json:ro" \
  --env PROMPTONE_REQUEST_FILE=/inputs/request.json promptone
```

For a commented configuration with request, checkpoint and secret-file mounts,
see [`compose.example.yml`](https://github.com/prompttechies-del/promptone/blob/main/compose.example.yml):

```bash
docker compose -f compose.yaml -f compose.example.yml run --build --rm promptone
```

Add `-f compose.cuda.yaml` before `run` for NVIDIA GPUs. The example is an
override of `compose.yaml`, so cache and image settings stay in one place.

## Secret files

`HF_TOKEN_FILE` reads a mounted UTF-8 file at startup, trims surrounding
whitespace and takes precedence over `HF_TOKEN`. Unreadable, empty or invalid
files stop startup without printing their contents. The file must be readable
by UID 10001. `_FILE` applies only to supported secrets, not every setting.

With `HF_TOKEN_PATH` pointing to an existing host file outside the checkout:

```bash
docker compose run --rm --volume "$HF_TOKEN_PATH:/run/secrets/hf_token:ro" \
  --env HF_TOKEN_FILE=/run/secrets/hf_token promptone
```

Docker secrets or Kubernetes Secret volumes can supply the same file. Values
are loaded into the process environment at startup; restart after changing a
file. Never use tokens as build arguments or bake them into images. Public
checkpoints need no token.

## Fine-tuned checkpoints

This image runs inference. Fine-tuning happens outside it — the
[fine-tuning notebook](https://github.com/prompttechies-del/promptone/blob/main/notebooks/promptone_finetune_typed_decisions_2xT4_kaggle.ipynb)
runs the whole loop on Kaggle's free 2xT4 GPUs and exports a checkpoint this image can
serve. Background and open questions about the training interface stay in
[#4](https://github.com/prompttechies-del/promptone/issues/4) and
[#26](https://github.com/prompttechies-del/promptone/issues/26).

Point `PROMPTONE_CHECKPOINT_PATH` to an absolute host directory containing
`rl_agent_config.json`, `model.safetensors` and matching tokenizer files:

```bash
docker compose run --rm --volume "$PROMPTONE_CHECKPOINT_PATH:/models/custom" \
  --env PROMPTONE_MODEL_PATH=/models/custom promptone
```

Use a working copy writable by UID 10001 because the loader may update tokenizer
configuration. A LoRA adapter alone is not a complete checkpoint. Leave
`PROMPTONE_MODEL=auto` when setting `PROMPTONE_MODEL_PATH`; an explicit alias and local path
are mutually exclusive. The local-path response comes from the Agent and has no
Router `routing` metadata. These settings also work with the CUDA override.
Evaluate fine-tuned checkpoints on held-out examples before relying on them.

## Models from ModelScope

When a host cannot reach huggingface.co, the checkpoint can come from
[ModelScope](https://modelscope.cn) and be baked into the image at build time. One
argument selects which checkpoint, and it defaults to the multilingual one. The
Compose override adds the prefetch arguments to both services and keeps the
container off the Hub:

```bash
docker compose -f compose.yaml -f compose.http.yaml -f compose.modelscope.yaml up --build promptone-serve
```

For NVIDIA, add `-f compose.cuda.yaml` before `up`; it repeats its own arguments for
both services, so the ordering between the two overrides does not matter. Plain
Docker takes the arguments directly:

```bash
docker build --build-arg MODELSCOPE_MODEL=multilingual \
  -t promptone:local .
docker run --rm -e HF_HUB_OFFLINE=1 -p 127.0.0.1:8000:8000 promptone:local promptone-serve
```

One prerequisite on a host that has run this before. The baked weights land in
`$HF_HOME/hub` inside the image, under the cache directory `compose.yaml` mounts
the `model-cache` volume at (`/home/promptone/.cache/huggingface`), and Docker seeds a
named volume from the image only while that volume is empty. A volume left over
from the Hub-based quickstart holds the older Hub snapshot, it is never
re-seeded, and the baked weights stay invisible behind it: the loader resolves
`refs/main` to the old Hub commit and the container answers with the weights
that were already downloaded, as if the rebuild had changed nothing. Point the
deployment at an empty cache volume -- `docker compose down --volumes` with the
same Compose files and the same `PROMPTONE_CACHE_VOLUME`, or `PROMPTONE_CACHE_VOLUME=<name>`
for a fresh one. With `HF_HUB_OFFLINE=1` a missing or divergent ref is a load
failure with no network to fall back to, but the prerequisite is the same.

[`docker/prefetch_modelscope.py`](https://github.com/prompttechies-del/promptone/blob/main/docker/prefetch_modelscope.py)
lists the repository on modelscope.cn, downloads the checkpoint's own files -- the
same set `promptone/agent.py` asks the Hub for, so no sibling checkpoint is pulled -- and
writes them into the image's hub cache the way `snapshot_download` lays out a
snapshot. Nothing else changes: `Agent`, the `Router` that `promptone-serve` builds,
`promptone.cli` and the integrations keep their repo ids and resolve them to the baked
snapshot, so a container built this way needs no network at all. Each file's size
is checked against what the repository reports before the snapshot is published,
and its SHA-256 as well when the repository publishes one. A size or digest
mismatch fails the build. A repository that publishes no digest leaves the
check to size alone, which cannot detect a same-size substitution.

| Variable | Default | Purpose |
| --- | --- | --- |
| `MODELSCOPE_MODEL` | `multilingual` (Compose); empty in the Dockerfile | Checkpoint to bake: `multilingual`, `english`, `typed-decisions` or `all`. Empty means no prefetch and the unchanged image |
| `MODELSCOPE_REVISION` | `master` | ModelScope branch, tag or commit to bake |
| `HF_HUB_OFFLINE` | `1` in the Compose override | `1` never contacts the Hub, so the baked copy is served |

A type expands to that checkpoint's path inside the bundled repository, which is what
the `Router` and the one-shot quickstart load by default, so a build that names one
type serves it with no further change:

```bash
MODELSCOPE_MODEL=english docker compose -f compose.yaml -f compose.http.yaml -f compose.modelscope.yaml up --build promptone-serve
MODELSCOPE_MODEL=all docker compose -f compose.yaml -f compose.http.yaml -f compose.modelscope.yaml up --build promptone-serve
```

`all` is the whole family, about 2.4 GB of weights. The override also sets
`PROMPTONE_MODELS=multilingual`, because `PROMPTONE_PRELOAD=1` with the default list would try
to build every checkpoint and fail on the first one that was not baked; set
`PROMPTONE_MODELS` to the list you baked when you bake more, and `MODELSCOPE_MODEL=all`
when a deployment really does serve the family.

Several types can be named at once -- `MODELSCOPE_MODEL="english multilingual"`
bakes both, about 1.5 GB -- which is what a serving image usually wants: the
`Router` chooses between the English and the multilingual checkpoint on its own,
and one it was not given answers `500 inference failed` with
`does not contain 'rl_agent_config.json'` in the log. Checkpoints that share a
repository always bake into one snapshot, because a cached revision resolves to a
single directory; the root checkpoint and each subfolder are both in it.

Beyond the types the argument also takes `repo[:subfolder]` specs, comma- or
space-separated, which is how the mirror's standalone repositories
([promptone](https://modelscope.cn/models/zameer1/promptone),
[promptone-multilingual](https://modelscope.cn/models/zameer1/promptone-multilingual),
[promptone-typed-decisions](https://modelscope.cn/models/zameer1/promptone-typed-decisions))
or a fine-tuned checkpoint is baked. A standalone repository is what
`Agent("zameer1/promptone-multilingual")` loads directly; the `Router`'s
default is the bundled path, so a type is normally what a serving image wants.

Two details about pins and provenance. The build prints the commit the snapshot is
keyed by, which is the tip of the revision that was baked -- pass that SHA as
`revision=` or `PROMPTONE_REVISION` to pin a load to exactly what was baked. A mirror
repository can hold files from several uploads, so that tip is the one
repository-wide key there is. Hub-side pins do not describe a mirror snapshot:
`reviewed` names Hugging Face commits, and the SHA-256 digest map is keyed to Hub
artifact hashes, so neither applies here, and no digest pin exists for a mirror
snapshot either. The build already refuses a download that does not match the
mirror's own reported size -- and its digest, when the mirror publishes one --
but this checks consistency with mirror metadata, not an independently pinned
digest. The weights come from
whichever mirror account the argument names, which is its own supply-chain
decision for the deployment to make.

## Development and cleanup

Open a Python prompt with `docker compose run --rm promptone python`. To run the
existing routing/criteria checks and secret-file tests against your checkout
without downloading weights:

```bash
docker compose run --rm --volume "$PWD:/workspace:ro" --workdir /workspace promptone \
  sh -ec 'python tests/test_router.py; python tests/test_criteria.py; python tests/test_docker_entrypoint.py'
```

Rebuild with `--build` after changing source or the bundled example. The image
runs as UID/GID 10001. New named volumes inherit the image cache directory's
ownership; host directories must be writable by that UID. Keep model caches
writable for tokenizer compatibility updates.

`--rm` removes completed containers. `docker compose down` retains the cache.
To **delete downloaded weights**, run `docker compose down --volumes` using the
same Compose files and `PROMPTONE_CACHE_VOLUME` setting. The next request downloads
them again; don't remove a cache shared with another project.

## HTTP serving

The image ships `promptone-serve`, so the same build that runs the one-shot quickstart can
serve the Jev-compatible API. `compose.http.yaml` adds it as a second service and leaves
`promptone` alone:

```bash
docker compose -f compose.yaml -f compose.http.yaml up --build promptone-serve
curl -s localhost:8000/health
curl -s localhost:8000/v1/systemone -H 'content-type: application/json' \
  --data @examples/docker/request.json
```

For NVIDIA, add the CUDA override. It repeats the build args and the device reservation
for `promptone-serve`, because `promptone-serve` is a separate service and overrides for `promptone`
never reach it:

```bash
docker compose -f compose.yaml -f compose.http.yaml -f compose.cuda.yaml up --build promptone-serve
```

`up` keeps the service running in the foreground; `-d` detaches. Weights go to the same
named `model-cache` volume as the quickstart, so serving after a quickstart run starts
with the checkpoints already on disk. Stop with `docker compose ... down`, using the same
Compose files.

The port is published on `127.0.0.1` only. The API has no authentication until
`PROMPTONE_API_KEY` is set, so set a key before exposing it with
`PROMPTONE_BIND_ADDRESS=0.0.0.0`, and put a TLS reverse proxy in front for remote clients.
`/health` does not require authentication in either case, so the healthcheck below keeps
working; with a key set it answers an unauthenticated caller `{"status": "ok"}` and withholds the
checkpoint, revision and device fields, which need the bearer.

The service has a healthcheck on `/health`. `docker compose ... up -d --wait promptone-serve` waits for HTTP liveness. With the Compose default `PROMPTONE_PRELOAD=0`, it can return with no checkpoint loaded; the first valid inference request loads the checkpoint it needs. With `PROMPTONE_PRELOAD=1`, the server builds the selected checkpoints before listening, but preloading does not run a model forward.

These are three separate checks: `/health` confirms the HTTP process responds, an authenticated `/health` response lists resident checkpoints, and a successful authenticated `POST /v1/systemone` confirms that the requested inference ran. A healthy container or a nonempty `loaded` list alone does not establish successful inference. The Docker workflow's HTTP job checks startup, loopback publication and authentication with preload disabled and invalid request bodies; it does not run a model forward.

`/health` reports `device` as the device a resident checkpoint actually computes
on, which is not always what `PROMPTONE_DEVICE` asked for: a checkpoint that wants a
GPU it cannot get falls back to CPU silently and still answers correctly.
`checkpoint_devices` names each loaded checkpoint, and `device_is_preference` is
`true` only while nothing is resident, so a deployment that quietly lost its GPU
says so instead of echoing its own configuration back.

### Server configuration

These apply to the `promptone-serve` service only.

| variable | default | effect |
|---|---|---|
| `PROMPTONE_HOST` | `0.0.0.0` | bind address inside the container |
| `PROMPTONE_PORT` | `8000` | container port, and the host port published for it |
| `PROMPTONE_BIND_ADDRESS` | `127.0.0.1` | host address the port is published on |
| `PROMPTONE_PRELOAD` | `0` | `1` builds every checkpoint at startup instead of on first request |
| `PROMPTONE_MODELS` | (all) | comma list to preload: `english,multilingual,typed-decisions` |
| `PROMPTONE_THREADS` | `OMP_NUM_THREADS` | caps torch intra-op threads; keep at or below physical cores |
| `PROMPTONE_AUTO_TASK` | `0` | `1` lets the router reach `typed-decisions` automatically |
| `PROMPTONE_DEFAULT_MODEL` | `multilingual` | Checkpoint a state with no language evidence falls back to (no letters, or Latin text too short to identify). Set `english` for mostly English traffic; an unresolvable name stops the container at startup instead of serving a configuration nobody asked for |
| `PROMPTONE_EXTRA_MODELS` | (none) | JSON object `{name: source}` registering extra checkpoints beside the bundled ones: a Hub repo id or local checkpoint directory as a string, or a `["repo", "subfolder"]` pair |
| `PROMPTONE_IDLE_UNLOAD_SECONDS` | `0` | unload resident checkpoints after this many idle seconds; the next request loads its checkpoint again. Zero disables unloading |
| `PROMPTONE_MAX_LOADED` | `2` | Checkpoints kept resident; `PROMPTONE_AUTO_TASK` makes a third reachable on demand, and a cap below what routing chooses rebuilds one per switch |
| `PROMPTONE_MAX_CONCURRENT` | `16` | requests admitted at once; later ones get `503` (a value that does not parse, or is not positive, falls back to `16`) |
| `PROMPTONE_LOG_LEVEL` | `info` | uvicorn log level |
| `PROMPTONE_API_KEY` | (none) | when set, requires `Authorization: Bearer <key>` |
| `PROMPTONE_ROOT_PATH` | (empty) | public URL prefix for FastAPI when behind a reverse proxy; the proxy should strip it before forwarding |
| `PROMPTONE_MAX_TOKEN_BUDGET` | `8192` | cap on per-request `max_len` and `head_max_len` overrides |
| `PROMPTONE_MAX_BATCH_TOKENS` | `131072` | tokens one `/v1/systemone/batch` forward pass may collate (`states` × questions × row width); a larger batch is split into several passes, not refused |
| `PROMPTONE_JEV_STRICT` | `0` | `1` serves the strict Jev wire contract (no root `routing`, no per-answer `action` / `answer_confidence`, reduced `usage`) for strict Jev clients |
| `PROMPTONE_TOURNAMENT_GROUP` | `0` | answer a `choice` with more options than this by elimination in groups of this size (2 to 100), on `/v1/systemone`, and raise its option cap from 100 to 255. See [Large choices](http-api.md#large-choices-promptone_tournament_group). Unset, or outside 2 to 100, is off |
| `PROMPTONE_SHA256_DIGESTS` | (none) | JSON digests checked before a checkpoint is parsed: `{artifact: digest}` for every checkpoint, or `{model: {artifact: digest}}` per checkpoint. See [Security](security.md) |

For example, set `PROMPTONE_ROOT_PATH=/promptone` when publishing the API under `/promptone`. The proxy must
strip that prefix before forwarding to the container; this setting updates FastAPI's generated
URLs and does not change the internal `/health` or `/v1/systemone` routes.

`PROMPTONE_PRELOAD` defaults to `0` here rather than the package default of `1`, because
preloading makes the first boot download all three checkpoints. Set it to `1` for a
long-running deployment so the first request does not pay for the build.

`PROMPTONE_PORT` sets both the published host port and the port the server binds, so the two
cannot drift. Change one place to move the service:

```bash
PROMPTONE_PORT=9000 docker compose -f compose.yaml -f compose.http.yaml up --build promptone-serve
```

### Bearer token from a file

`PROMPTONE_API_KEY_FILE` is read once at startup, moved into `PROMPTONE_API_KEY`, and the `_FILE`
variable is removed before the server execs. Prefer this to putting the key in the
environment:

```bash
docker compose -f compose.yaml -f compose.http.yaml run --rm \
  --volume "$PWD/promptone_api_key:/run/secrets/promptone_api_key:ro" \
  -e PROMPTONE_API_KEY_FILE=/run/secrets/promptone_api_key \
  --service-ports promptone-serve
```

