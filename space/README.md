---
title: PromptOne
emoji: ⚡
colorFrom: indigo
colorTo: blue
sdk: docker
app_port: 7860
pinned: false
license: apache-2.0
short_description: Typed decisions in one forward pass
---

# PromptOne server

This Space runs `promptone-serve` on CPU and hosts the PromptOne playground at `/ui/`.

- Playground: open this Space's URL, then `/ui/`.
- API: `POST /v1/systemone`, `GET /health`.
- Source and docs: https://github.com/prompttechies-del/promptone

Set a Space secret named `PROMPTONE_API_KEY` to require a bearer token, and a variable named
`PROMPTONE_CORS_ORIGINS` to allow the website to call it from the browser.
