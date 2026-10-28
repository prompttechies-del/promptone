# Hugging Face Space (free CPU): installs PromptOne from the public repo and serves it on 7860.
FROM python:3.11-slim

ENV PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    USE_TF=0 \
    TOKENIZERS_PARALLELISM=false \
    TORCH_DISABLE_NATIVE_JIT=1 \
    OMP_NUM_THREADS=2 \
    HF_HOME=/home/user/.cache/huggingface \
    PROMPTONE_DEVICE=cpu \
    PROMPTONE_HOST=0.0.0.0 \
    PROMPTONE_PORT=7860 \
    PROMPTONE_PRELOAD=1 \
    PROMPTONE_MAX_LOADED=2

RUN apt-get update && apt-get install -y --no-install-recommends git \
    && rm -rf /var/lib/apt/lists/* \
    && useradd -m -u 1000 user

# CPU-only PyTorch keeps the image small; then PromptOne with its server extra.
RUN pip install --extra-index-url https://download.pytorch.org/whl/cpu torch \
    && pip install "promptone[serve] @ git+https://github.com/prompttechies-del/promptone.git@main"

USER user
WORKDIR /home/user
EXPOSE 7860
CMD ["promptone-serve"]
