"""Compare two headless patch tables for dispatcher equivalence.

For every target, the sequence of patches per kind is reduced to (owner, priority, before, after) and
consecutive LibraryOfRuina entries with identical (priority, before, after) are collapsed into one.
If the collapsed sequences match, every third-party patch keeps its position relative to ours.
usage: tools/compare_patch_table.py snapshots/headless/patch_table.txt <new_table>"""
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
        key = (kind, prio, owner, before, after)
        if owner == OWN and out and out[-1][0] == key:
            out[-1][1].append(method)
        else:
            out.append((key, [method]))
    return [k for k, _ in out]


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
    elif [p[5] for p in a] != [p[5] for p in b]:
        print("MERGED " + target.split("|", 1)[1][:110])
        print("   " + str(len(a)) + " -> " + str(len(b)) + " patches")
print("EQUIVALENT" if ok else "DIFFERENCES FOUND")
