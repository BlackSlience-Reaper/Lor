#!/usr/bin/env python3
"""src/ 里完整的 "res://…" 字面量必须指向存在的资源（重构指导 §5.2）。

usage: check_res_paths.py                          检查；有坏路径时列出并以 1 退出
       check_res_paths.py --stats                  检查并打印字面量分类计数
       check_res_paths.py --generate <原版.pck> <LibraryOfRuinaLib.pck>
                                                   按 PCK 文件表重写 tools/vanilla_res_paths.txt 与
                                                   tools/library_res_paths.txt，并列出两边都找不到的路径

判定规则：
- 只检查“完整”字面量：不是插值字符串（$"…"），两侧都没有 `+`，不含 `{`（string.Format 模板）。
  字面量按 C# 运行期值判断（普通字符串解码转义，verbatim 还原 "" ，原始字符串去缩进与首尾换行），每次运行先跑解码自检。
  其余含 res:// 的字面量按“拼接/插值/模板”计数，不检查——它们的最终路径在运行时才确定。
- 最后一段没有扩展名的完整字面量是前缀常量（目录，或 BossNodePath 这类由原版补 .png 的文件名前缀），
  检查至少有一个文件以它开头。
- 文件是否存在按 `git ls-files` 判断，大小写敏感：macOS 文件系统不区分大小写，但 Godot 在 PCK 里按原样查找，
  大小写不对的路径在玩家机器上加载不到。工作树里 LFS 素材是指针文件，只看路径不看内容。
- res:// 映射到仓库根；仓库里没有的，必须列在 vanilla_res_paths.txt（原版 PCK）、library_res_paths.txt
  （前置库 LibraryOfRuinaLib 的 PCK）或 optional_res_paths.txt（手工维护：代码先探测再读、允许不存在的路径）里。
- PCK 里导入过的资源只有 `<路径>.import` / `<路径>.remap`，两者都算存在。原版 `images/atlases/<图集>.sprites/<名>.tres`
  不是真实文件，由原版 AtlasResourceLoader 按 `<图集>.tpsheet` 的精灵表虚拟出来，生成清单时按精灵表判断。
"""
import bisect
import json
import os
import re
import struct
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "src")
VANILLA_LIST = os.path.join(ROOT, "tools", "vanilla_res_paths.txt")
LIBRARY_LIST = os.path.join(ROOT, "tools", "library_res_paths.txt")
OPTIONAL_LIST = os.path.join(ROOT, "tools", "optional_res_paths.txt")
PREFIX = "res://"
ATLAS_SPRITE = re.compile(r"^images/atlases/([^/]+)\.sprites/(.+)\.tres$")


SIMPLE_ESCAPES = {"'": "'", '"': '"', "\\": "\\", "0": "\0", "a": "\a", "b": "\b", "f": "\f",
                  "n": "\n", "r": "\r", "t": "\t", "v": "\v", "e": "\x1b"}


def decode_regular(body):
    """普通字符串字面量的运行期值：处理 \\n、\\uXXXX、\\UXXXXXXXX、\\xH{1,4} 等转义。"""
    out, k, n = [], 0, len(body)
    while k < n:
        ch = body[k]
        if ch != "\\" or k + 1 >= n:
            out.append(ch)
            k += 1
            continue
        esc = body[k + 1]
        if esc in SIMPLE_ESCAPES:
            out.append(SIMPLE_ESCAPES[esc])
            k += 2
        elif esc == "u":
            out.append(chr(int(body[k + 2:k + 6], 16)))
            k += 6
        elif esc == "U":
            out.append(chr(int(body[k + 2:k + 10], 16)))
            k += 10
        elif esc == "x":
            m = re.match(r"[0-9A-Fa-f]{1,4}", body[k + 2:])
            out.append(chr(int(m.group(0), 16)))
            k += 2 + len(m.group(0))
        else:
            out.append(esc)
            k += 2
    return "".join(out)


def decode_raw(body):
    """原始字符串（\"\"\"…\"\"\"）的运行期值。多行形式：去掉开头引号后的换行与结尾引号前的换行，
    每行去掉与结尾引号所在行相同的前导空白（C# 11 规则）；单行形式原样。"""
    if "\n" not in body:
        return body
    lines = body.split("\n")
    indent = lines[-1] if lines[-1].strip() == "" else ""
    inner = lines[1:-1]
    return "\n".join(line[len(indent):] if line.startswith(indent) else line.lstrip() for line in inner).replace("\r", "")


