"""Read-only database access for offline model fitting."""

import os

import psycopg

DEFAULT_DATABASE_URL = "postgresql://fantasy@localhost:5432/fantasy_basketball"


def connect() -> psycopg.Connection:
    """Open a read-only connection to the fantasy database."""
    conn_str = os.environ.get("MODELING_DATABASE_URL", DEFAULT_DATABASE_URL)
    try:
        conn = psycopg.connect(conn_str)
        conn.read_only = True
        return conn
    except psycopg.Error as err:
        raise RuntimeError(
            f"Could not connect to the fantasy database: {err}"
        ) from err
