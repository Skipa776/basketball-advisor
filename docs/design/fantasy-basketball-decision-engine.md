# Fantasy Basketball Decision Engine — Design Document

**Status:** Planning / pre-implementation  
**Primary implementation language:** C#  
**Primary platform:** .NET / ASP.NET Core  
**Architecture direction:** Modular monolith with provider adapters  
**Working objective:** Build a fantasy-basketball decision engine that combines league-specific scoring, current NBA statistics, schedules, roster availability, draft state, and qualitative basketball context to produce explainable draft, waiver, streaming, trade, and roster recommendations.

---

## 1. Product Vision

The application should answer a more useful question than “Who has the highest average fantasy score?”

> **Given my exact league rules, roster, opponents, available players, schedule, current NBA roles, and recent basketball context, what decision gives my team the highest expected value?**

The system should support both:

1. **Preseason / Draft Mode**
   - Live draft assistance.
   - Ranking players according to the configured league.
   - Updating recommendations as players are drafted.
   - Identifying value, scarcity, team fit, and the risk of waiting until the user's next pick.

2. **In-Season Mode**
   - Risers and fallers.
   - Free-agent and waiver recommendations.
   - Streaming plans.
   - Trade evaluation.
   - Category-specific team improvement.
   - Schedule-aware recommendations.
   - Context-aware projection adjustments.

The project is intentionally designed as a substantial **C#/.NET application**, not a Python statistics script wrapped in a web page.

---

## 2. Core Design Principle: Statistics Are Necessary but Not Sufficient

Historical box-score statistics cannot completely describe future fantasy value.

A player can have excellent prior-season production while entering a materially different basketball environment because of:

- trades;
- new high-usage teammates;
- a new primary facilitator;
- starting-lineup changes;
- coaching changes;
- injury returns;
- reduced or expanded rotation;
- depth-chart competition;
- position changes;
- changes in offensive role;
- expected load management;
- changes in pace or offensive system.

The application therefore needs **two parallel evidence systems**:

### 2.1 Quantitative Evidence

Examples:

- minutes per game;
- usage rate;
- field-goal attempts;
- three-point attempts;
- touches;
- assists / potential assists;
- rebounds / rebound chances;
- steals;
- blocks;
- turnovers;
- fantasy points per minute;
- shooting percentages;
- starter status;
- games played;
- injury history;
- opponent strength;
- schedule density;
- back-to-backs;
- recent rolling averages.

### 2.2 Contextual Evidence

Examples:

- “Player X is expected to become the primary ball handler.”
- “Player Y moved to the bench.”
- “Team acquired another high-usage scorer.”
- “Coach intends to reduce Player Z's minutes.”
- “Starting center is expected to miss multiple weeks.”
- “Player is returning from a long injury.”
- “Team is experimenting with Player A at point guard.”

These events must not exist only as unstructured text. The system should convert important context into **structured Context Events** that can modify projections.

---

## 3. Illustrative Context Example

A motivating example supplied during project planning is a historically elite fantasy guard whose team subsequently acquires multiple high-usage players and another major facilitator.

Even if the player's previous statistics indicate first-round fantasy value, the system should recognize possible changes to:

- minutes;
- usage;
- assist share;
- shot attempts;
- touches;
- role stability;
- ceiling and floor.

The system should therefore be capable of producing an explanation such as:

> Historical production remains elite, but the current projection carries increased downside because roster changes may reduce on-ball responsibility and offensive usage. The model has reduced expected assists and usage while increasing role uncertainty.

This example is an **illustrative design scenario**, not a hard-coded assumption about any real NBA roster.

---

# 4. Primary User Workflows

## 4.1 League Setup

A user creates a league and chooses:

- points league;
- category league;
- roto league if eventually supported.

Configuration includes:

- number of teams;
- roster slots;
- position eligibility;
- bench slots;
- IR / IL+ settings;
- weekly or daily lineups;
- acquisition limits;
- trade deadline;
- playoffs;
- scoring categories;
- point values;
- negative scoring rules;
- waiver behavior.

### Points-League Example

```text
PTS   = +1.0
REB   = +1.2
AST   = +1.5
STL   = +3.0
BLK   = +3.0
TO    = -1.0
```

The scoring engine must not assume ESPN defaults.

---

## 4.2 League Import

Supported ingestion should be provider-based.

Preferred order:

