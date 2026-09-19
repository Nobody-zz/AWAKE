"""_probe_sprite_tpac_20260919.py —— 弄清「图集的正像素」到底从哪个文件来。

要回答的问题：
  SimpleBank 是本机唯一「四件套齐全」的样本，它同时有
      AssetSources/GauntletUI/Bank_1.png      （源图）
      Assets/GauntletUI/Bank_1_tex.tpac       （479 B，薄壳）
      AssetPackages/pack0.tpac                （37,890 B）
  到底运行时是从 pack0.tpac 取像素，还是拿 _tex.tpac 里那条路径去读松散 PNG？

做法：把两个 tpac 里的可读串打出来看。
只读，不改。
"""
import os
import re
import struct

GAME = os.environ.get(
    "AWAKE_GAME",
    r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord")

TARGETS = [
    r"SimpleBank\AssetPackages\pack0.tpac",
    r"SimpleBank\Assets\GauntletUI\Bank_1_tex.tpac",
    r"Leverage\AssetSources\GauntletUI",
    r"AWAKE\AssetSources\GauntletUI",
]


def strings(b, minlen=4):
    return [m.group(0).decode("latin1")
            for m in re.finditer(rb"[\x20-\x7e]{%d,}" % minlen, b)]


def dump_tpac(rel):
    p = os.path.join(GAME, "Modules", rel)
    if not os.path.isfile(p):
        print("  （没有这个文件：%s）" % rel)
        return
    b = open(p, "rb").read()
    print("\n--- %s  %d B  magic=%r" % (rel, len(b), b[:4]))
    head = b[:64]
    print("    head hex: %s" % head.hex(" "))
    if len(b) >= 16:
        print("    前 4 个大端 uint32: %s"
              % [struct.unpack(">I", b[i:i + 4])[0] for i in (0, 4, 8, 12)])
    for s in strings(b)[:40]:
        print("    %r" % s)


def main():
    for t in TARGETS:
        if t.lower().endswith(".tpac"):
            dump_tpac(t)
        else:
            d = os.path.join(GAME, "Modules", t)
            print("\n--- %s/ （列目录）" % t)
            if os.path.isdir(d):
                for fn in sorted(os.listdir(d)):
                    fp = os.path.join(d, fn)
                    print("    %-40s %d B" % (fn, os.path.getsize(fp)))
            else:
                print("    （无此目录）")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
