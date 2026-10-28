# Python API

These pages are generated from the docstrings in `promptone/`, so they change with the code. Apart
from `ONNXAgent` and `promptone.train`, every name below can be imported from the top-level package,
for example `from promptone import Router`.

- [Agent](agent.md): `Agent` and `load` run one checkpoint; `ONNXAgent` runs an exported ONNX
  model.
- [Router](router.md): `Router` picks the checkpoint for each request; `RouteDecision` records
  the choice.
- [Helpers](helpers.md): language detection, email cleaning, question presets, shortlisting
  and calibration utilities.
- [Fine-tuning](train.md): `finetune` and `TrainConfig` from `promptone.train`, imported from that
  module rather than the top-level package, and the pieces they are built from.
- [LangChain components](langchain.md): `PromptOneRouter`, `PromptOneGuardrail`, `PromptOneTriage` and
  `PromptOneEvaluator`.

Prediction hooks have a hand-written [API reference](../hooks/api.md) with the rest of the
[hooks guide](../hooks/index.md).
