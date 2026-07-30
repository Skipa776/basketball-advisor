# Fantasy Basketball Decision Engine

A league-aware fantasy basketball decision engine in C# / ASP.NET Core. It answers a
more useful question than *who has the highest average fantasy score*:

> Given my exact league rules, roster, opponents, available players, schedule,
> current NBA roles, and recent basketball context, what decision gives my team the
> highest expected value?

Every recommendation is explainable: structured evidence, a confidence level, and a
projection you can see decomposed into its statistical baseline and its contextual
adjustment.

> **Status: in active development.** The domain and scoring engine are implemented
> and tested; the rest is specified and being built epic by epic. See
> [Current state](#current-state) — this README describes what exists, not what is
> planned.

## Why it is built this way

**Statistics are necessary but not sufficient.** A player can post elite numbers and
then enter a materially different situation — a trade, a new primary facilitator, a
bench role, a minutes restriction. So the engine keeps two parallel evidence systems:
quantitative production and structured **context events**, and it never lets the
second overwrite the first. A projection always shows baseline, adjustment, and final
as separate numbers.

**No provider is load-bearing.** The domain never learns whether data came from an
API, an HTML scrape, a CSV, or a human typing it in. Manual entry always works, so a
dead integration degrades confidence rather than breaking the app.

**The statistics make the recommendation.** A language model may propose a context
event from a news article for a human to review. It never makes a recommendation and
has no path to one.

## Current state

| Area | State |
|---|---|
| Solution skeleton, pins, quality gate | Implemented |
| Stat vocabulary, league configuration | Implemented |
| Points and category scoring engines | Implemented |
| Persistence (EF Core, PostgreSQL) | Partial |
| Ingestion, projections, draft board, context engine | Specified, not built — epics E01–E03 |
| Everything else | Specified — epics E04–E12 |

`context/codebase_okf/assumptions.md` is the honest current-state file, including
what is deliberately unenforced.

## Stack

.NET 10 · ASP.NET Core · Blazor Server · EF Core · PostgreSQL 17 · xUnit v3 ·
Testcontainers · AngleSharp

## Running it

Requires the .NET 10 SDK and Docker.

```bash
docker compose up -d --wait     # PostgreSQL 17
scripts/gate.sh                 # format, build, test, and the bundle validator
```

A one-command self-hosted quickstart, a container image, and screenshots arrive with
epic E12.

## How this repository is organised

The specification is a first-class artifact, not documentation written after the
fact. It is what lets an agent — or a new contributor — build a subsystem correctly
without asking anyone.

| Path | What it is |
|---|---|
| `AGENTS.md` | Agent identity, read order, safety boundaries |
| `stack_config.toml` | Pins, forbidden packages and patterns, gate minimums — machine-readable |
| `PROJECT_REQUIREMENTS.md` | R1–R22 with acceptance criteria |
| `ARCHITECTURE.md` | File tree, type shapes, API routes |
| `AGENT_INSTRUCTIONS.md` | The numbered build sequence |
| `context/codebase_okf/` | The knowledge base: contracts, components, safety policies, task playbooks, test matrices |
| `context/AGENT_CONTEXT_INDEX.md` | "I am about to change X" → read these concepts first |
| `docs/epics/` | Twelve phased builds, each with a paste-ready prompt |
| `docs/design/` | The original design document |

Two rules keep it trustworthy: every canonical value (stat key, formula, threshold,
enum member) lives in exactly one file, and **every invariant names the test row,
scan, or validator check that catches its violation.** Rules that are only prose are
listed as such in `assumptions.md` rather than presented as guarantees.

```bash
python3 context/validate_okf.py    # frontmatter, statuses, links, index paths
```

## Data sources and compliance

| Role | Source | Method |
|---|---|---|
| Players, teams, schedule | balldontlie | Documented HTTP API, free tier, API key |
| Season statistics | Basketball-Reference | HTML scrape, season pages only |
| ADP | FantasyPros | HTML scrape, plus CSV and manual entry |
| League rules and rosters | Manual, CSV, Yahoo (planned) | ESPN is CSV/manual only |

Scraping is a controlled capability, not ad-hoc parsing:
`context/codebase_okf/safety/scraping_policy.md` holds a per-host allowlist with
exact permitted paths, the disallowed patterns, crawl delays, and the date each
host's robots directives were last verified. The self-imposed ceiling is **6
requests per minute** — far below every host's threshold.

Notably, `*/gamelog/` is robots-disallowed on Basketball-Reference, so per-game data
comes from `/boxscores/` via a rate-limited background importer that takes hours per
season. That constraint is designed for rather than worked around.

Terms-of-use compliance for each host is a human judgment and stays the operator's
responsibility. Read them before pointing this at a live host.

## Licence

MIT — see [LICENSE](LICENSE).
