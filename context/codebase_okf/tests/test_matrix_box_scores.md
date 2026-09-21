---
type: test_matrix
title: Test Matrix — Completed Box Scores
description: Offline parser and atomic snapshot storage boundaries for appearance-based analysis.
tags: [tests, ingestion, persistence, matrix]
source_paths: [src/FantasyBasketball.Domain/Trends/CompletedBoxScore.cs, src/FantasyBasketball.Infrastructure/Scrapers/BasketballReference/BoxScoreParser.cs, src/FantasyBasketball.Infrastructure/Persistence/Repositories/BoxScoreRepository.cs]
test_paths: [tests/FantasyBasketball.Domain.Tests/Trends/CompletedBoxScoreTests.cs, tests/FantasyBasketball.IntegrationTests/Scrapers/BoxScoreParserTests.cs, tests/FantasyBasketball.IntegrationTests/Persistence/BoxScoreStorageTests.cs]
depends_on: [required_gates.md, ../contracts/box_score_storage_contract.md]
status: implemented
last_updated: 2026-09-21
owners: [engineering]
---

# Required cases

| ID | Case | Expected | Required |
|---|---|---|---|
| `BS-01` | Visible/comment tables, repeated headers, quarter/advanced/footer rows | Only full-game players; deterministic raw-page SHA-256 | ✅ |
| `BS-02` | DNP, played zero, unknown absence reason | DNP has null stats; zero appearance retained; unknown reason fails | ✅ |
| `BS-03` | Missing/renamed/duplicate required columns | Explicit parse failure | ✅ |
| `BS-04` | Invalid minutes, negative/fractional counts, rebound/shot identities | Explicit parse failure | ✅ |
| `BS-05` | Wrong game/host, duplicate tables/players, contradictory identity | Explicit parse failure | ✅ |
| `BS-06` | Invalid snapshot, duplicate players, mutable input | Rejected invalid observations; defensive copy | ✅ |
| `BS-07` | Empty-database migration and shared-data round trip | Full provenance, explicit phase, DNP/zero preserved, no user required | ✅ |
| `BS-08` | Sequential replay and concurrent inserts after duplicate checks | Exactly one complete snapshot | ✅ |
| `BS-09` | Whole-page correction, source/season/date/phase selection | Only latest page rows; no removed player or outdated classification resurrection | ✅ |
| `BS-10` | Invalid FK, cancellation, invalid game/season, mutation/deletion | Atomic rollback, retryable context, append-only enforcement | ✅ |

# Verification boundary

`dotnet test --filter 'FullyQualifiedName~BS'`, followed by the full gate.
Parser fixtures are synthetic and database fixtures use disposable PostgreSQL 17.
These tests do not claim live-site compatibility, identity resolution, final-game
verification, a resumable importer, heat HTTP routes, or heat UI completion.
