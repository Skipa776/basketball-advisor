# Projection backtest — 2025–26 holdout

As of 2025-10-01 · trained on 2024–25 · 390 players with 20+ games · model `baseline-v1` · commit `f84389e`

Fantasy points per game under ESPN default points scoring.

| Metric | Model | Last season repeats | Model − baseline |
|---|---|---|---|
| MAE | 5.2182 | 5.3542 | -0.1360 |
| RMSE | 6.6112 | 6.7809 | -0.1697 |
| Spearman ρ | 0.7521 | 0.7710 | -0.0189 |
| Top-100 hit rate | 0.7800 | 0.7600 | 0.0200 |

## Calibration by decile (model)

| Decile | Players | Mean projected | Mean actual | Actual − projected |
|---|---|---|---|---|
| 1 | 39 | 8.3441 | 12.3227 | 3.9787 |
| 2 | 39 | 12.0635 | 14.1293 | 2.0658 |
| 3 | 39 | 15.1739 | 16.6329 | 1.4590 |
| 4 | 39 | 17.6187 | 15.3829 | -2.2358 |
| 5 | 39 | 19.8865 | 18.6664 | -1.2201 |
| 6 | 39 | 21.8608 | 21.3229 | -0.5379 |
| 7 | 39 | 24.7154 | 21.0423 | -3.6731 |
| 8 | 39 | 29.0382 | 29.2555 | 0.2173 |
| 9 | 39 | 34.1670 | 32.4026 | -1.7644 |
| 10 | 39 | 42.4786 | 42.6109 | 0.1323 |

## Hierarchical rates (`projection-rates-202610030117`)

Same players as the model above; per-minute rates from the hierarchical model, minutes from `projection-minutes-202610050340`.

| Metric | Hierarchical | baseline-v1 | Last season repeats | Hierarchical − baseline-v1 |
|---|---|---|---|---|
| MAE | 4.8734 | 5.2182 | 5.3542 | -0.3449 |
| RMSE | 6.0616 | 6.6112 | 6.7809 | -0.5496 |
| Spearman ρ | 0.7831 | 0.7521 | 0.7710 | 0.0310 |
| Top-100 hit rate | 0.7800 | 0.7800 | 0.7600 | 0.0000 |

80% intervals held the actual points per game for 0.7667 of 390 players (mean SD 5.6461; target 0.76–0.84). Distribution models: projection-availability-202610030224, projection-covariance-202610030228.

### Calibration by decile (hierarchical)

| Decile | Players | Mean projected | Mean actual | Actual − projected |
|---|---|---|---|---|
| 1 | 39 | 9.6324 | 10.8726 | 1.2402 |
| 2 | 39 | 12.7370 | 13.8191 | 1.0821 |
| 3 | 39 | 15.2858 | 16.0405 | 0.7547 |
| 4 | 39 | 17.2995 | 16.6409 | -0.6586 |
| 5 | 39 | 19.1828 | 19.5842 | 0.4014 |
| 6 | 39 | 21.0276 | 20.0547 | -0.9729 |
| 7 | 39 | 24.1120 | 22.9103 | -1.2017 |
| 8 | 39 | 28.1206 | 27.7480 | -0.3726 |
| 9 | 39 | 33.2416 | 33.1429 | -0.0987 |
| 10 | 39 | 42.3605 | 42.9551 | 0.5946 |

## Draft benchmark

200 seeded 10-team, 9-starter drafts (ESPN points scoring, user slot rotating) over the 569-player 2024–25 pool with projections as of 2025-10-01. Opponents take lowest ADP after Normal(0, max(6, 0.2·ADP)) jitter; with no 2025 ADP stored, ADP is each player's rank by last season's fantasy points. Each roster scores its best starting lineup's **actual** 2025–26 points. The simulator ran 500 rollouts per pick.

| Drafter | Mean starting-lineup points |
|---|---|
| ADP bot | 20157.0650 |
| Heuristic board | 27693.6300 |
| Simulator | 27432.7450 |

| Comparison (paired, 200 drafts) | Mean difference | 95% CI |
|---|---|---|
| Heuristic board − ADP bot | 7536.5650 | 7139.6511 to 7933.4789 |
| Simulator − ADP bot | 7275.6800 | 6871.1824 to 7680.1776 |
| Simulator − Heuristic board | -260.8850 | -562.4348 to 40.6648 |
