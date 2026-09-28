"""Compare two headless patch tables (LOR_DUMP_PATCHES) for dispatcher equivalence.

For every target and patch kind, consecutive LibraryOfRuina entries with identical (priority, before, after)
are collapsed into one slot; third-party entries keep their full identity (owner and patch method). If the
resulting sequences match, no third-party patch moved relative to ours or to each other. It does not prove
that a dispatcher calls every handler it replaced; review the dispatcher body for that.
usage: tools/compare_patch_table.py snapshots/headless/patch_table.txt <new_table>
exit code: 0 when equivalent, 1 otherwise."""
import re
import sys

OWN = "FYY.LibraryOfRuina"


def parse(path):
    targets, cur = {}, None
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if not line or line.startswith("#"):
            continue
        if not line.startswith("  "):
            cur = targets.setdefault(line, [])
            continue
        m = re.match(r"\s+(\w+) priority=(-?\d+) index=\d+ owner=(\S+)((?: before=\S+)?)((?: after=\S+)?) (\S+)$", line)
        assert m, line
        kind, prio, owner, before, after, method = m.groups()
        cur.append((kind, prio, owner, before.strip(), after.strip(), method))
    return targets


def collapse(patches):
    out = []
    for kind, prio, owner, before, after, method in patches:
        if owner == OWN:
            key = (kind, prio, OWN, before, after)
            if not out or out[-1] != key:
                out.append(key)
        else:
            out.append((kind, prio, owner, before, after, method))
    return out


old, new = parse(sys.argv[1]), parse(sys.argv[2])
ok = True
for target in sorted(set(old) | set(new)):
    a, b = old.get(target), new.get(target)
    if a is None or b is None:
        ok = False
        print(("ONLY-NEW " if a is None else "ONLY-OLD ") + target)
        continue
    if collapse(a) != collapse(b):
        ok = False
        print("ORDER " + target)
        for line in collapse(a):
            print("   old", line)
        for line in collapse(b):
            print("   new", line)
    elif a != b:
        own_a = sum(1 for p in a if p[2] == OWN)
        own_b = sum(1 for p in b if p[2] == OWN)
        print("MERGED " + target.split("|", 1)[1][:110])
        print(f"   LibraryOfRuina patches {own_a} -> {own_b}")
print("EQUIVALENT" if ok else "DIFFERENCES FOUND")
sys.exit(0 if ok else 1)