1. Official supported API.
2. Public structured endpoint.
3. Public web scraping where permitted.
4. CSV/file import.
5. Manual configuration.

The application must remain fully usable even when no fantasy provider can be connected.

### Initial provider targets

- ESPN — main intended user platform, but no dependable public fantasy API should be assumed.
- Yahoo — official Fantasy Sports API supports basketball and league-specific fantasy data.
- Sleeper — read-only API exists, but NBA-specific coverage must be validated before depending on individual endpoints.
- Manual / CSV — required fallback, not an afterthought.

---

# 5. Draft Assistant

The Draft Assistant should be a first-class feature rather than a later add-on.

## 5.1 Draft Setup

Inputs:

- league scoring settings;
- league size;
- roster configuration;
- draft type;
- user's draft position;
- number of rounds;
- current pick;
- existing keeper players if relevant;
- projection source;
- ADP/ranking source.

The system creates a live **Draft Session**.

---

## 5.2 Live Draft Board

As players are selected, picks can be:

- imported automatically from a supported provider;
- detected from a permitted public source;
- entered manually with a fast search/select UI.

Manual entry must always work so a live draft is never dependent on an integration.

The board should immediately update:

- players remaining;
- team rosters;
- positional scarcity;
- expected value;
- category strengths/weaknesses;
- projected replacement value;
- probability a player survives until the user's next selection.

---

## 5.3 Draft Recommendation Goal

The highest-ranked player should **not automatically** be the recommended pick.

The system should distinguish:

- **Best player available**
- **Best expected-value pick**
- **Best roster-fit pick**
- **Best category-fit pick**
- **Best value relative to market/ADP**
- **Player unlikely to survive until next pick**
- **Player who can probably be selected later**

### Example

```text
1. Player A — Recommended Now
   Projection Rank: 12
   ADP: 18
   Survival to next pick: 14%
   Major tier drop after this player
   Strong fit for AST/STL

2. Player B — Better raw projection, but likely wait
   Projection Rank: 10
   ADP: 31
   Survival to next pick: 73%

3. Player C — High upside / higher role risk
```

This creates a true drafting assistant rather than a sorted projections table.

---

## 5.4 Draft Value Model

A conceptual points-league draft score:

```text
DraftValue =
    ProjectedSeasonValue
  + ValueAboveReplacement
  + PositionalScarcity
  + RosterFit
  + MarketValue
  + ContextAdjustment
  + UpsideAdjustment
  - InjuryRisk
  - RoleRisk
  - AvailabilityAtNextPickAdjustment
```

The weights should eventually be configurable and back-tested.

### Market Value

If a player is projected as the 20th-best asset but is commonly selected around pick 45, selecting him at pick 18 may be unnecessarily early.

The assistant should recognize the value of waiting.

Possible approach:

```text
P(player available at next pick)
```

estimated from:

- ADP;
- ADP variance;
- current draft position;
- number of picks until next selection;
- positional runs;
- live draft behavior.

---

## 5.5 Category-League Drafting

For category leagues, raw total value is insufficient.

The assistant should monitor the developing roster:

```text
PTS   strong
REB   average
AST   weak
STL   weak
BLK   strong
3PM   strong
FG%   average
FT%   strong
TO    weak
```

Recommendations should change depending on team construction.

Potential support:

- balanced drafting;
- punt strategies;
- user-selected punt categories;
- automatically detected punt opportunities.

The system should distinguish between **absolute player value** and **marginal value to this particular roster**.

---

# 6. In-Season Dashboard

Primary panels:

- Risers
- Fallers
- Free Agents
- Streamers
- Buy Low
- Sell High
- Drop Candidates
- Schedule Opportunities
- Injury / Role Changes
- My Team
- Matchup Outlook

Every recommendation should include:

1. recommendation;
2. expected impact;
3. major reasons;
4. confidence;
5. evidence;
6. notable risks.

---

# 7. Riser / Faller Engine

A player should not become a “riser” merely because he scored unusually well for three games.

## 7.1 Rolling Windows

Track:

- season;
- last 30 days;
- last 14 days;
- last 7 days;
- last 10 games;
- last 5 games.

Potential features:

- MPG change;
- usage change;
- FGA change;
- 3PA change;
- assist change;
- rebound change;
- steals/blocks;
- starting percentage;
- fantasy points per minute;
- efficiency;
- foul rate;
- turnover rate.

---

