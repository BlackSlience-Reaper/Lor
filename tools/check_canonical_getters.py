#!/usr/bin/env python3
"""Flag getters that canonical models (compendium, ModelDb iteration by other mods) evaluate but
that read instance state without an IsMutable/IsCanonical guard.

Vanilla PowerModel/CardModel/RelicModel.Owner asserts mutability, so even `Owner?.X` throws on a
canonical model. Exit code 1 when anything is found.
usage: tools/check_canonical_getters.py [src-dir]
"""
import os
import re
import sys

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "src")
MEMBERS = r"(DisplayAmount|ShowCounter|ExtraHoverTips|CanonicalVars|SmartDescriptionLocKey|Title|Description|GetBaseValueForIConvertible)"
DECL = re.compile(r"(?:override|virtual)\s+[\w<>\[\]?., ]+\s" + MEMBERS + r"\b(\s*\(\s*\))?\s*(=>|\{|\n)")
UNSAFE = re.compile(r"(?<![\w.])(Owner|CombatState)\b|\bGetInternalData<|\bpower\.Owner\b")
GUARD = re.compile(r"\bIsMutable\b|\bIsCanonical\b")


def body_at(text, start):
    """Return the member body starting at `start` (just after the declaration)."""
    i = start
    while i < len(text) and text[i] in " \t\r\n":
        i += 1
    if text.startswith("=>", i) or text[start - 2:start] == "=>":
        end = text.find(";", i)
        return text[i:end]
    brace = text.find("{", start - 1)
    depth = 0
    for j in range(brace, len(text)):
        if text[j] == "{":
            depth += 1
        elif text[j] == "}":
            depth -= 1
            if depth == 0:
                return text[brace:j + 1]
    return text[brace:]


findings = []
for dirpath, dirs, files in os.walk(ROOT):
    if os.sep + "debug" in dirpath:
        continue
    for name in files:
        if not name.endswith(".cs"):
            continue
        path = os.path.join(dirpath, name)
        text = open(path, encoding="utf-8-sig").read()
        for match in DECL.finditer(text):
            body = body_at(text, match.end())
            code = re.sub(r"//[^\n]*", "", body)
            if UNSAFE.search(code) and not GUARD.search(code):
                line = text.count("\n", 0, match.start()) + 1
                findings.append(f"{os.path.relpath(path, ROOT)}:{line}: {match.group(1)} reads instance state without IsMutable guard")

print("\n".join(findings) if findings else "no unguarded canonical getters")
sys.exit(1 if findings else 0)
