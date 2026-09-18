# -*- coding: utf-8 -*-
"""配色对照：四套材质色的可量化差异

量四件事，都跟"做得好不好"直接相关：
  ① 面/框 明度差：面要能读出来比框亮（层级）
  ② 面色 vs 金色 的色相差与明度差：金能不能跳出来（强调可辨）
  ③ 文字可读性预演：在面色上放 #E8E1D2 与 #F0E6D2 两种字色，算对比度
  ④ 三档状态（default/hover/pressed）的面色明度差：状态分不分得开
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

OUT = os.path.join(HERE, "out")
STUDY = os.path.join(OUT, "study", "tone")
os.makedirs(STUDY, exist_ok=True)

import artkit as K

TXT_A = (232, 225, 210)
TXT_B = (240, 230, 210)
TXT_C = (245, 242, 235)
GOLD = K.GOLD


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def contrast(a, b):
    """WCAG 对比度。"""
    def rl(c):
        def f(v):
            v /= 255.0
            return v / 12.92 if v <= 0.03928 else ((v + 0.055) / 1.055) ** 2.4
        return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2])
    la, lb = rl(a), rl(b)
    if la < lb:
        la, lb = lb, la
    return (la + 0.05) / (lb + 0.05)


def sat(c):
    mx, mn = max(c), min(c)
    return 0.0 if mx == 0 else (mx - mn) / float(mx)


def main():
    sets = [
        ("现状 橄榄铜", dict(face=K.BTN_FIELD, frame=(66, 58, 47),
                        h=K.BTN_FIELD_HOVER, p=K.BTN_FIELD_PRESS,
                        sec=K.BTN_SEC_FIELD, tab=K.BTN_TAB_FIELD)),
        ("净石 暖灰", dict(face=(79, 74, 68), frame=(58, 54, 49),
                       h=(101, 95, 87), p=(60, 56, 51),
                       sec=(64, 60, 55), tab=(46, 43, 39))),
        ("冷灰岩", dict(face=(71, 75, 79), frame=(51, 55, 59),
                     h=(93, 97, 102), p=(53, 56, 60),
                     sec=(57, 61, 64), tab=(41, 44, 47))),
        ("铜锈", dict(face=(58, 71, 68), frame=(43, 53, 51),
                    h=(78, 93, 89), p=(43, 53, 51),
                    sec=(47, 58, 55), tab=(33, 42, 40))),
    ]

    L = []
    L.append("按钮配色对照 · 可量化差异")
    L.append("金色基准 GOLD = %s  lum %.1f" % (GOLD, lum(GOLD)))
    L.append("")
    hdr = ("%-12s %7s %7s %6s %7s %6s %7s %6s %6s"
           % ("配色", "面lum", "框lum", "面-框", "面色相", "饱和",
              "金/面比", "字A比", "字C比"))
    L.append(hdr)
    L.append("-" * len(hdr))

    for nm, s in sets:
        f, fr = s["face"], s["frame"]
        lf, lfr = lum(f), lum(fr)
        gap = lf - lfr
        # 面色相
        import colorsys
        hh, ss, vv = colorsys.rgb_to_hsv(f[0] / 255., f[1] / 255., f[2] / 255.)
        L.append("%-12s %7.1f %7.1f %6.1f %7.0f %6.2f %7.2f %6.2f %6.2f"
                 % (nm, lf, lfr, gap, hh * 360, ss,
                    contrast(GOLD, f), contrast(TXT_A, f), contrast(TXT_C, f)))

    L.append("")
    L.append("判据参考：面-框 明度差 ≥ +8 才读得出层级；字/面 对比度 ≥ 4.5 为可读，")
    L.append("          ≥ 7.0 为舒适。金/面 对比度 ≥ 3.0 金才跳得出来。")
    L.append("")
    L.append("=" * 74)
    L.append("三档状态分开程度（面色明度）")
    L.append("=" * 74)
    for nm, s in sets:
        ds = [("default", s["face"]), ("hover", s["h"]), ("pressed", s["p"])]
        vals = "  ".join("%s %.0f" % (k, lum(v)) for k, v in ds)
        d1 = lum(s["h"]) - lum(s["face"])
        d2 = lum(s["face"]) - lum(s["p"])
        L.append("%-12s %s   ⇒ hover +%.0f / pressed -%.0f"
                 % (nm, vals, d1, d2))
    L.append("")
    L.append("判据参考：hover ≥ +12、pressed ≥ -10 才在 1× 下看得出状态切换。")
    L.append("")
    L.append("=" * 74)
    L.append("层级关系（主 vs 次 vs 页签）")
    L.append("=" * 74)
    for nm, s in sets:
        L.append("%-12s 主 %.0f  次 %.0f  页签 %.0f   ⇒ 主-次 %+.0f  主-页签 %+.0f"
                 % (nm, lum(s["face"]), lum(s["sec"]), lum(s["tab"]),
                    lum(s["face"]) - lum(s["sec"]),
                    lum(s["face"]) - lum(s["tab"])))
    L.append("")
    L.append("判据参考：主按钮应最重，主-次 ≥ +6；页签应最轻。")
    L.append("")

    path = os.path.join(STUDY, "color_compare.txt")
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("\n".join(L))
    print("saved", path)
    print("\n".join(L))


if __name__ == "__main__":
    main()
