# -*- coding: utf-8 -*-
"""sethys-river 引文子串化整改：河档借引三个村庄描述文首句，与村庄批 2 撞 quote_hash。
按 mount-iltan 先例：主引权归村庄档（描述文主消费方），河档改小子串引用。
双写同步（authoring-out + workspace authoring）。"""
import hashlib, io, os, re

P1 = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out/sethys-river.yaml"
P2 = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring/sethys-river.yaml"

# 整句 -> 子串（保留河向信息，去村名开头）
REPL = [
    ("quote: 摩雷尼亚俯瞰着塞堤斯河，位于通往吕卡里亚谷的低矮入口处。",
     "quote: 塞堤斯河，位于通往吕卡里亚谷的低矮入口处"),
    ("quote: 卡诺普西斯依着密泽亚德高原上的塞堤斯河源头而建。",
     "quote: 密泽亚德高原上的塞堤斯河源头"),
    ("quote: 阿特费尼亚村靠近塞堤斯河口，再向下游河流随即分为三支，形成三河河谷。",
     "quote: 再向下游河流随即分为三支，形成三河河谷"),
]


def sha(s):
    return hashlib.sha256(s.encode("utf-8")).hexdigest().upper()


for path in (P1, P2):
    t = io.open(path, encoding="utf-8").read()
    for old_line, new_line in REPL:
        old_q = old_line.split("quote: ", 1)[1]
        new_q = new_line.split("quote: ", 1)[1]
        n = t.count(old_line)
        assert n >= 1, (path, old_q)
        t = t.replace(old_line, new_line)
        # 同步该 quote 的 quote_hash（old 之后的第一个 hash 行）
    # 逐对重算 hash：重新扫文件，quote 行后跟的 quote_hash 行按 quote 内容重算
    lines = t.split("\n")
    for i, ln in enumerate(lines):
        m = re.match(r"^(\s*)quote: (.+)$", ln)
        if m:
            for j in range(i + 1, min(i + 3, len(lines))):
                mh = re.match(r"^(\s*)quote_hash: ([0-9A-Fa-f]{64})$", lines[j])
                if mh:
                    nh = sha(m.group(2))
                    if mh.group(2) != nh:
                        lines[j] = f"{mh.group(1)}quote_hash: {nh}"
                    break
    t = "\n".join(lines)
    io.open(path, "w", encoding="utf-8", newline="\n").write(t)
    print("patched:", path)

# 验证：新子串必须是官方描述文的连续子串（借引合法性）
import sqlite3
con = sqlite3.connect(r"file:C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db?mode=ro", uri=True)
for sid, frag in [
    ("castle_village_ES5_1", "塞堤斯河，位于通往吕卡里亚谷的低矮入口处"),
    ("castle_village_ES8_1", "密泽亚德高原上的塞堤斯河源头"),
    ("castle_village_ES5_2", "再向下游河流随即分为三支，形成三河河谷"),
]:
    r = con.execute(
        "SELECT descriptionText FROM bannerlord_settlements WHERE settlementId=?", (sid,)).fetchone()
    tok = r[0].split("{=")[1].split("}")[0]
    full = con.execute(
        "SELECT text FROM localization_entries WHERE stringId=? AND language='CNs'", (tok,)).fetchone()[0]
    full = re.sub(r"\s+", "", full)
    assert frag in full, (sid, frag)
    print("substring OK:", sid)
print("DONE")
