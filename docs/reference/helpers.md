# Helpers

## Language detection

`promptone.detect_language` is `promptone.lang.analyse`.

::: promptone.lang.analyse

::: promptone.lang.detect_script

::: promptone.lang.is_english

## Email

::: promptone.email.clean_email_body

::: promptone.email.email_state

## Question presets

::: promptone.presets.triage_questions

::: promptone.presets.email_questions

::: promptone.presets.guard_questions

::: promptone.presets.moderation_questions

::: promptone.presets.router_questions

## Pre-run request sizing

What a request will fit before any forward pass. These are the helpers `predict_long` itself
reads, so a question can be measured without holding an Agent: `build_head` is the question half
of a sequence, `state_room` is what is left for the state after it, and `build_sequence` reports
what the head budget did to the options and to the state.

```python
from transformers import AutoTokenizer
from promptone.common import build_sequence, collapsed_options, state_room, window_budget

tok = AutoTokenizer.from_pretrained("zameer1/promptone", subfolder="tokenizer")
max_len, head_max_len = 512, 192      # from the checkpoint's rl_agent_config.json

question = {"type": "choice", "instructions": "What does the customer want?",
            "criteria": {"refund": "money back", "cancel": "stop the service"}}
# The short-key internal shape the sizing helpers read; inline the mapping as
# questions-and-answers.md shows rather than reaching into Agent._to_internal.
q = {"t": question["type"], "ins": question["instructions"], "crit": question["criteria"]}
state = "I was charged twice for the same invoice."

state_room(tok, q, max_len, head_max_len)   # state tokens this question leaves room for
seq, markers, stats, trunc = build_sequence(
    tok, state, q, max_len, head_max_len, return_stats=True, return_truncation_stats=True)
stats       # how many options the head budget kept distinct, and the cap it applied
trunc       # state tokens, used, dropped, and whether the state was truncated
collapsed_options(["q"], [{"options": stats}])   # {} until an option loses its own span
window_budget(tok, [q], max_len, head_max_len)   # (window, stride, room) a scan may use
```

The same numbers come back after a call in `usage`: `state_tokens`, `state_tokens_dropped`,
`truncated`, `truncated_questions`, and `options` when an option lost its span. That block is
the record of what a run did; the helpers above are how to find out before paying for one.
`predict_long` hands a state straight to a single pass when its tokens fit the room its
questions leave, and a scan window is capped at that room with a 50%-of-window stride -- a cap
large enough to multiply the passes warns before the scan runs.

None of this picks a strategy: tournament rounds come back as `tournament[qid]["rounds"]`
after `predict_tournament`, and shortlist ranking quality stays with whatever embedder
supplies it.

::: promptone.common.state_room

::: promptone.common.window_budget

::: promptone.common.collapsed_options

## Shortlisting

::: promptone.shortlist.shortlist_choice

::: promptone.shortlist.predict_shortlist

::: promptone.shortlist.predict_tournament

::: promptone.shortlist.embed_fn_from_agent

::: promptone.shortlist.cached_embed_fn

## Abstention

::: promptone.confidence.check_min_confidence

::: promptone.confidence.check_min_confidence_map

::: promptone.confidence.resolve_min_confidence

::: promptone.confidence.flag_low_confidence

::: promptone.confidence.apply_confidence_gate

::: promptone.confidence.GATE_STATES

## Calibration and training

::: promptone.common.answer_confidence

::: promptone.common.confidence_from_probs

::: promptone.confidence.jev_confidence

::: promptone.common.ece_score

::: promptone.calibrate.fit_temperatures

::: promptone.calibrate.fit_one_temperature

::: promptone.calibrate.fit_temperature_map

::: promptone.calibrate.fit_abstention_thresholds
::: promptone.calibrate.fit_binning_map

::: promptone.calibrate.apply_binning_map

::: promptone.agent.Agent.fit_binning

::: promptone.common.render_options

::: promptone.common.proper_reward

::: promptone.common.td_lambda_targets

::: promptone.common.QTYPES

::: promptone.common.QTYPE_NAMES
