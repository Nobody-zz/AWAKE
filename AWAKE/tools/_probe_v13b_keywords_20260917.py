# -*- coding: utf-8 -*-
"""收窄概念词条别名之前，先量三件事（只读，不改任何东西）：

1. v13 包里三条概念词条的 keywords 到底是什么（别名→keywords 的实际映射）。
2. 六个口语同义词（村子/村落/城砦/堡垒/镇子/城市）在**全库 keywords** 里的 document frequency。
   df=1 说明只出现在概念词条上 ⇒ 它就是"堵死兜底"的那把钥匙。
3. 把概念词条的 keywords 收窄成"只留主词"之后，重建关键词索引（同一套剔除规则），
   看「有大瀑布的村子是哪个？」这句在主路**还有没有命中**。
   期望：0 命中 ⇒ 主路空手 ⇒ 兜底通道重新跑起来（v12 就是这样拿到 4 条、含正确答案）。

运行：python -u tools/_probe_v13b_keywords_20260917.py
"""
import io
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = r"D:/AWAKE-Dev/AWAKE"
PKG = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v13-settlement-types/runtime.json")
MAXDF = 40
NEW = ["awake:entry:geography.settlement-types-village",
       "awake:entry:geography.settlement-types-castle",
       "awake:entry:geography.settlement-types-town"]
# 收窄后三条概念词条应当只保留的主词（中文）＋英文
NARROW = {
    NEW[0]: ["村庄", "Village", "Villages"],
    NEW[1]: ["城堡", "Castle", "Castles"],
    NEW[2]: ["城镇", "Town", "Towns"],
}
WORDS = ["村庄", "村子", "村落", "城堡", "城砦", "堡垒", "城镇", "镇子", "城市"]
QUERY = "有大瀑布的村子是哪个？"


def excluded(kw, df):
    if df > MAXDF:
        return True
    if kw.lower().startswith("doc."):
        return True
    has_us = False
    for ch in kw:
        if ch == "_":
            has_us = True
            continue
        if ch > "\x7e" or ch < "\x20":
            return False
    return has_us


def build(entries, narrow=False):
    kws = {}
    for e in entries:
        s = []
        for k in (e.get("keywords") or []):
            if narrow and e["id"] in NARROW:
                if k not in NARROW[e["id"]]:
                    continue
            if k and k.strip() and k.strip() not in s:
                s.append(k.strip())
        kws[e["id"]] = s
    df = {}
    for i, s in kws.items():
        for k in s:
            df[k] = df.get(k, 0) + 1
    idx = {}
    for i, s in kws.items():
        for k in s:
            if excluded(k, df[k]):
                continue
            idx.setdefault(k, []).append(i)
    return kws, df, idx


def literal_hits(idx, text):
    ids = set()
    low = text.lower()
    for k, v in idx.items():
        kl = k.lower()
        if low.find(kl) < 0 and kl.find(low) < 0:
            continue
        ids.update(v)
    return ids


def main():
    data = json.loads(io.open(PKG, encoding="utf-8").read())
    entries = data["entries"]
    print("包 = %s" % os.path.relpath(PKG, ROOT))
    print("条目 %d" % len(entries))
    print()

    print("── 1. 三条概念词条的 keywords（别名→keywords 的实际落点） ──")
    kws0, df0, idx0 = build(entries, narrow=False)
    for nid in NEW:
        e = next((x for x in entries if x["id"] == nid), None)
        if e is None:
            print("  %s 不在包里！" % nid)
            continue
        ks = kws0[nid]
        print("  %s" % nid)
        print("    title=%r" % e.get("title"))
        print("    keywords(%d)=%s" % (len(ks), json.dumps(ks, ensure_ascii=False)))
    print()

    print("── 2. 九个类别/口语词在全库 keywords 里的覆盖度 ──")
    for w in WORDS:
        exact = sum(1 for i, s in kws0.items() for k in s if k == w)
        sub = sum(1 for i, s in kws0.items() for k in s if w in k and k != w)
        inidx = "进索引" if (exact and not excluded(w, df0.get(w, 0))) else (
            "被剔(%s)" % ("df>%d" % MAXDF if df0.get(w, 0) > MAXDF else "—"))
        print("  %-4s 整词 %3d 条 · 作为别人的一部分 %3d 条 ⇒ %s" % (w, exact, sub, inidx))
    print()

    print("── 3. 收窄前后，那句回归查询在主路的命中 ──")
    print("  查询：%s" % QUERY)
    for tag, nar in (("收窄前(v13 现状)", False), ("收窄后(只留主词)", True)):
        kws, df, idx = build(entries, narrow=nar)
        hits = literal_hits(idx, QUERY)
        names = sorted(h.replace("awake:entry:", "") for h in hits)
        print("  %-18s 主路命中 %d 条 %s" % (tag, len(names), names[:6]))
        print("  %-18s ⇒ %s" % ("", "主路空手，兜底会跑" if not names else "主路有命中 ⇒ 兜底不跑"))
    print()

    print("── 4. 收窄是否动到别人 ──")
    kws_a, _d, idx_a = build(entries, narrow=False)
    kws_b, _d, idx_b = build(entries, narrow=True)
    only_a = {k for k in idx_a if k not in idx_b}
    only_b = {k for k in idx_b if k not in idx_a}
    print("  只在“收窄前”索引里的键：%s" % json.dumps(sorted(only_a), ensure_ascii=False))
    print("  只在“收窄后”索引里的键：%s" % json.dumps(sorted(only_b), ensure_ascii=False))
    diff_ids = [i for i in kws_a if kws_a[i] != kws_b[i]]
    print("  keywords 被改动的条目：%s" % json.dumps(
        [x.replace("awake:entry:", "") for x in diff_ids], ensure_ascii=False))


if __name__ == "__main__":
    main()
