# ADR-001 — Offline Python model fitting

- **Status:** accepted by the owner, 2026-10-01
- **Plan:** [Statistical Engine & Product Upgrade (M1–M3)](https://claude.ai/code/artifact/82cad5d4-4267-4c0f-9176-26eb7580b278)

## Decision

Model parameters are fitted offline in Python 3.12 under `tools/modeling/`.
The .NET solution never references, launches or calls that folder. Python reads
history from PostgreSQL read-only and writes versioned parameter JSON; C# loads
the active version and applies it on the request path.

## Why

The M1–M3 models are Bayesian (hierarchical rates, Beta-Binomial availability,
Plackett–Luce choice, empirical-Bayes heat priors). Fitting them needs MCMC and
maximum likelihood, which hand-written C# would reimplement badly. Applying a
fitted posterior mean or a choice probability is closed-form and stays in C#.

## Boundaries

- No Python on the request path, in a worker, or in a test the gate runs.
- `MathNet.Numerics` stays forbidden; C# keeps its own `Phi` and rank code.
- Parameters are validated against a JSON Schema and checked by C# goldens:
  C# recomputes fixed players from stored params and must match Python's
  predictions to 1e-6, or the gate fails.
- Python dependencies are pinned in `tools/modeling/pyproject.toml` and its lock
  file; adding one follows the same approval rule as a NuGet package.
- Only Bayesian and classical statistics. No gradient boosting or neural nets.