def read_literal(text, i, out):
    """text[i] 是 $、@ 或 "：若从这里开始一个字符串字面量，把它（以及插值洞里的嵌套字面量）追加到 out，
    元素为 (起点, 终点, 是否插值, 运行期值)，返回终点；不是字面量时返回 i + 1。
    运行期值按 C# 规则解码：普通字符串处理转义，verbatim 把 \"\" 还原成 \"，原始字符串去掉缩进与首尾换行。"""
    n = len(text)
    j = i
    interpolated = verbatim = False
    while j < n and text[j] in "$@":
        interpolated |= text[j] == "$"
        verbatim |= text[j] == "@"
        j += 1
    if j >= n or text[j] != '"':
        return i + 1
    if text.startswith('"""', j):
        quotes = 3
        while text.startswith('"', j + quotes):
            quotes += 1
        delimiter = '"' * quotes
        end = text.find(delimiter, j + quotes)
        end = n if end < 0 else end + quotes
        out.append((i, end, interpolated, decode_raw(text[j + quotes:end - quotes])))
        return end
    j += 1
    start = j
    depth = 0
    while j < n:
        ch = text[j]
        if depth == 0:
            if ch == '"':
                if verbatim and text.startswith('""', j):
                    j += 2
                    continue
                break
            if not verbatim and ch == "\\":
                j += 2
                continue
            if interpolated and ch == "{":
                if text.startswith("{{", j):
                    j += 2
                    continue
                depth = 1
            j += 1
        elif ch == "{":
            depth += 1
            j += 1
        elif ch == "}":
            depth -= 1
            j += 1
        elif ch in '$@"':
            j = read_literal(text, j, out)
        elif ch == "'":
            j = skip_char(text, j)
        else:
            j += 1
    body = text[start:j]
    value = body.replace('""', '"') if verbatim else decode_regular(body)
    out.append((i, j + 1, interpolated, value))
    return j + 1


SELF_TEST_CASES = [
    # (C# 源码片段, 期望的字面量运行期值)
    ('var a = "res://a/b.png";', ["res://a/b.png"]),
    ('var a = @"res://a/""q"".png";', ['res://a/"q".png']),
    ('var a = "r\\u0065s://esc/u.png";', ["res://esc/u.png"]),
    ('var a = "res:\\x2F/esc/x.png";', ["res://esc/x.png"]),
    ('var a = """res://raw/one.png""";', ["res://raw/one.png"]),
    ('const string P = """\n    res://raw/multi.png\n    """;', ["res://raw/multi.png"]),
    ('var a = """"res://raw/"""quoted.png"""";', ['res://raw/"""quoted.png']),
    ('// "res://comment.png"\nvar c = \'"\'; var a = "res://after/char.png";', ["res://after/char.png"]),
]


def self_test():
    """字面量解码的自检：普通、verbatim、转义、单行与多行原始字符串、注释与字符字面量。"""
    failures = []
    for source, expected in SELF_TEST_CASES:
        got = [value for _, _, _, value in iter_string_literals(source)]
        if got != expected:
            failures.append(f"{source!r}: expected {expected!r}, got {got!r}")
    for failure in failures:
        print(f"check_res_paths self-test failed: {failure}", file=sys.stderr)
    return not failures


def skip_char(text, i):
    j = i + 1
    while j < len(text) and text[j] != "'":
        j += 2 if text[j] == "\\" else 1
    return j + 1


def iter_string_literals(text):
    """C# 词法的最小子集：跳过注释与字符字面量，产出字符串字面量（见 read_literal）。"""
    i, n = 0, len(text)
    out = []
    while i < n:
        if text.startswith("//", i):
            j = text.find("\n", i)
            i = n if j < 0 else j
        elif text.startswith("/*", i):
            j = text.find("*/", i + 2)
            i = n if j < 0 else j + 2
        elif text[i] == "'":
            i = skip_char(text, i)
        elif text[i] in '$@"':
            i = read_literal(text, i, out)
        else:
            i += 1
    return out


def neighbour(text, index, step):
    while 0 <= index < len(text) and text[index] in " \t\r\n":
        index += step
    return text[index] if 0 <= index < len(text) else ""


def collect():
    """返回 (完整字面量 [(路径, 文件:行)], 未检查的计数 {类别: 数量})。"""
    checked, skipped = [], {"interpolated": 0, "concatenated": 0, "format": 0}
    for folder, _, files in os.walk(SRC):
        for name in sorted(files):
            if not name.endswith(".cs"):
                continue
            path = os.path.join(folder, name)
            with open(path, encoding="utf-8-sig") as handle:
                text = handle.read()
            for start, end, interpolated, content in iter_string_literals(text):
                if not content.startswith(PREFIX):
                    continue
                where = f"{os.path.relpath(path, ROOT)}:{text.count(chr(10), 0, start) + 1}"
                if interpolated:
                    skipped["interpolated"] += 1
                elif neighbour(text, start - 1, -1) == "+" or neighbour(text, end, 1) == "+":
                    skipped["concatenated"] += 1
                elif "{" in content:
                    skipped["format"] += 1
                else:
                    checked.append((content, where))
    return checked, skipped


def is_prefix(rel):
    return "." not in rel.rsplit("/", 1)[-1]


class FileTable:
    """大小写敏感的文件表。sprites 是 {图集名: 精灵键集合}，只有原版 PCK 有。"""

    def __init__(self, names, sprites=None):
        self.names = set(names)
        self.sorted = sorted(self.names)
        self.sprites = sprites or {}

    def has(self, rel):
        if is_prefix(rel):
            k = bisect.bisect_left(self.sorted, rel)
            return k < len(self.sorted) and self.sorted[k].startswith(rel)
        atlas = ATLAS_SPRITE.match(rel)
        if atlas and atlas.group(1) in self.sprites:
            return atlas.group(2) in self.sprites[atlas.group(1)]
        return rel in self.names or rel + ".import" in self.names or rel + ".remap" in self.names