## 7.2 Sustainable vs Unsustainable Production

Example:

### Player A

```text
Fantasy production +35%
Minutes             +2%
Usage               +1%
FGA                 +3%
3P%                 +19 percentage points
```

Interpretation:

> Production spike may be primarily shooting-driven and therefore less sustainable.

### Player B

```text
Fantasy production +18%
Minutes            +24%
Usage              +17%
FGA                +20%
Starter status     changed to starter
```

Interpretation:

> More modest production increase, but underlying opportunity supports a more sustainable breakout.

---

## 7.3 Trend Score

Initial conceptual model:

```text
TrendScore =
    w1 * MinutesTrend
  + w2 * UsageTrend
  + w3 * OpportunityTrend
  + w4 * FantasyProductionTrend
  + w5 * RoleChange
  + w6 * StartingProbability
  - w7 * ShootingRegressionRisk
  - w8 * SmallSamplePenalty
  - w9 * RoleUncertainty
```

The actual weights should initially be transparent constants and later tuned through historical validation.

---

# 8. Context / News Engine

This is a critical differentiator.

## 8.1 ContextEvent Entity

Suggested fields:

```text
ContextEvent
------------
Id
EventType
TeamId
PrimaryPlayerId
AffectedPlayerIds[]
Timestamp
EffectiveFrom
ExpectedExpiration
Direction
Magnitude
Confidence
SourceUrl
SourceName
RawText
Summary
Verified
```

Possible `EventType` values:

```text
Trade
Signing
Injury
ReturnFromInjury
StartingLineupChange
BenchRoleChange
MinutesRestriction
CoachStatement
FacilitatorChange
UsageChange
PositionChange
RotationChange
RestRisk
DepthChartChange
```

---

## 8.2 Context Impact

A context event can apply estimated directional impacts:

```text
PlayerContextImpact
-------------------
PlayerId
ContextEventId
MinutesDelta
UsageDelta
AssistShareDelta
ReboundShareDelta
ShotVolumeDelta
RoleRiskDelta
ProjectionConfidenceDelta
```

These are **estimates**, not facts, and must be shown as such.

---

## 8.3 Human-in-the-Loop

The user should be able to:

- accept an inferred context event;
- reject it;
- adjust its confidence;
- manually create one;
- override projection effects.

This prevents scraped commentary from silently becoming ground truth.

---

# 9. Web Scraping Strategy

Web scraping is a required capability because:

1. ESPN integration cannot be assumed.
2. Provider APIs can disappear, change, or expose incomplete information.
3. Qualitative basketball context is often published as articles, transaction reports, roster notes, or coaching comments rather than structured statistics.

However, scraping should be implemented as a controlled infrastructure capability rather than ad-hoc HTML parsing throughout the application.

---

## 9.1 Source Priority

```text
Official API
    ↓
Public structured data endpoint
    ↓
Permitted public-page scraper
    ↓
CSV / manual input
```

A scraper should never be required for the core domain to function.

---

## 9.2 Scraper Interfaces

```csharp
public interface IDataSource
{
    string Name { get; }
}

public interface IPlayerStatsProvider : IDataSource
{
    Task<IReadOnlyList<PlayerStatLine>> GetStatsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}

public interface IFantasyLeagueProvider : IDataSource
{
    Task<FantasyLeagueSnapshot> GetLeagueAsync(
        CancellationToken cancellationToken);
}

public interface INewsProvider : IDataSource
{
    Task<IReadOnlyList<NewsItem>> GetRecentNewsAsync(
        CancellationToken cancellationToken);
}
```

Possible implementations:

```text
YahooFantasyProvider
SleeperFantasyProvider
EspnPublicScraper
ManualLeagueProvider
CsvLeagueProvider

PublicStatsScraper
ScheduleProvider
TransactionNewsScraper
TeamNewsScraper
```

---

## 9.3 Scraping Rules

The implementation should:

- scrape only content the application is permitted to access;
- respect source Terms of Use and robots directives;
- not bypass authentication or anti-bot controls;
- not automate access to private league pages unless an explicitly supported/authorized method exists;
- use rate limiting;
- use retry with exponential backoff;
- cache responses;
- store acquisition timestamps;
- identify data source provenance;
- avoid unnecessary repeated requests;
- isolate each site's parser;
- include automated parser tests using saved HTML fixtures;
- fail gracefully when a page structure changes.

### Important ESPN constraint

