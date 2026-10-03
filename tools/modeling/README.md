# fantasy-modeling

Offline parameter fitting only, per `docs/design/adr-001-offline-python-fitting.md`.
Python fits Bayesian and classical statistical parameters; the C# service serves them.
Nothing here runs on the request path.

## Setup

```sh
uv sync
```

## Environment variables

- `MODELING_DATABASE_URL` — connection string, defaults to
  `postgresql://fantasy@localhost:5432/fantasy_basketball`
- `PGPASSWORD` — read by psycopg when the URL carries no password

## Rule

Nothing in this directory is imported by the .NET solution. Fit results are
exported as data; the C# side reads parameters, never this code.