def repo_files():
    out = subprocess.run(["git", "-C", ROOT, "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
                         check=True, capture_output=True).stdout.decode("utf-8")
    return FileTable(f for f in out.split("\0") if f)


def read_list(path):
    if not os.path.exists(path):
        return set()
    with open(path, encoding="utf-8") as handle:
        return {line.split("\t")[0].strip() for line in handle if line.strip() and not line.startswith("#")}


def read_pck(path):
    """Godot 4 PCK（格式 2/3，目录不加密）的文件表，只读；顺带按 .tpsheet 展开图集精灵。"""
    entries = {}
    with open(path, "rb") as f:
        assert f.read(4) == b"GDPC", f"{path}: not a Godot PCK"
        fmt, _, _, _, flags = struct.unpack("<5I", f.read(20))
        file_base = struct.unpack("<Q", f.read(8))[0]
        if fmt >= 3:
            f.seek(struct.unpack("<Q", f.read(8))[0])
        else:
            f.read(16 * 4)
        assert not (flags & 1), f"{path}: encrypted directory"
        relative = fmt >= 3 or bool(flags & 2)
        for _ in range(struct.unpack("<I", f.read(4))[0]):
            size = struct.unpack("<I", f.read(4))[0]
            name = f.read(size).rstrip(b"\0").decode("utf-8")
            offset, length = struct.unpack("<QQ", f.read(16))
            f.read(16 + (4 if fmt >= 2 else 0))
            entries[name[len(PREFIX):] if name.startswith(PREFIX) else name] = (
                offset + (file_base if relative else 0), length)
        sprites = {}
        for name, (offset, length) in entries.items():
            sheet = re.match(r"^images/atlases/([^/]+)\.tpsheet$", name)
            if not sheet:
                continue
            f.seek(offset)
            data = json.loads(f.read(length).decode("utf-8"))
            sprites[sheet.group(1)] = {
                s["filename"][:-4] if s["filename"].lower().endswith(".png") else s["filename"]
                for texture in data["textures"] for s in texture["sprites"]}
    return FileTable(entries, sprites)


HEADERS = {
    VANILLA_LIST: "# 本模组引用、且存在于原版 PCK（SlayTheSpire2.app/Contents/Resources/Slay the Spire 2.pck）的资源路径。\n",
    LIBRARY_LIST: "# 本模组引用、且存在于前置库 LibraryOfRuinaLib 的 PCK（创意工坊 3747541096）的资源路径。\n",
}
FOOTER = ("# 由 tools/check_res_paths.py --generate <原版.pck> <LibraryOfRuinaLib.pck> 生成，tools/check.sh 核对；\n"
          "# 只收 src/ 里完整字面量引用到的路径。游戏或前置库更新后重新生成，diff 里消失的行就是上游删掉或改名的资源。\n")


def main():
    args = sys.argv[1:]
    if not self_test():
        return 1
    checked, skipped = collect()
    repo = repo_files()
    missing = sorted({(p, w) for p, w in checked if not repo.has(p[len(PREFIX):])})
    optional = read_list(OPTIONAL_LIST)

    if args[:1] == ["--generate"]:
        vanilla, library = read_pck(args[1]), read_pck(args[2])
        buckets = {VANILLA_LIST: set(), LIBRARY_LIST: set()}
        for path, where in missing:
            rel = path[len(PREFIX):]
            if vanilla.has(rel):
                buckets[VANILLA_LIST].add(path)
            elif library.has(rel):
                buckets[LIBRARY_LIST].add(path)
            elif path not in optional:
                print(f"NOT FOUND\t{path}\t{where}")
        for target, paths in buckets.items():
            with open(target, "w", encoding="utf-8") as handle:
                handle.write(HEADERS[target] + FOOTER + "".join(p + "\n" for p in sorted(paths)))
        return 0

    listed = read_list(VANILLA_LIST) | read_list(LIBRARY_LIST) | optional
    bad = [(p, w) for p, w in missing if p not in listed]
    stale = sorted(listed - {p for p, _ in checked})
    if "--stats" in args:
        prefixes = sum(1 for p, _ in checked if is_prefix(p[len(PREFIX):]))
        print(f"complete literals: {len(checked)} ({prefixes} prefixes, {len({p for p, _ in checked})} distinct), "
              f"resolved by lists: {len(missing) - len(bad)}")
        print("unchecked: " + ", ".join(f"{k} {v}" for k, v in skipped.items()))
    for path, where in bad:
        print(f"missing resource\t{path}\t{where}", file=sys.stderr)
    for path in stale:
        print(f"stale list entry (no longer referenced)\t{path}", file=sys.stderr)
    return 1 if bad or stale else 0


if __name__ == "__main__":
    sys.exit(main())
