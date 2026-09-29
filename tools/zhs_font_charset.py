"""Character set the Simplified Chinese story font must cover: GB2312, ASCII, common punctuation blocks, and every
character that appears in the mod's zhs localization. Shared by subset_zhs_font.py and check_zhs_font.py."""
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ZHS_DIR = ROOT / "LibraryOfRuina" / "localization" / "zhs"
FONT = ROOT / "fonts" / "NotoSansCJKsc-Regular.otf"


def gb2312() -> set[str]:
    chars = set()
    for high in range(0xA1, 0xF8):
        for low in range(0xA1, 0xFF):
            try:
                chars.add(bytes([high, low]).decode("gb2312"))
            except UnicodeDecodeError:
                pass
    return chars


def fixed_ranges() -> set[str]:
    ranges = [
        (0x20, 0x7E),      # ASCII
        (0xA0, 0xFF),      # Latin-1 supplement
        (0x2000, 0x206F),  # general punctuation
        (0x2190, 0x21FF),  # arrows
        (0x2460, 0x24FF),  # enclosed alphanumerics
        (0x25A0, 0x25FF),  # geometric shapes
        (0x2600, 0x26FF),  # miscellaneous symbols
        (0x3000, 0x303F),  # CJK symbols and punctuation
        (0xFF00, 0xFFEF),  # halfwidth and fullwidth forms
    ]
    return {chr(c) for start, end in ranges for c in range(start, end + 1)}


def localization_chars() -> set[str]:
    chars = set()
    for path in sorted(ZHS_DIR.rglob("*.json")):
        chars.update(path.read_text(encoding="utf-8"))
    return {c for c in chars if c.isprintable() or c == "　"}


def required() -> set[str]:
    return localization_chars()


def subset_charset() -> set[str]:
    return gb2312() | fixed_ranges() | localization_chars()
