#!/usr/bin/env python3
"""有状态的静态字段都必须在 tools/static_state.txt 登记生命周期类别（重构指导 §4.5，设计哲学 §4）。

usage: check_static_state.py <static_fields.txt> <static_state.txt>

“有状态”指可变静态字段，以及类型是可变容器的只读静态字段：集合、弱表、AsyncLocal/ThreadLocal、Random、
RitsuLib 挂载状态、临时地图会话仓库、RunScoped/CombatScoped。只读数组与 IReadOnly* 字段按常量处理，不登记；
自定义的可变仓库类型要加进下面的正则才会被检查。
"""
import re
import sys

STATEFUL_READONLY = re.compile(
    r"^(System\.(Collections\.Generic\.(Dictionary|HashSet|List|Queue|Stack|SortedDictionary|SortedSet|LinkedList)`"
    r"|Collections\.Concurrent\.|Runtime\.CompilerServices\.(ConditionalWeakTable|StrongBox)`"
    r"|Threading\.(AsyncLocal|ThreadLocal)`|Random$)"
    r"|STS2RitsuLib\.Utils\.SavedAttachedState`"
    r"|LibraryOfRuina\.features\.temporarymaps\.TemporaryMapSessionStore$"
    r"|LibraryOfRuina\.infra\.lifecycle\.(RunScoped|CombatScoped)`)")
CATEGORIES = {"run", "combat", "instance", "harmless"}
HARMLESS_KINDS = {"cache", "once", "log", "ui", "settings", "scope", "registry", "net"}
CONTAINER_CATEGORY = {
    "LibraryOfRuina.infra.lifecycle.RunScoped`": "run",
    "LibraryOfRuina.infra.lifecycle.CombatScoped`": "combat",
}


def main() -> int:
    fields_path, table_path = sys.argv[1], sys.argv[2]
    stateful = {}
    with open(fields_path, encoding="utf-8") as handle:
        for line in handle:
            mutability, name, type_name = line.rstrip("\n").split("\t")
            if mutability == "mutable" or STATEFUL_READONLY.match(type_name):
                stateful[name] = type_name

    errors = []
    table = {}
    with open(table_path, encoding="utf-8") as handle:
        for number, line in enumerate(handle, 1):
            line = line.rstrip("\n")
            if not line or line.startswith("#"):
                continue
            parts = line.split("\t")
            if len(parts) != 4 or not parts[3].strip():
                errors.append(f"{table_path}:{number}: expected 4 tab-separated columns with a note")
                continue
            name, category, kind, _ = parts
            if name in table:
                errors.append(f"{table_path}:{number}: duplicate entry {name}")
            table[name] = (category, kind)
            if category not in CATEGORIES:
                errors.append(f"{table_path}:{number}: unknown category '{category}' for {name}")
            elif category == "harmless" and kind not in HARMLESS_KINDS:
                errors.append(f"{table_path}:{number}: harmless field {name} needs a kind from {sorted(HARMLESS_KINDS)}")
            elif category != "harmless" and kind != "-":
                errors.append(f"{table_path}:{number}: {category} field {name} takes '-' as its kind")

    for name in sorted(set(stateful) - set(table)):
        errors.append(f"unregistered: {name}\t{stateful[name]}")
    for name in sorted(set(table) - set(stateful)):
        errors.append(f"stale: {name} is no longer a stateful static field")
    for name, type_name in sorted(stateful.items()):
        for prefix, expected in CONTAINER_CATEGORY.items():
            if type_name.startswith(prefix) and name in table and table[name][0] != expected:
                errors.append(f"{name} is a {prefix.rsplit('.', 1)[1].rstrip('`')} and must be registered as {expected}")

    if errors:
        print("static state registry (tools/static_state.txt) is out of date:", file=sys.stderr)
        for error in errors:
            print("  " + error, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
