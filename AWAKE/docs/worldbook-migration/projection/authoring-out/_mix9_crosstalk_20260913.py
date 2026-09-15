# -*- coding: utf-8 -*-
"""交叉清障：mount-iltan 借引城镇描述文整段 → 改子串（主引权归城镇实体档）。
只改档内 quote + quote_hash（快照与登记 hash 不动，子串仍在原快照内可定位）。
"""
import io, os, re, hashlib, json

BASE = os.path.dirname(os.path.abspath(__file__))
FN = os.path.join(BASE, "mount-iltan.yaml")


def sha(s):
    return hashlib.sha256(s.encode("utf-8")).hexdigest().upper()


def main():
    text = io.open(FN, encoding="utf-8").read()
    # 两条整段引文 → 与山直接相关的子串
    pairs = [
        ("蒂亚尔坐落于斯特吉亚东部边境的伊勒坦山西麓，该地区长期远离其他斯特吉亚公国的权力争斗。",
         "伊勒坦山西麓，该地区长期远离其他斯特吉亚公国的权力争斗。"),
        ("巴尔塔罕，即“斧头堡垒”，由居住在山地的库赛特表亲伊勒坦人建造。",
         "由居住在山地的库赛特表亲伊勒坦人建造。"),
    ]
    n = 0
    for old, new in pairs:
        assert old in text, old[:30]
        h_old = sha(old)
        h_new = sha(new)
        # quote 行替换（quote: <old>），同 hash 行替换
        text = text.replace(f"quote_hash: {h_old}", f"quote_hash: {h_new}")
        text = text.replace(f"quote: {old}", f"quote: {new}")
        n += 1
    # 红线：无锚点
    assert "&id" not in text and "*id" not in text
    io.open(FN, "w", encoding="utf-8", newline="\n").write(text)
    print("replaced", n, "quotes with substrings")


if __name__ == "__main__":
    main()
