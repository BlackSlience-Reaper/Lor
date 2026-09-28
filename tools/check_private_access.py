#!/usr/bin/env python3
"""Fail when code outside src/interop/ reaches members by name through reflection.

Vanilla non-public members go through src/interop/VanillaPrivate.cs, so a game update shows every missing member in
one startup summary instead of features silently degrading one by one. Exempt:
  - src/interop/ (the accessors themselves) and src/compat/ (version shims that probe by name on purpose);
  - Harmony target resolution: code inside TargetMethod / TargetMethods / Prepare (LibraryPatcher reports failures);
  - entries in tools/private_access_allowlist.txt (path, a substring of the line, and why it is not vanilla-private).
usage: tools/check_private_access.py <src-dir>
"""
import pathlib
import re
import sys

SRC = pathlib.Path(sys.argv[1])
ALLOWLIST = pathlib.Path(__file__).with_name("private_access_allowlist.txt")
EXEMPT_DIRS = ("interop/", "compat/")
TARGET_METHODS = {"TargetMethod", "TargetMethods", "Prepare"}

REFLECTION = re.compile(
    r'AccessTools\.(?:Field|DeclaredField|Property|DeclaredProperty|PropertyGetter|PropertySetter|Method|DeclaredMethod'
    r'|FieldRefAccess(?:<[^>]*>)?|StaticFieldRefAccess(?:<[^>]*>)?|TypeByName)\([^;]*"'
    r'|Traverse\.Create'
    r'|\.(?:GetField|GetProperty|GetMethod|GetNestedType)\(\s*"'
    r'|\.GetType\(\s*"')
MEMBER = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|internal|protected|static|readonly|override|async|sealed|virtual|new)\s+)+"
    r"[\w<>\[\],.?() ]+?\s+(\w+)\s*(?:\(|=|\{|$)")


def load_allowlist():
    entries = []
    for line in ALLOWLIST.read_text(encoding="utf-8").splitlines():
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        path, needle, reason = (line.split("\t") + ["", ""])[:3]
        if not reason.strip():
            sys.exit(f"{ALLOWLIST.name}: entry without a reason: {line}")
        entries.append((path.strip(), needle.strip()))
    return entries


def enclosing_member(lines, index):
    for i in range(index, max(-1, index - 120), -1):
        m = MEMBER.match(lines[i])
        if m:
            return m.group(1)
    return None


allow = load_allowlist()
used = set()
violations = []
for path in sorted(SRC.rglob("*.cs")):
    rel = path.relative_to(SRC).as_posix()
    if rel.startswith(EXEMPT_DIRS):
        continue
    lines = path.read_text(encoding="utf-8").splitlines()
    for index, line in enumerate(lines):
        # Calls are often wrapped: look at the statement's next two lines too, but only report where the call starts.
        window = " ".join(l.strip() for l in lines[index:index + 3])
        if line.lstrip().startswith(("//", "///", "*")) or not re.search(r"AccessTools\.|Traverse\.|\.Get(?:Field|Property|Method|NestedType|Type)\(", line):
            continue
        call = re.search(r"(AccessTools\.|Traverse\.|\.Get(?:Field|Property|Method|NestedType|Type)\().*", window)
        if not call or not REFLECTION.search(call.group(0).split(";")[0]):
            continue
        if enclosing_member(lines, index) in TARGET_METHODS:
            continue
        match = next((i for i, (p, needle) in enumerate(allow) if p == rel and needle in line), None)
        if match is not None:
            used.add(match)
            continue
        violations.append(f"src/{rel}:{index + 1}: {line.strip()}")

stale = [f"{allow[i][0]}\t{allow[i][1]}" for i in range(len(allow)) if i not in used]
if violations or stale:
    if violations:
        print("reflection by name outside src/interop/ (add a VanillaPrivate accessor, or allowlist it with a reason):")
        print("\n".join("  " + v for v in violations))
    if stale:
        print("allowlist entries that no longer match anything:")
        print("\n".join("  " + s for s in stale))
    sys.exit(1)
print(f"no reflection by name outside src/interop/ ({len(allow)} allowlisted)")
