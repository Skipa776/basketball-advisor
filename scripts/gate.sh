#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

dotnet restore --locked-mode

python3 - <<'PY'
from __future__ import annotations

import pathlib
import re
import subprocess
import tomllib

root = pathlib.Path.cwd()
config = tomllib.loads((root / "stack_config.toml").read_text())
source_files = list((root / "src").rglob("*.cs"))
project_files = list(root.rglob("*.csproj")) + [root / "Directory.Packages.props"]

for forbidden in config["forbidden"]["packages"]:
    pattern = re.compile(
        re.escape(forbidden).replace(r"\*", ".*"),
        re.IGNORECASE,
    )
    for path in project_files:
        if pattern.search(path.read_text()):
            raise SystemExit(f"Forbidden package {forbidden!r} found in {path}")

for expression, reason in config["forbidden"]["patterns"].items():
    pattern = re.compile(expression, re.MULTILINE)
    for path in source_files:
        if pattern.search(path.read_text()):
            raise SystemExit(f"Forbidden pattern {expression!r} found in {path}: {reason}")

tracked = subprocess.run(
    ["git", "ls-files", "-co", "--exclude-standard"],
    check=True,
    capture_output=True,
    text=True,
).stdout.splitlines()
secret_patterns = (
    re.compile(r"sk-[A-Za-z0-9_-]{20,}"),
    re.compile(r"(?i)(api[_-]?key|client[_-]?secret|password)\s*[:=]\s*['\"][^'\"\s]{12,}"),
)
for relative in tracked:
    path = root / relative
    if not path.is_file() or ".git" in path.parts:
        continue
    try:
        contents = path.read_text()
    except UnicodeDecodeError:
        continue
    for pattern in secret_patterns:
        if pattern.search(contents):
            raise SystemExit(f"Potential secret found in {relative}")

for path in source_files:
    contents = path.read_text()
    if "EnsureCreated" in contents:
        raise SystemExit(f"EnsureCreated is forbidden; use migrations: {path}")

canonical_patterns = {
    r'"(?:PTS|REB|AST|STL|BLK|TO)"': {"StatKey.cs"},
}
for expression, owners in canonical_patterns.items():
    pattern = re.compile(expression)
    for path in source_files:
        if path.name not in owners and pattern.search(path.read_text()):
            raise SystemExit(f"Canonical stat literal found outside its owner: {path}")
PY

dotnet format --verify-no-changes
dotnet build --no-restore -c Release
dotnet test --no-build -c Release --collect:"XPlat Code Coverage"

python3 - <<'PY'
from __future__ import annotations

import pathlib
import tomllib
import xml.etree.ElementTree as ET

root = pathlib.Path.cwd()
config = tomllib.loads((root / "stack_config.toml").read_text())
targets = (
    (
        "FantasyBasketball.Domain",
        root / "src/FantasyBasketball.Domain",
        root / "tests/FantasyBasketball.Domain.Tests/TestResults",
        config["gates"]["domain_line_coverage_min"],
    ),
    (
        "FantasyBasketball.Application",
        root / "src/FantasyBasketball.Application",
        root / "tests/FantasyBasketball.Application.Tests/TestResults",
        config["gates"]["application_line_coverage_min"],
    ),
)

for assembly, source_directory, results_directory, minimum in targets:
    source_files = [
        path
        for path in source_directory.rglob("*.cs")
        if "obj" not in path.parts and "bin" not in path.parts
    ]
    if not source_files:
        continue

    reports = list(results_directory.rglob("coverage.cobertura.xml"))
    if not reports:
        raise SystemExit(f"No coverage report found for {assembly}")

    report = max(reports, key=lambda path: path.stat().st_mtime)
    document = ET.parse(report)
    package = document.find(f".//package[@name='{assembly}']")
    if package is None:
        raise SystemExit(f"Coverage report does not contain {assembly}: {report}")

    actual = float(package.attrib["line-rate"]) * 100
    print(f"{assembly} line coverage: {actual:.2f}% (minimum {minimum}%)")
    if actual < minimum:
        raise SystemExit(
            f"{assembly} line coverage {actual:.2f}% is below {minimum}%"
        )
PY

python3 context/validate_okf.py
