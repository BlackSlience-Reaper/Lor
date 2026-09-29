"""Fail when a character in the mod's zhs localization is missing from the subset story font.
Fix by rerunning tools/subset_zhs_font.py against the full font."""
import sys

try:
    from fontTools.ttLib import TTFont
except ImportError:
    sys.exit("check_zhs_font.py needs fontTools: pip install fonttools")

from zhs_font_charset import FONT, required

cmap = TTFont(FONT, lazy=True).getBestCmap()
missing = sorted(c for c in required() if ord(c) not in cmap and not c.isspace())
if missing:
    print(f"{FONT.name} is missing {len(missing)} character(s) used in zhs localization: "
          + " ".join(f"{c}(U+{ord(c):04X})" for c in missing[:40]), file=sys.stderr)
    print("rerun: python3 tools/subset_zhs_font.py <full NotoSansCJKsc-Regular.otf>", file=sys.stderr)
    sys.exit(1)
print(f"zhs story font covers all {len(required())} characters used in zhs localization")
