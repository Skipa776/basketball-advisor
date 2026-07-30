---
type: component
title: LLM Context Proposer
description: The Claude extraction adapter, the grounding filter, the review queue it feeds, and its optionality.
tags: [component, llm, context]
source_paths: [src/FantasyBasketball.Infrastructure/Llm, src/FantasyBasketball.Application/Context/ContextProposalService.cs]
test_paths: [tests/FantasyBasketball.IntegrationTests/Llm]
depends_on: [../contracts/llm_extraction_contract.md, ../safety/llm_trust_boundary.md, context_engine.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
done_criteria:
  - The proposer is one adapter behind one interface, removable without trace.
  - Every proposal reaches a human before it affects a number.
---

# Responsibility

Owns the adapter that turns an article into proposed context events. The call, the
schema, and the grounding check are owned by
[llm_extraction_contract](../contracts/llm_extraction_contract.md); the
non-negotiables by
[llm_trust_boundary](../safety/llm_trust_boundary.md).

# Design

```text
IContextEventProposer          ← Application abstraction, one method
  ClaudeContextProposer        ← the only implementation
    ExtractionPrompt           ← system prompt: role + catalog + rules (cached)
    ProposalSchema             ← the JSON schema, generated from ContextEventType
    TokenBudget                ← daily cap, refuses at the limit
```

- `ProposalSchema` is **generated from the `ContextEventType` enum**, not written by
  hand. A hand-maintained schema drifts from the catalog the first time a type is
  added, and the model would then be able to emit a type the domain cannot parse.
- `ContextProposalService` (Application) owns the pipeline: grounding filter →
  identity resolution → persist as `Proposed`. The adapter itself persists nothing.
- The proposer is registered only when a key is configured. Absent, the interface has
  no implementation and the Context Review page shows manual creation — the same
  optionality pattern as the provider adapters.
- Extraction is **triggered explicitly**, per article, from the review UI or an
  operator-run batch. There is no crawler and no automatic feed: a background job
  that continuously feeds arbitrary internet text to a paid API on a stranger's
  self-hosted box is not a default anyone should inherit.

# The review queue is the product

The interesting surface is not the extraction — it is the queue. For each proposal
the reviewer sees the summary, the **verbatim supporting quote in context**, the
proposed deltas, and the source link, and can accept, edit the magnitude, or reject.
Rejections are retained
([context_event_catalog](../contracts/context_event_catalog.md)), which over time is
the honest measure of whether extraction is worth keeping on.

# Invariants

- **`ProposalSchema` is generated from the enum.** *Check:
  [test_matrix_llm](../tests/test_matrix_llm.md) row M-13 asserts the schema's enum
  values equal `ContextEventType`'s members exactly.*
- **The adapter persists nothing.** *Check: its return type and row M-14.*
- **Everything persisted is `Proposed`.** *Check: row M-01.*
- **No key means no registration and a working app.** *Check: row M-08.*
- **No crawler exists** — extraction is only ever explicitly triggered. *Check: row
  M-15 asserts no `BackgroundService` references the proposer.*
- **Ungrounded proposals never reach the queue.** *Check: row M-03.*
- **Rejected proposals are retained with their source.** *Check: row M-16.*

# Change procedure

Adding a `ContextEventType`: the catalog only — the schema regenerates and row M-13
proves it. Changing the prompt: the contract, the prompt, the adversarial fixtures,
and a fixture-suite re-run.

# Verification

[test_matrix_llm](../tests/test_matrix_llm.md), rows M-01 through M-16.