ESPN should be treated as an **optional adapter**, not a dependency of the domain.

The system must continue to operate through:

- manual roster entry;
- manual draft entry;
- CSV import;
- alternative statistics providers.

This allows ESPN to remain the user's primary fantasy platform without making an unsupported integration a single point of failure.

---

# 10. Data Provenance

Every imported value should ideally be traceable.

Suggested metadata:

```text
Source
ExternalId
FetchedAt
SourceTimestamp
ParserVersion
Confidence
RawRecordHash
```

This allows debugging situations such as:

> Why does the app think Player X changed teams?

or:

> Which source supplied this injury status?

---

# 11. Player Identity Resolution

Different platforms may use different player IDs.

Create an internal canonical ID:

```text
Player
  InternalPlayerId

ExternalPlayerIdentity
  PlayerId
  Provider
  ExternalPlayerId
```

Matching may initially use:

- full name;
- normalized name;
- NBA team;
- position;
- birth date when available.

Ambiguous matches should require manual confirmation.

Never make provider IDs the application's primary domain identity.

---

# 12. Projections

Maintain separate concepts:

```text
ObservedStats
ProjectedStats
AdjustedProjection
FantasyValue
```

Do not overwrite raw projections after context adjustment.

Example:

```text
Baseline Projection:
31.2 fantasy points/game

Statistical Trend Adjustment:
+1.8

Context Adjustment:
-2.4

Final Projection:
30.6

Confidence:
Moderate
```

This makes recommendations auditable.

---

# 13. Context-Aware Projection Model

Conceptual pipeline:

```text
Historical Production
        ↓
Baseline Projection
        ↓
Age / Development
        ↓
Minutes Projection
        ↓
Role / Usage Projection
        ↓
Team Context
        ↓
Injury / Availability
        ↓
Schedule
        ↓
League Scoring
        ↓
Fantasy Projection
```

A major design goal is to avoid treating recent fantasy points as the only predictive signal.

---

# 14. Free-Agent Analyzer

For every available player calculate:

- baseline value;
- recent value;
- trend score;
- roster percentage trend if available;
- role stability;
- upcoming games;
- usable games;
- positional eligibility;
- expected rest;
- opponent quality;
- contextual upside/downside;
- fit with user's roster.

Outputs:

```text
Strong Add
Speculative Add
Streamer
Watch List
Hold
Drop Candidate
```

---

# 15. Streaming Advisor

Streaming should optimize **usable fantasy production**, not simply number of NBA games.

## 15.1 Inputs

- user's roster;
- opponent roster if available;
- lineup slots;
- available free agents;
- acquisition limit;
- schedule;
- high-volume / low-volume NBA days;
- expected production;
- injuries;
- matchup difficulty;
- rest;
- back-to-backs;
- category needs.

---

## 15.2 Usable Game

A game only provides value if the user can actually place the player in an active lineup slot.

Therefore:

```text
4 scheduled games
```

may be worse than:

```text
3 scheduled games on low-volume nights
```

if the four-game player would repeatedly sit on the fantasy bench.

---

## 15.3 Streaming Score

Conceptually:

```text
StreamingValue =
    ExpectedPerGameValue
  * UsableGames
  * RoleConfidence
  * AvailabilityProbability
  * MatchupAdjustment
  * TeamNeedAdjustment
```

---

## 15.4 Multi-Transaction Optimization

Advanced feature:

Instead of recommending one player, recommend a sequence.

```text
Monday:
Add Player A

Wednesday:
Drop Player A
Add Player B

Saturday:
Drop Player B
Add Player C
```

Optimize:

```text
maximize ExpectedIncrementalFantasyValue
subject to:
    acquisition count <= weekly limit
    roster constraints satisfied
    player availability satisfied
```

Possible implementation approaches:

- dynamic programming;
- integer programming;
- search with pruning.

Start with a greedy heuristic, then improve.

---

# 16. Category-League Team Analyzer

For category leagues, compute team-level category strength.

Potential metrics:

- league percentile;
- standardized z-score;
- expected weekly category total;
- matchup win probability.

Example:

```text
PTS   88th percentile
REB   63rd
AST   41st
STL   28th
BLK   75th
3PM   92nd
FG%   59th
FT%   72nd
TO    49th
```

A free agent who adds scoring may have low marginal value while a lower-ranked steals/assists player may materially improve expected matchup wins.

---

