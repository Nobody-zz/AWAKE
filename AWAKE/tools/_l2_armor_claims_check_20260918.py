# -*- coding: utf-8 -*-
"""护甲批 L2 文案的**数字红队**（只读）：把文案里所有"名字+护值N"的说法拉回快照核对。

三类检查：
  A. 「<中文名>护值<数>」——按中文名在快照里找同名的物品，比对护值；
     同名多件时，只要其中一件等于声称值就算过（宽松），否则报 FAIL 并列出实际值。
     找不到同名时，退一步做**子串匹配**：若某个快照名以该串开头/结尾，列出它的实际值供人眼判。
  B. 「护值<数>到<数>」——区间声称，比对**本卡全部成员**（taxonomy 的 items，不只 refs）的
     min/max。区间可以只是主档的子区间，但**必须落在成员区间之内**；越界即 FAIL。
  C. 汇总每卡的成员护值分布，供人眼复核。
"""
import io
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SNAP = ROOT / "tools/worldbook-studio/workspace/full-geo1/authoring/sources/game-items-armor.txt"
L2 = ROOT / "docs/worldbook-migration/projection/authoring-out/_l2_armor_20260918.json"
TAX = ROOT / "tools/_armor_taxonomy_20260918.json"

CN = {"零": 0, "一": 1, "二": 2, "两": 2, "三": 3, "四": 4, "五": 5,
      "六": 6, "七": 7, "八": 8, "九": 9}


def cn2int(s):
    """中文数字 → int，覆盖 0–99（本例最大 57）。"""
    if s in CN:
        return CN[s]
    if "十" in s:
        a, _, b = s.partition("十")
        hi = CN.get(a, 1) if a else 1
        lo = CN.get(b, 0) if b else 0
        return hi * 10 + lo
    return None


# ---- 快照：eid -> (中文名, 护), 兼 中文名 -> [护值] ----
snap = {}
by_name = {}
for ln in SNAP.read_text(encoding="utf-8").splitlines():
    ln = ln.strip()
    if not ln:
        continue
    m = re.match(r"^armor\.([A-Za-z0-9_]+)\s*=>\s*(.+)$", ln)
    if not m:
        continue
    eid, seg = m.group(1), m.group(2)
    parts = seg.split(" | ")
    name = parts[0]
    prot = None
    for p in parts:
        if p.startswith("护 "):
            prot = int(p[2:])
    snap[eid] = (name, prot)
    by_name.setdefault(name, []).append(prot)

cards = json.loads(L2.read_text(encoding="utf-8"))["cards"]

# 卡 -> 全部成员的护值（taxonomy 定稿的成员名单 + 快照的护值；盾的护值只在快照里有）
tax = json.loads(TAX.read_text(encoding="utf-8"))
card_members = {}
for c in tax["cards"]:
    key = c.get("card") or c["key"]
    vs = []
    for it in c["items"]:
        got = snap.get(it["entityId"], (None, None))[1]
        if got is not None:
            vs.append(got)
    card_members.setdefault(key, []).extend(vs)

RE_NAME = re.compile(r"([\u4e00-\u9fff]{2,10})护值([零一二三四五六七八九十]{1,4})")
RE_RANGE = re.compile(r"护值([零一二三四五六七八九十]{1,4})到([零一二三四五六七八九十]{1,4})")

fails = []
loose = []
n_name, n_range = 0, 0

for c in cards:
    slug = c["slug"]
    refs = sorted({r for a in c["asserts"] for r in a["refs"]})
    rvals = [snap[r][1] for r in refs if snap[r][1] is not None]
    mvals = card_members.get(slug, [])
    texts = [c["summary"]] + [a["text"] for a in c["asserts"]] + \
            [e["text"] for a in c["asserts"] for e in a["exprs"]]
    blob = "\n".join(texts)

    # A. 名字 + 护值
    for m in RE_NAME.finditer(blob):
        name, num = m.group(1), cn2int(m.group(2))
        if num is None:
            continue
        n_name += 1
        if name in by_name:
            actual = sorted({v for v in by_name[name] if v is not None})
            if num not in actual:
                fails.append(f"{slug}: 「{name}」文案说护值{num}，快照实际 {actual}")
        else:
            # 退一步：子串匹配（文案用的是半句，不是物品名）
            hits = {k: sorted({v for v in vs if v is not None})
                    for k, vs in by_name.items() if name in k or k in name}
            if hits:
                loose.append(f"{slug}: 「{name}护值{num}」非准确物品名；近似物品 {hits}")
            else:
                loose.append(f"{slug}: 「{name}护值{num}」快照里无此名、也无近似名（应为例举形制，非物品）")

    # B. 区间 护值X到Y —— 必须落在本卡全部成员的 [min,max] 之内
    for m in RE_RANGE.finditer(blob):
        lo, hi = cn2int(m.group(1)), cn2int(m.group(2))
        n_range += 1
        if mvals:
            amin, amax = min(mvals), max(mvals)
            if lo < amin or hi > amax:
                fails.append(
                    f"{slug}: 文案区间护值{lo}到{hi}，越出本卡成员实际 {amin}–{amax}")

    print(f"{slug:<18} refs={len(refs):>2}  成员={len(mvals):>2}  "
          f"refs护值 {min(rvals) if rvals else '-'}–{max(rvals) if rvals else '-'}  "
          f"成员护值 {min(mvals) if mvals else '-'}–{max(mvals) if mvals else '-'}")

print()
print(f"名字型断言 {n_name} 条，区间型断言 {n_range} 条")
print()
if loose:
    print(f"LOOSE {len(loose)}（非 FAIL，需人眼判断是形制描述还是笔误）：")
    for x in loose:
        print("  " + x)
    print()
if fails:
    print(f"FAIL {len(fails)}:")
    for x in fails:
        print("  " + x)
    sys.exit(1)
print("VERDICT PASS")
