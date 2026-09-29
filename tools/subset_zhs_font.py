"""Regenerate fonts/NotoSansCJKsc-Regular.otf as a subset of the full Noto Sans CJK SC Regular.

The story player only draws zhs text with this font, so the subset keeps GB2312, ASCII, common punctuation and
every character in LibraryOfRuina/localization/zhs (see zhs_font_charset.py). Glyph outlines, hinting, metrics and
all OpenType layout features are kept, so covered characters render exactly as with the full font.

When new zhs text needs a character outside the subset, tools/check_zhs_font.py fails; rerun this script against
the full font (Noto Sans CJK SC Regular, Version 1.004, from https://github.com/notofonts/noto-cjk):

    python3 tools/subset_zhs_font.py /path/to/NotoSansCJKsc-Regular.otf
"""
import sys

from fontTools import subset
from fontTools.ttLib import TTFont

from zhs_font_charset import FONT, subset_charset


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    source = TTFont(sys.argv[1])
    if len(source.getBestCmap()) < 40000:
        raise SystemExit("source looks like a subset already; pass the full Noto Sans CJK SC Regular")
    options = subset.Options()
    options.layout_features = ["*"]
    options.name_IDs = ["*"]
    options.name_languages = ["*"]
    options.name_legacy = True
    options.notdef_outline = True
    options.glyph_names = False
    options.hinting = True
    options.legacy_kern = True
    options.symbol_cmap = True
    options.prune_unicode_ranges = False
    subsetter = subset.Subsetter(options)
    subsetter.populate(unicodes=[ord(c) for c in subset_charset()])
    subsetter.subset(source)
    source.save(FONT)
    print(f"wrote {FONT} ({len(source.getBestCmap())} code points, {FONT.stat().st_size / 2**20:.1f} MB)")


if __name__ == "__main__":
    main()
