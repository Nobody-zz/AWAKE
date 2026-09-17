# -*- coding: utf-8 -*-
"""核「村庄 / 城堡 / 城镇」这三个泛词在索引里到底从哪来、覆盖多宽、查询实况如何。

只读真件：ModuleData/Worldbook/packages/calradia/runtime.json（上线包）
＋ 真索引件 WorldbookKeywordIndex（由 probe 侧边导出读取，若存在）。
"""
import io
import json
import sys

sys.stdout.reconfigure(encoding="utf-8")

PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"
pkg = json.load(io.open(PKG, encoding="utf-8"))
entries = pkg["entries"]
PFX = "awake:entry:"


def short(i):
    return i.replace(PFX, "")


def kw_of(e):
    k = e.get("keywords")
    if isinstance(k, dict):
        return k.get("zh-CN") or []
    return k or []


def tt_of(e):
    t = e.get("title")
    if isinstance(t, dict):
        return t.get("zh-CN") or ""
    return t or ""


GENERIC = ["村庄", "城堡", "城镇"]

# 1. 每个泛词：出现在多少条的 keywords 里（原样、按整词）
print("=" * 78)
print("① 泛词作为**整词**出现在 keywords 里的条目数")
for g in GENERIC:
    hit = [short(e["id"]) for e in entries if g in kw_of(e)]
    print("   「%s」 出现在 %d 条的 keywords 里" % (g, len(hit)))
    if hit[:3]:
        print("       例：%s" % "、".join(hit[:3]))

# 2. 每个泛词：作为**子串**出现在多少条的 keywords 里
print("\n② 泛词作为**子串**出现在 keywords 里的条目数（含「XX·村庄」这类）")
for g in GENERIC:
    hit = [(short(e["id"]), [w for w in kw_of(e) if g in w]) for e in entries if any(g in w for w in kw_of(e))]
    print("   「%s」 子串命中 %d 条" % (g, len(hit)))
    for i, ws in hit[:5]:
        print("       %-40s %s" % (i, ws))

# 3. 泛词是否出现在 title 里
print("\n③ 泛词出现在**条目名**里的条目数")
for g in GENERIC:
    hit = [short(e["id"]) for e in entries if g in tt_of(e)]
    print("   「%s」 在 %d 条的 title 里" % (g, len(hit)))
    for i in hit[:5]:
        e = next(x for x in entries if short(x["id"]) == i)
        print("       %-40s title=%s" % (i, tt_of(e)))

# 4. 一个村庄条目的完整入口面（看泛词是不是它自己的 kw）
print("\n④ 样例：三个村庄／城堡／城镇条目的完整入口面")
for want in ["geography.villages-deriat", "geography.villages-ab-comer",
             "geography.castles-takor-castle", "geography.towns-akkalat"]:
    e = next((x for x in entries if short(x["id"]) == want), None)
    if not e:
        print("   %s 不在包里" % want)
        continue
    print("   %-38s title=「%s」" % (want, tt_of(e)))
    print("        %s keywords=%s" % (" " * 30, kw_of(e)))

# 5. 「村庄」在别的条目 keywords 里的**全部原始形态**
print("\n⑤ 含「村庄」子串的 keywords 原始形态（去重）")
forms = {}
for e in entries:
    for w in kw_of(e):
        if "村庄" in w:
            forms.setdefault(w, []).append(short(e["id"]))
for w, owners in sorted(forms.items(), key=lambda kv: -len(kv[1])):
    print("   「%s」 %d 条" % (w, len(owners)))

print("\n⑥ 「村」「庄」「城」「堡」「镇」单字是否作为 keywords 出现")
for c in ["村", "庄", "城", "堡", "镇"]:
    n = sum(1 for e in entries if any(w == c for w in kw_of(e)))
    print("   单字「%s」作为整词：%d 条" % (c, n))
