# Projection backtest — 2025–26 holdout

As of 2025-10-01 · trained on 2024–25 · 390 players with 20+ games · model `baseline-v1` · commit `ecc1ad5`

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

Same players as the model above; per-minute rates from the hierarchical model, minutes from `projection-minutes-202610030206`.

| Metric | Hierarchical | baseline-v1 | Last season repeats | Hierarchical − baseline-v1 |
|---|---|---|---|---|
| MAE | 4.8846 | 5.2182 | 5.3542 | -0.3336 |
| RMSE | 6.0771 | 6.6112 | 6.7809 | -0.5341 |
| Spearman ρ | 0.7828 | 0.7521 | 0.7710 | 0.0307 |
| Top-100 hit rate | 0.7800 | 0.7800 | 0.7600 | 0.0000 |

80% intervals held the actual points per game for 0.7590 of 390 players (mean SD 5.6358; target 0.76–0.84). Distribution models: projection-availability-202610030224, projection-covariance-202610030228.

### Calibration by decile (hierarchical)

| Decile | Players | Mean projected | Mean actual | Actual − projected |
|---|---|---|---|---|
| 1 | 39 | 9.4660 | 11.0830 | 1.6171 |
| 2 | 39 | 12.7785 | 13.5139 | 0.7354 |
| 3 | 39 | 15.3081 | 16.0091 | 0.7010 |
| 4 | 39 | 17.3262 | 16.8230 | -0.5032 |
| 5 | 39 | 19.1818 | 19.8422 | 0.6604 |
| 6 | 39 | 21.0802 | 19.7408 | -1.3394 |
| 7 | 39 | 24.1365 | 22.9103 | -1.2262 |
| 8 | 39 | 28.1605 | 27.7480 | -0.4125 |
| 9 | 39 | 33.2543 | 33.1429 | -0.1113 |
| 10 | 39 | 42.4093 | 42.9551 | 0.5458 |
