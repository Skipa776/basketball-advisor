# Functional app implementation solution

Updated: 2026-09-22. This plan implements the owner-requested
[`functional-app-roadmap.md`](functional-app-roadmap.md) while preserving the
existing redesign and Blazor routes during migration. The repository's contracts
and safety policies remain authoritative where the roadmap is stale.

## Outcome and order

1. Verify the uncommitted visual refresh with the full gate and commit it as a
   coherent checkpoint. Preserve every existing user edit.
2. Complete the React path using the existing same-origin `api.ts` client: an
   owner-only Data Sources page (four import actions, runs, freshness), saved
   drafts with reopen, league settings and category setup, context review, and
   account export/import/delete. Put links to every shipped page in the signed-in
   navigation; route unknown paths to a useful page. Keep draft rows stable and
   all actions keyboard accessible. Unsupported trade, streaming, and standings
   pages identify R15, R14, and the deferred roadmap entry without fabricated
   results. Existing Blazor routes keep working until parity is verified.
3. Add `GET /api/drafts?leagueId=` with owner-scoped paging. Add league identity,
   cadence, and roster editing with a clear rule for active drafts: changing team
   count or roster slots is refused while a draft exists; name and cadence edits
   remain available. Keep scoring edits separate and require explicit projection
   recalculation. Add HTTP cross-user and mutation tests with each endpoint.
4. Make imported data actionable through the owner screen. Keep the required
   BallDontLie key validated at startup, then show missing rows, running, failed,
   stale, and ready states truthfully. Never ship
   credentials or a test fixture as production player data. Use a scratch database
   for a live import if keys and network access are available; otherwise record
   that end-to-end live verification is pending.
5. Add the new **player labels** and pre-draft distribution feature to product
   and technical documentation. The canonical planned vocabulary, played-game
   percentile interpretation, separate availability, injury-evidence rule,
   unresolved thresholds, and required offline goldens now live in
   [player_season_intelligence_contract](../../context/codebase_okf/contracts/player_season_intelligence_contract.md).
   Keep the current draft-value contract until a new calculation is approved and
   calibrated; do not silently change its weights.

## Provider decision

The owner requests swar/nba_api as the main player-stat source. It is a Python
client and endpoint reference for NBA.com, not a hosted API. The C# app can use
the documented NBA.com endpoints through its existing `IHttpClientFactory` path,
subject to source terms, robots/rate review, identity mapping, offline fixtures,
and an approved provider-contract update. A Python sidecar would add a new
runtime and packages and needs an explicit choice. No live NBA.com fetch is
enabled before the source policy is resolved.

**Source review on 2026-09-22:** [NBA.com Terms of Use, section 9](https://www.nba.com/termsofuse)
restrict use of NBA Statistics with a fantasy game and with a comprehensive,
regularly updated statistics database without prior consent. The documented
NBA.com endpoints therefore are **not an approved production source for this
fantasy application** on the evidence available. Obtain appropriate NBA rights
or select a provider whose terms cover this use before making NBA.com the main
feed. swar/nba_api's open source license does not grant rights to NBA.com data.

The [FantasyNerds NBA API](https://api.fantasynerds.com/docs/nba) documents
`draft-rankings` and `draft-projections`, but no NBA ADP endpoint. A rank is not
an average pick. Keep the existing FantasyPros/CSV/manual ADP field accurate;
FantasyNerds rankings can be shown as a distinct market rank if the owner chooses
that integration and provides a configured key. Never rename rankings to ADP.

The saved Basketball-Reference box-score parser has no approved live import path:
the stable path allowlist and prose conflict. The offline pipeline can proceed,
but no live scrape bypasses the URL builder or changes the safety table without
an explicit source-policy decision. NBA.com game logs may offer an alternative
after their own source-policy and compatibility review.

## Acceptance evidence

- `scripts/gate.sh` green after each completed subsystem, with contracts, concept
  status, tests, and implementation in the same commit.
- Browser journey opens every React navigation target, including signed-out,
  non-owner, owner, empty, and error states. No broken links or unknown-route
  dead ends; axe and 390/320px checks pass.
- Saved drafts and settings are owned by the signed-in user; a second account
  receives 404 for another user's resources.
- Import status and player values use real provenance. No production label or
  percentile appears without adequate recorded samples.
- Run ponytail-review on the final diff and remove justified complexity before
  handoff. Owner performs the final visual and live-data test.
