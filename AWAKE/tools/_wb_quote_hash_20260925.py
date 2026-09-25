# -*- coding: utf-8 -*-
"""反推 quote_hash 口径。
已知（military-empire-system.yaml）：
  quote: 皇帝阿雷尼科斯在潘德拉克战役的惨败之后废除了最后一支帝国常备军，...
  quote_hash: C844C912B02C36044764F52975B6861CB5EF007E92DAB1EDEC7E31BCACC0F326
"""
import hashlib

Q = ("皇帝阿雷尼科斯在潘德拉克战役的惨败之后废除了最后一支帝国常备军，转而依赖开销更少、"
     "更适合控制领土的执政官私兵，被弃者军团在这样的局势下成立。而这支军团则是由厌恶这种变化的人所组成："
     "他们偏爱昔日的军团，及其规矩、历史与军营生活，他们指责新制度正在摧毁帝国。但无饷不成军，"
     "所以他们会同时与帝国领主和外国领主签订契约。")
WANT = "C844C912B02C36044764F52975B6861CB5EF007E92DAB1EDEC7E31BCACC0F326"

print("目标 quote_hash:", WANT)
print("引文长度(字符):", len(Q))

def h(b):
    return hashlib.sha256(b).hexdigest().upper()

cands = {
    "sha256(utf8)": h(Q.encode("utf-8")),
    "sha256(utf8 strip)": h(Q.strip().encode("utf-8")),
    "sha256(utf8 + \\n)": h((Q + "\n").encode("utf-8")),
    "sha256(utf16-le)": h(Q.encode("utf-16-le")),
    "sha256(utf16)": h(Q.encode("utf-16")),
    "sha256(utf8, 中文标点->英文)": h(Q.replace("，", ",").replace("。", ".").replace("：", ":").encode("utf-8")),
}
for k, v in cands.items():
    print("  %-28s %s  %s" % (k, v, "  <<< 命中" if v == WANT else ""))