# 17. Trade Analyzer

Input:

- players received;
- players sent;
- optional multi-team trade;
- current roster;
- league settings.

Evaluate:

### Points league

- projected fantasy points;
- games played;
- replacement player effect;
- schedule;
- roster slot impact;
- injury/role risk.

### Category league

- before/after category profile;
- category win probability;
- strength redistribution;
- punt compatibility.

Possible output:

```text
Raw Value: Slight win
Roster Fit: Strong win
Schedule: Neutral
Risk: Increased
Category Impact:
  +REB
  +BLK
  +STL
  -3PM
  -FT%

Recommendation: Accept
Confidence: Moderate
```

---

# 18. Data Model

Initial entities:

```text
Player
NbaTeam
ExternalPlayerIdentity

PlayerGameStat
PlayerRollingStat
PlayerProjection

NbaGame
ScheduleSnapshot

FantasyLeague
FantasyScoringRule
FantasyRoster
FantasyRosterPlayer
FantasyTransaction

DraftSession
DraftTeam
DraftPick
DraftPlayerState

ContextEvent
PlayerContextImpact
NewsItem

Recommendation
RecommendationEvidence

DataSource
DataImportRun
```

---

# 19. Proposed .NET Solution Structure

Use a modular monolith rather than microservices for V1.

```text
FantasyBasketball.sln

src/
  FantasyBasketball.Api/
  FantasyBasketball.Application/
  FantasyBasketball.Domain/
  FantasyBasketball.Infrastructure/

tests/
  FantasyBasketball.Domain.Tests/
  FantasyBasketball.Application.Tests/
  FantasyBasketball.IntegrationTests/
```

## Domain

Contains pure business concepts:

```text
Player
League
ScoringRules
Projection
Draft
Recommendation
ContextEvent
```

No ESPN/Yahoo/database code.

## Application

Use cases/services:

```text
DraftRecommendationService
StreamingRecommendationService
TradeEvaluationService
TrendAnalysisService
ProjectionService
LeagueImportService
```

## Infrastructure

External concerns:

```text
EF Core
PostgreSQL
HTTP clients
Yahoo adapter
Sleeper adapter
ESPN scraper
news scrapers
caching
background refresh jobs
```

## API

ASP.NET endpoints/controllers.

---

# 20. .NET Concepts This Project Should Intentionally Demonstrate

This project exists partly to prove practical C#/.NET ability.

Required learning targets:

- C# classes and records;
- enums;
- interfaces;
- nullable reference types;
- generics;
- LINQ;
- async/await;
- `Task<T>`;
- dependency injection;
- ASP.NET Core;
- REST APIs;
- middleware;
- configuration / Options pattern;
- `HttpClientFactory`;
- EF Core;
- PostgreSQL;
- migrations;
- background hosted services;
- cancellation tokens;
- caching;
- validation;
- structured logging;
- unit tests;
- integration tests.

Potential later additions:

- SignalR for live draft updates;
- authentication / ASP.NET Identity;
- Redis;
- Quartz.NET or Hangfire;
- containerization;
- cloud deployment.

---

# 21. Background Data Refresh

Use .NET hosted services for recurring data collection.

Example responsibilities:

```text
StatRefreshWorker
ScheduleRefreshWorker
NewsRefreshWorker
ProjectionRefreshWorker
```

Different information has different freshness requirements.

Example conceptual cadence:

```text
Schedules       daily / after changes
Player metadata daily
Game stats      after games / periodic
News            more frequently
League roster   user-triggered + periodic
Draft board     near-real-time during draft
```

Do not blindly refresh every source at the same frequency.

---

# 22. Explainability

The application should never produce an unexplained number like:

```text
Stream Score: 87.4
```

without evidence.

Example:

```text
Recommendation: Add Player X
Confidence: High

Why:
+ 4 usable games this week
+ minutes increased from 22.4 to 31.7
+ moved into starting lineup
+ usage increased 4.2 percentage points
+ strong AST/STL fit for your roster

Risks:
- recent FG% is well above career average
- starter ahead of him may return next week
```

The recommendation engine should return structured evidence that the UI formats.

---

# 23. Confidence Model

All recommendations need a confidence value.

Potential factors:

- amount of historical data;
- consistency across data sources;
- role stability;
- injury uncertainty;
- freshness;
- projection disagreement;
- contextual uncertainty.

Example:

```text
High
Moderate
Low
Speculative
```

