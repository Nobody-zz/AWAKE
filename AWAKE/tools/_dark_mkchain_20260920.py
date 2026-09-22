# -*- coding: utf-8 -*-
"""由经济批 chain 脚本机械生成暗面批 chain 脚本（避免手抄出错）。

只替换 5 处：OPBASE / OUT_PKG / 三个中间文件名前缀 / 文档字符串里的批名。
"""
import io
import re

SRC = r"D:/AWAKE-Dev/AWAKE/tools/_eco_chain_20260920.py"
DST = r"D:/AWAKE-Dev/AWAKE/tools/_dark_chain_20260920.py"

t = io.open(SRC, encoding="utf-8").read()

subs = [
    ('OPBASE = "eco20260920d"', 'OPBASE = "dark20260920a"'),
    ('OUT_PKG = WS_REL + "/compiled/geo1-v20-goods"', 'OUT_PKG = WS_REL + "/compiled/geo1-v21-dark"'),
    ('_eco_chain_log.txt', '_dark_chain_log.txt'),
    ('_eco_register_batch.json', '_dark_register_batch.json'),
    ('_eco_register_out.txt', '_dark_register_out.txt'),
    ('_eco_select.json', '_dark_select.json'),
    ('_eco_compile.json', '_dark_compile.json'),
    ('"""经济批（09-20）链式编译', '"""暗面批（09-20）链式编译'),
    ('==== 经济批链式编译：磁盘 %d 档 ====', '==== 暗面批链式编译：磁盘 %d 档 ===='),
]
for a, b in subs:
    assert a in t, "没找到待替换串: %s" % a
    t = t.replace(a, b)

# 残留检查：不许再有 eco 字样（除注释里提到模板）
left = [l for l in t.splitlines() if 'eco' in l.lower()]
io.open(DST, "w", encoding="utf-8", newline="\n").write(t)
print("已写出:", DST)
print("残留含 eco 的行数:", len(left))
for l in left:
    print("   ", l.strip()[:120])
