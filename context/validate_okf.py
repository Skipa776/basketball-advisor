#!/usr/bin/env python3
"""Validate an OKF-style context bundle (portable version, bundled with the
okf-context-engineering skill).

Usage:
    python validate_okf.py [bundle_dir]

`bundle_dir` defaults to `context/codebase_okf`. Every Markdown file under it
is checked for:
- YAML frontmatter delimited by --- lines
- required keys (type, title, description, tags, source_paths, test_paths,
  depends_on, status, last_updated)
- a valid status value (planned | partial | implemented) and YYYY-MM-DD date
- depends_on entries and relative Markdown links that resolve to real files

If an `AGENT_CONTEXT_INDEX.md` exists in the bundle's parent directory, its
backticked `<bundle>/....md` path references are also resolved.

Stdlib only; runs before any project dependency is installed.
Exit code 0 = clean, 1 = problems found. Copy into the target repo
(conventionally `context/validate_okf.py`) or run from the skill directory.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REQUIRED_KEYS = [
    "type",
    "title",
    "description",
    "tags",
    "source_paths",
    "test_paths",
    "depends_on",
    "status",
    "last_updated",
]
VALID_STATUS = {"planned", "partial", "implemented"}
DATE_RE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
TOP_KEY_RE = re.compile(r"^([A-Za-z_][A-Za-z0-9_]*):\s*(.*)$")
MD_LINK_RE = re.compile(r"\]\(([^)\s]+)\)")


def parse_frontmatter(lines: list[str]) -> dict[str, str] | None:
    """Return top-level frontmatter keys/values, or None if no frontmatter."""
    if not lines or lines[0].strip() != "---":
        return None
    keys: dict[str, str] = {}
    for line in lines[1:]:
        if line.strip() == "---":
            return keys
        match = TOP_KEY_RE.match(line)
        if match:
            keys[match.group(1)] = match.group(2).strip()
    return None  # unterminated frontmatter reads as missing


def inline_list(value: str) -> list[str]:
    if not (value.startswith("[") and value.endswith("]")):
        return []
    inner = value[1:-1].strip()
    return [item.strip() for item in inner.split(",") if item.strip()]


def check_file(path: Path, display_root: Path, errors: list[str]) -> None:
    rel = path.relative_to(display_root)
    lines = path.read_text(encoding="utf-8").splitlines()
    front = parse_frontmatter(lines)
    if front is None:
        errors.append(f"{rel}: missing or unterminated YAML frontmatter")
        return

    for key in REQUIRED_KEYS:
        if key not in front:
            errors.append(f"{rel}: missing required frontmatter key '{key}'")

    status = front.get("status", "")
    if status and status not in VALID_STATUS:
        errors.append(f"{rel}: invalid status '{status}' (expected planned|partial|implemented)")

    date = front.get("last_updated", "")
    if date and not DATE_RE.match(date):
        errors.append(f"{rel}: last_updated '{date}' is not YYYY-MM-DD")

    for dep in inline_list(front.get("depends_on", "[]")):
        target = (path.parent / dep).resolve()
        if not target.exists():
            errors.append(f"{rel}: depends_on '{dep}' does not resolve")

    body = "\n".join(lines)
    for link in MD_LINK_RE.findall(body):
        if link.startswith(("http://", "https://", "mailto:", "#")):
            continue
        target = (path.parent / link.split("#")[0]).resolve()
        if not target.exists():
            errors.append(f"{rel}: link '{link}' does not resolve")


def check_agent_index(bundle: Path, errors: list[str]) -> None:
    """Resolve backticked bundle paths in an optional parent-level index."""
    index = bundle.parent / "AGENT_CONTEXT_INDEX.md"
    if not index.exists():
        return
    ref_re = re.compile(rf"`({re.escape(bundle.name)}/[^`]+\.md)`")
    body = index.read_text(encoding="utf-8")
    for ref in ref_re.findall(body):
        if not (bundle.parent / ref).exists():
            errors.append(f"{index.name}: '{ref}' does not resolve")


def main() -> int:
    bundle = Path(sys.argv[1] if len(sys.argv) > 1 else "context/codebase_okf").resolve()
    if not bundle.is_dir():
        print(f"ERROR bundle directory not found: {bundle}")
        return 1

    errors: list[str] = []
    files = sorted(bundle.rglob("*.md"))
    if not files:
        errors.append(f"no Markdown files found under {bundle}")
    display_root = bundle.parent
    for path in files:
        check_file(path, display_root, errors)
    check_agent_index(bundle, errors)

    if errors:
        for error in errors:
            print(f"ERROR {error}")
        print(f"\n{len(errors)} problem(s) in {len(files)} concept file(s).")
        return 1
    print(
        f"OK: {len(files)} concept files validated; "
        "all frontmatter, links, and index paths resolve."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