Avoid false precision.

---

# 24. Manual Overrides

Users must be able to override:

- league rosters;
- scoring settings;
- draft picks;
- player status;
- context events;
- projection adjustments;
- player availability.

This makes the product resilient when external sources fail.

---

# 25. Testing Strategy

## Domain Unit Tests

Examples:

```text
Fantasy scoring calculation
Category z-score calculation
Replacement value
Draft ranking
Trend score
Streaming usable-game calculation
Trade before/after calculation
```

These should not require a database or internet connection.

## Scraper Tests

Each scraper should use saved HTML fixtures.

Test:

- expected player names;
- statistics;
- dates;
- parser behavior when fields are missing.

A website redesign should fail the parser test rather than silently corrupt data.

## Provider Contract Tests

Verify each adapter converts provider-specific responses into canonical application models.

## Integration Tests

Test:

- ASP.NET endpoints;
- EF Core;
- PostgreSQL;
- provider mocks.

---

# 26. Error Handling

Provider failure must not break the application.

Example:

```text
ESPN scraper unavailable
Yahoo unavailable
news source changed HTML
```

Expected application behavior:

```text
Last successful stats update: 3h ago
ESPN league sync unavailable
Manual roster data remains active
Recommendations generated from remaining sources
Confidence reduced
```

---

# 27. Security / Privacy Scope

Although this is not a cybersecurity-focused project, basic application hygiene remains necessary:

- never store fantasy-provider passwords;
- use OAuth where supported;
- encrypt stored provider tokens;
- keep secrets out of source control;
- validate user input;
- restrict imported files;
- do not scrape authenticated/private data without an explicitly permitted integration.

---

# 28. Frontend

Frontend technology is intentionally undecided.

Candidates:

- React;
- Blazor.

The backend should remain independent of the frontend.

Initial pages:

```text
Dashboard
My League
My Team
Players
Free Agents
Streamers
Risers/Fallers
Trades
Draft Assistant
Data Sources
Settings
```

---

# 29. Recommended Implementation Order

Because Draft Mode is useful before the regular season, build from the domain outward.

## Phase 0 — Project Skeleton

- .NET solution;
- Domain/Application/Infrastructure/API projects;
- PostgreSQL;
- EF Core;
- testing projects;
- basic Player/Team models.

## Phase 1 — League + Scoring Engine

- manual league creation;
- points scoring;
- category rules;
- roster structures;
- manual player availability.

**Milestone:** Given a stat line and league settings, calculate fantasy value correctly.

## Phase 2 — Player Data Ingestion

- canonical player IDs;
- one reliable player/stat source;
- schedule ingestion;
- caching;
- import history;
- one required public web scraper.

**Milestone:** Application maintains its own normalized NBA player/stat dataset.

## Phase 3 — Draft Assistant MVP

- draft session;
- manual live picks;
- projection rankings;
- available player board;
- value above replacement;
- positional scarcity;
- roster fit;
- ADP comparison.

**Milestone:** User can conduct an entire draft with only manual pick entry.

## Phase 4 — Context Engine

- news ingestion/scraping;
- ContextEvent model;
- context review UI;
- projection adjustments;
- explanation evidence.

**Milestone:** Roster/role news can alter projections without overwriting baseline statistics.

## Phase 5 — Draft Intelligence

- probability of surviving to next pick;
- market/ADP value;
- tier detection;
- category fit;
- recommended-now vs recommended-later.

## Phase 6 — In-Season Trends

- rolling averages;
- riser/faller model;
- sustainable vs shooting-driven spikes;
- free-agent rankings.

## Phase 7 — Streaming Advisor

- schedule-aware usable games;
- daily lineup capacity;
- multi-day streaming recommendations;
- acquisition-limit optimization.

## Phase 8 — Provider Integrations

Priority to be determined by feasibility:

- Yahoo OAuth/API;
- ESPN adapter if a compliant/reliable method is available;
- Sleeper adapter after NBA endpoint validation;
- CSV import.

## Phase 9 — Trade Analyzer

- points-league trade value;
- category roster impact;
- replacement-level effects.

## Phase 10 — Production Polish

- auth;
- responsive UI;
- logging;
- deployment;
- caching improvements;
- data-source health dashboard;
- richer test suite.

---

# 30. MVP Definition

The first version should be considered successful when it can:

