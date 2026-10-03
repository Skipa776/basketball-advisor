# Projection backtest — 2025–26 holdout

As of 2025-10-01 · trained on 2024–25 · 390 players with 20+ games · model `baseline-v1` · commit `3f33362`

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

Same players and projected minutes as the model above; only the per-minute rates differ.

| Metric | Hierarchical | baseline-v1 | Last season repeats | Hierarchical − baseline-v1 |
|---|---|---|---|---|
| MAE | 5.0594 | 5.2182 | 5.3542 | -0.1588 |
| RMSE | 6.4219 | 6.6112 | 6.7809 | -0.1893 |
| Spearman ρ | 0.7679 | 0.7521 | 0.7710 | 0.0158 |
| Top-100 hit rate | 0.7800 | 0.7800 | 0.7600 | 0.0000 |

### Calibration by decile (hierarchical)

| Decile | Players | Mean projected | Mean actual | Actual − projected |
|---|---|---|---|---|
| 1 | 39 | 7.6023 | 11.9330 | 4.3306 |
| 2 | 39 | 11.1455 | 14.3125 | 3.1670 |
| 3 | 39 | 14.5256 | 17.0142 | 2.4886 |
| 4 | 39 | 17.1792 | 14.1115 | -3.0677 |
| 5 | 39 | 19.4148 | 18.6726 | -0.7421 |
| 6 | 39 | 21.4960 | 21.7743 | 0.2783 |
| 7 | 39 | 24.6134 | 21.7059 | -2.9075 |
| 8 | 39 | 29.1751 | 28.8826 | -0.2925 |
| 9 | 39 | 34.4512 | 32.5934 | -1.8578 |
| 10 | 39 | 43.5387 | 42.7684 | -0.7703 |
