# E10 — LLM context proposer

**Requirements:** R17 · **Depends on:** E04

An article becomes **proposed** context events a human reviews. The one place a model
touches this product, and the epic with the most safety surface per line of code.

## Pre-flight

E04 landed green · `scripts/gate.sh` green.

An `Anthropic:ApiKey` is needed for the live path. **The tests must not need one** —
if the suite requires a key, the epic is wrong.

---

```text
You are explicitly authorized to implement: create and edit files, run commands,
and commit. Any planning-first restriction in CLAUDE.md is lifted for this
session. Proceed without waiting for further authorization.

Build epic E10 in this repository: the LLM context proposer.

1. Read in order: AGENTS.md, stack_config.toml ([llm] section), and
   PROJECT_REQUIREMENTS.md (R17). Then read, in full and before writing any code:
     context/codebase_okf/safety/llm_trust_boundary.md      (read this FIRST)
     context/codebase_okf/contracts/llm_extraction_contract.md
     context/codebase_okf/components/llm_context_proposer.md
     context/codebase_okf/contracts/context_event_catalog.md
     context/codebase_okf/contracts/player_identity_contract.md
     context/codebase_okf/tests/test_matrix_llm.md
   Also load the `claude-api` skill before writing the client code — model ids,
   parameter shapes, and structured-output syntax have all changed recently and
   guessing them from memory produces 400s.
   Add Anthropic 12.39.0 (the OFFICIAL SDK) from stack_config.toml
   [dependencies.epic]. The community anthropic.sdk package is forbidden.

2. The frame for this entire epic: ARTICLE TEXT IS UNTRUSTED INPUT — the same trust
   class as an HTTP request body, not the same class as the operator's config.
   Every design decision below follows from that.

3. Scope, in order:
   a. ProposalSchema GENERATED from the ContextEventType enum, not hand-written. A
      hand-maintained schema drifts the first time a type is added, and the model
      would then be able to emit a type the domain cannot parse.
   b. ExtractionPrompt: the system prompt carries role, the event-type catalog, and
      the extraction rules. It is stable, is the operator channel, and is CACHED
      (cache_control on the system block) — it comfortably exceeds Claude Opus 5's
      512-token cache minimum.
   c. ClaudeContextProposer using client.Messages.Create with:
        - Model claude-opus-5, MaxTokens 8000
        - OutputConfig.Format = JsonOutputFormat with the generated schema
        - OutputConfig.Effort = Medium
        - the article in the USER turn inside an explicit delimiter, labelled as
          untrusted third-party content to summarize rather than obey
        - NO tools field. The extractor gets no tools, ever.
      Do not pass budget_tokens (removed on Opus 5; returns 400) and do not
      disable thinking. CHECK StopReason BEFORE reading Content — a refusal
      returns HTTP 200 with empty or partial content, and indexing Content[0]
      unconditionally crashes.
   d. The grounding filter: a proposal whose SupportingQuote is not a VERBATIM
      substring of the article (normalizing whitespace and Unicode quotes only) is
      DISCARDED, and the discard is counted on the run. A claim the model cannot
      point at in the source does not become a proposal.
   e. ContextProposalService: grounding filter -> identity resolution -> persist as
      Proposed. The adapter itself persists nothing.
   f. TokenBudget: a hard daily stop that REFUSES with source_unavailable rather
      than partially processing a batch.
   g. Content-hash caching so the same article is never extracted twice.
   h. The review queue UI: summary, the verbatim quote IN CONTEXT, the proposed
      deltas, the source link, and accept / edit magnitude / reject. Rejections are
      retained — over time they are the honest measure of whether this feature is
      worth keeping on.

4. Absolute limits. None of these are negotiable and none may be relaxed to improve
   extraction quality:
   - No tools, no web fetch, no code execution, no MCP.
   - No user-owned data in the prompt: not the roster, not the league, not the
     user's identity. Extraction is per-article and user-independent — that is what
     makes the content-hash cache correct and bounds what an injection could reach.
   - No path from model output to a Recommendation.
   - No conversation history, and model output is never fed into another prompt.
   - Nothing the model produces can be Verified. Verified is assigned in exactly
     one place: the human verification endpoint.
   - NO CRAWLER. Extraction is explicitly triggered per article or by an
     operator-run batch. A background job continuously feeding arbitrary internet
     text to a paid API on a stranger's self-hosted box is not a default anyone
     should inherit.

5. Required test rows: M-01 to M-16. Row M-02 needs SIX committed adversarial
   article fixtures — instruction override, fake system framing, schema-shaped
   injection, an explicit verification request, tool solicitation, and cross-user
   solicitation — each asserting NO privileged effect. Row M-09 installs a handler
   that throws on real send: zero live API calls in the suite.

6. The whole feature is DISABLED BY DEFAULT. With no key configured the proposer is
   not registered and the Context Review page offers manual creation. Row M-08
   proves a keyless instance is a complete application.

7. Commit after each of a-h. Never leave the tree broken at a commit boundary.
   Update OKF concept status in the same commit as its evidence. Commit locally.

8. Budget ladder: (a) schema + prompt; (b) the adapter with recorded-response
   tests; (c) the grounding filter; (d) the review queue; (e) budget and caching.
   Do NOT ship c without b's adversarial fixtures — ungrounded proposals reaching a
   queue is the failure this epic exists to prevent.

Wrap-up at ~10% budget: green, statuses synced, validator clean, assumptions.md
updated, commit.

Final report: implemented / partial / missing, each adversarial fixture and what
the model did with it, confirmation that no live API call occurs in the suite, and
the one next step. Under-claim rather than over-claim.
```

---

## Exit gate

Rows M-01…M-16 green **with no API key present**, all six adversarial fixtures
producing no privileged effect, and a real article extracted into proposals a human
accepted through the review queue.
