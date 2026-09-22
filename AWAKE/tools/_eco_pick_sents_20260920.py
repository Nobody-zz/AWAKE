# -*- coding: utf-8 -*-
"""精筛：对每件货，抽出含货名的句子，只保留同时含产销/价钱动词的。"""
import io, json, re, os

CAND = r"D:\AWAKE-Dev\AWAKE\tools\_eco_chronicle_cands_20260920.json"
OUT  = r"D:\AWAKE-Dev\AWAKE\tools\_eco_chronicle_pick_20260920.json"
d = json.load(io.open(CAND, encoding="utf-8"))

VERB = ["产", "出", "卖", "买", "价", "第纳尔", "收", "运", "货", "市", "贵", "贱", "值", "挣", "钱", "换", "贩"]
SPLIT = re.compile(r"[。！？；\n]")

out = {}
for good, cands in d.items():
    keep = []
    seen = set()
    for c in cands:
        for s in SPLIT.split(c["content"]):
            s = s.strip()
            if not s or good[:2] not in s:
                continue
            if not any(v in s for v in VERB):
                continue
            if len(s) < 8 or len(s) > 160:
                continue
            key = s[:40]
            if key in seen:
                continue
            seen.add(key)
            keep.append({"file": c["file"], "vi": c["vi"], "sent": s})
    out[good] = keep

for good in out:
    print("## %s（%d 条）" % (good, len(out[good])))
    for k in out[good][:6]:
        print("   [%s V%d] %s" % (k["file"].replace("rule_", "").replace(".json", ""), k["vi"], k["sent"]))
    print()

json.dump(out, io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("已落盘:", OUT)
