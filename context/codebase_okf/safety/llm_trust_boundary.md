---
type: safety_policy
title: LLM Trust Boundary
description: Article text is untrusted input. What the model may never receive, never be granted, and never produce — and the resulting blast radius.
tags: [safety, llm, injection, trust]
source_paths: [src/FantasyBasketball.Infrastructure/Llm]
test_paths: [tests/FantasyBasketball.IntegrationTests/Llm]
depends_on: [../contracts/llm_extraction_contract.md, data_integrity_policy.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - The worst outcome of a successful prompt injection is a rejected proposal.
  - No code path carries model output into a recommendation.
  - No user-owned data is ever sent to the model.
---

# Responsibility

Owns the boundary around the single model call in this product. **Article text is
untrusted input written by a third party** — the same trust class as an HTTP
request body, not the same class as the operator's configuration. Everything here
follows from that.

Call mechanics are owned by
[llm_extraction_contract](../contracts/llm_extraction_contract.md). This file owns
what must never happen. **You may not weaken it to make extraction work better.**

# The trust ladder

Descending order of trust. Nothing may be promoted a rung without a human.

```text
manual entry by the user          authoritative — the user owns their league
        ↓
documented API (balldontlie)      confidence 0.95
        ↓
permitted HTML scrape             confidence 0.85, parser-tested
        ↓
LLM proposal from an article      Proposed only — never applied as fact
```

Model output sits at the bottom **on purpose**. It is the only rung produced by
something that can be persuaded by its own input.

# What the model never receives

- **No user-owned data.** Not the roster, not the league settings, not other
  context events, not the user's identity. Extraction is per-article and
  user-independent.
- **No conversation history.** Every call is one article, one response.
- **No output from a previous model call.** Model output is never fed into another
  prompt — that is how a single injection becomes a persistent one.

The first rule is why the content-hash cache is correct, and it also bounds
exfiltration: there is nothing private in the request to exfiltrate.

# What the model is never granted

- **No tools.** No web fetch, no search, no code execution, no file access, no MCP
  server. `tools_granted = []` in
  [`stack_config.toml`](../../../stack_config.toml) and the request carries no
  `tools` field.
- **No free-form output.** The response is schema-constrained
  (`OutputConfig.Format`). There is no prose channel to smuggle instructions
  through and nothing for a downstream parser to be tricked by.
- **No write path.** The proposer returns values; it does not persist. Persistence
  goes through `ContextProposalService`, which only ever writes `Proposed`.

# What the model can never produce

- **`VerificationState.Verified`** — absent from the schema, and assigned in
  exactly one place in the codebase: the human verification endpoint
  ([context_event_catalog](../contracts/context_event_catalog.md)).
- **A `Recommendation`.** There is no code path from model output into the
  recommendation engine. Statistical and optimization code makes every
  recommendation; the model contributes, at most, a proposed input that a human
  accepted first.
- **A player link.** Names resolve through
  [player_identity_contract](../contracts/player_identity_contract.md); ambiguity
  becomes a pending match for a human.
- **An ungrounded claim.** A proposal whose supporting quote is not verbatim in
  the article is discarded.

# Blast radius

Stating this plainly is the point of the design. Assume a malicious article and a
fully successful prompt injection. The attacker's maximum achievable effect is:

**one or more bogus `Proposed` context events, attributed to that article, sitting
in a review queue, which a human then rejects.**

They cannot verify an event, cannot alter a projection without human acceptance,
cannot reach a recommendation, cannot read another user's data, cannot call a
tool, cannot execute code, cannot persist anything beyond that queue, and cannot
influence the next extraction. The grounding check means they must also have
written the claim into the visible article text, where the reviewer sees it.

That bounded radius is what makes it acceptable to point a language model at
arbitrary internet text at all — not the prompt wording, which is the part an
attacker gets to argue with.

# Cost is a safety property

An unbounded model integration in a self-hosted app is a way for a scraped feed to
spend someone else's money. `daily_token_budget` is a hard stop that refuses
rather than degrading, usage is recorded per article, and the whole feature is
**disabled by default**.

# Invariants

- **Injection fixtures achieve nothing privileged.** *Check:
  [test_matrix_llm](../tests/test_matrix_llm.md) row M-02, with committed
  adversarial articles: instruction override, fake system framing, a schema-shaped
  payload embedded in prose, and an attempt to set verification.*
- **No `tools` field in any request.** *Check: row M-04.*
- **No user-owned data in any request body.** *Check: row M-10.*
- **No code path from model output to `Recommendation`.** *Check: row M-11 — a
  reference-graph assertion, not a grep, so an indirect path also fails.*
- **`Verified` is assigned in exactly one place.** *Check: row M-01 and the
  single-assignment-site grep.*
- **Model output is never used to build another prompt.** *Check: row M-12.*
- **Budget exhaustion refuses.** *Check: row M-07.*

# Change procedure

`stable_contract`. Granting the model a tool, adding user data to the prompt,
allowing free-form output, or creating any path from model output to a
recommendation requires explicit user approval and an entry in
[assumptions](../assumptions.md) recording what was traded away. Adding an
adversarial fixture requires no approval and is always welcome.

# Verification

[test_matrix_llm](../tests/test_matrix_llm.md), rows M-01, M-02, M-04, M-07,
M-10, M-11, M-12.