1. Create a custom points or category league manually.
2. Store NBA players and recent statistics.
3. Ingest at least one data source through HTTP.
4. Ingest at least one permitted public source through HTML scraping.
5. Calculate league-specific player value.
6. Run a draft with manually entered picks.
7. Dynamically rerank the remaining player pool.
8. Show baseline projection, context adjustment, and final projection separately.
9. Record user-created/context-scraped role events.
10. Generate explainable draft recommendations.

Do **not** wait for ESPN/Yahoo/Sleeper integration before the MVP is usable.

---

# 31. Non-Goals for V1

Avoid these until the decision engine works:

- native mobile application;
- microservices;
- Kubernetes;
- LLM-generated recommendations;
- automated fantasy transactions;
- automated trading/waiver submissions;
- social network features;
- perfect projection modeling;
- supporting every fantasy provider.

The goal is a strong decision engine and a strong .NET codebase.

---

# 32. Future ML / AI Opportunities

An LLM is explicitly **not** required for the first implementation.

Later possibilities:

- convert articles into proposed ContextEvents;
- summarize recommendation evidence;
- entity extraction from basketball news;
- explain trade recommendations conversationally;
- ask natural-language questions about a roster.

Important rule:

> Statistical/optimization code should make the recommendation. An LLM may eventually help extract context or explain the result.

Traditional ML could later be used for:

- minutes projection;
- injury-return uncertainty;
- breakout classification;
- probability a player remains available at a later draft pick;
- expected fantasy-value forecasting.

---

# 33. Data-Source Notes Verified During Planning

### Yahoo

Yahoo currently documents an official Fantasy Sports API that supports basketball and exposes fantasy game, league, team, and player information. Private/user-specific data uses OAuth.

Reference:
- https://developer.yahoo.com/fantasysports/guide/
- https://developer.yahoo.com/api/

### Sleeper

Sleeper documents a free, read-only HTTP API with league, roster, draft, transaction, and player concepts. The documentation includes an NBA sport state, but several league/player examples and parameter descriptions still explicitly describe NFL-only support. Therefore NBA support must be **tested endpoint-by-endpoint** before Sleeper becomes a required integration.

Reference:
- https://docs.sleeper.com/

### ESPN

ESPN remains a desirable fantasy source because it is the primary intended league platform, but this design should **not assume a stable, documented public consumer Fantasy Basketball API**. Public-page scraping may be investigated only where permitted, and the product must retain manual/CSV/provider-independent fallbacks.

ESPN publishes a robots file with path-specific restrictions, reinforcing the need to treat scraping as source-specific rather than assuming all ESPN pages are appropriate for automation.

Reference:
- https://www.espn.com/robots.txt
- https://fantasy.espn.com/basketball/

---

# 34. Key Architectural Decision

The most important technical rule in the project:

> **The domain must never know whether information came from ESPN, Yahoo, Sleeper, an HTML scraper, a CSV, or manual entry.**

All external sources translate into canonical models.

That allows the application to survive:

- API changes;
- failed integrations;
- HTML redesigns;
- provider migrations;
- different fantasy leagues.

---

# 35. Definition of the Project on a Resume

A future resume description should be supportable by the actual implementation, for example:

> **Fantasy Basketball Decision Engine — C#, ASP.NET Core, EF Core, PostgreSQL**  
> Built a league-aware fantasy basketball analytics platform that normalizes statistics and roster data across API, manual, and web-scraped sources; developed draft, streaming, and trend-ranking engines using schedule optimization, replacement value, rolling statistics, and contextual role adjustments.

A stronger later bullet could include:

> Designed provider adapters and background ingestion services for resilient multi-source data collection, with explainable recommendation scoring and live draft re-ranking based on roster fit, player scarcity, market ADP, and expected availability.

---

# 36. First IDE Planning Questions

When implementation planning resumes in the IDE, the first decisions should be:

1. Project name.
2. .NET target version.
3. PostgreSQL vs SQLite for local-first development.
4. React vs Blazor frontend.
5. Initial statistics source.
6. First permitted scraping target.
7. Projection source or initial projection algorithm.
8. Exact points/category scoring model.
9. Draft-value formula for V1.
10. Whether Clean Architecture project separation is useful immediately or should be simplified.

The recommended first coding milestone is:

> **Create a custom league, enter a player stat line, and calculate that player's fantasy value through a tested C# domain service.**

Everything else can grow from that stable domain model.
