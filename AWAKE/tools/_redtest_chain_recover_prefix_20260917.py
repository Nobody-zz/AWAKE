# -*- coding: utf-8 -*-
"""把上一版报告里的"改动前"逐条读数捞回来（2026-09-17）

为什么要捞：补完五条缺口后，我把两档结果**原地覆盖**了，而"改之前"那一档才是对照。
它没丢 —— 上一版报告已入库（commit `66649c5` 的 `REDTEST-CHAIN-20260917.json`），
`rowsOff`/`rowsOn` 里就是逐条判据。

本脚本把它还原成与探针输出**同形**的两份 json，好让判定脚本用同一套读法读三档：
    改动前(不挂) / 改动前(挂) / 改动后(不挂) / 改动后(挂)

⚠️ 还原是**有损**的：入库那份为了瘦身剔掉了 `text`（返回正文），所以还原出来的行没有正文，
   因此**还原档不能进"档位闸"**（那根闸要看正文）。归档里已注明。
"""
import io
import json
import subprocess

REPO = "D:/AWAKE-Dev"
SRC = "AWAKE/docs/worldbook-migration/REDTEST-CHAIN-20260917.json"
REV = "66649c5"
OUT = "tools/_redtest_chain_prefix_%s_20260917.json"


def load_from_git():
    raw = subprocess.run(
        ["git", "show", "%s:%s" % (REV, SRC)],
        cwd=REPO, capture_output=True, check=True,
    ).stdout
    return json.loads(raw.decode("utf-8"))


def convert(rows, name_of):
    out = []
    for r in rows:
        out.append({
            "name": "%s-%s" % (r["no"], r.get("group", "")),
            "identity": r.get("identity", ""),
            "role": "",
            "scope": r.get("scope"),
            "detail": r.get("detail"),
            "state": r.get("state"),
            "match_mode": None,
            "hits": r.get("hits") or [],
            "player_text": r.get("playerText") or r.get("text") or "",
            "text": "",
            "_recovered_from": REV,
            "_note": "入库版剔掉了返回正文 ⇒ 这一档**不进档位闸**",
            "_name_of": name_of,
        })
    return out


def main():
    doc = load_from_git()
    for key, tag in (("rowsOff", "off"), ("rowsOn", "on")):
        rows = convert(doc.get(key) or [], key)
        path = OUT % tag
        with io.open(path, "w", encoding="utf-8") as fh:
            json.dump(rows, fh, ensure_ascii=False, indent=2)
            fh.write("\n")
        print("还原 %s ← %s 的 %s：%d 条 → %s" % (tag, REV, key, len(rows), path))
    print()
    s = doc["summaryOff"], doc["summaryOn"]
    print("核对：入库版记的是 不挂 RED %d/%d、挂上 RED %d/%d"
          % (s[0]["red"], s[0]["graded"], s[1]["red"], s[1]["graded"]))


main()
