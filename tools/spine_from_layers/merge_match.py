"""合并多次 match_all.py 的结果，去掉没被采用的变体。用法：merge_match.py 输出 输入... --drop 名字,..."""
import json
import sys
from pathlib import Path

args = sys.argv[1:]
drop = set()
if "--drop" in args:
    i = args.index("--drop")
    drop = set(args[i + 1].split(","))
    args = args[:i]
out, inputs = Path(args[0]), args[1:]
merged = {}
for p in inputs:
    merged.update(json.loads(Path(p).read_text()))
for n in drop:
    merged.pop(n, None)
out.write_text(json.dumps(merged, ensure_ascii=False, indent=1))
print(sorted(merged))
