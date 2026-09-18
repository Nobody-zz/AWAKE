# -*- coding: utf-8 -*-
"""探测：taxonomy json 能否 load/dump 往返无损（决定用整体重序列化还是文本精准替换）。只读。"""
import io
import json

TAX = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-studio-plan\knowledge-taxonomy.v1.json"

raw = io.open(TAX, encoding="utf-8").read()
obj = json.loads(raw)
redump = json.dumps(obj, indent=2, ensure_ascii=False) + "\n"

print("roundtrip identical:", redump == raw)
print("len raw=%d redump=%d" % (len(raw), len(redump)))
if redump != raw:
    for i, (a, b) in enumerate(zip(raw, redump)):
        if a != b:
            print("first diff at char", i)
            print("  raw   :", repr(raw[max(0, i - 50):i + 60]))
            print("  redump:", repr(redump[max(0, i - 50):i + 60]))
            break
    else:
        print("prefix identical; tail differs")
        print("  raw tail   :", repr(raw[len(redump) - 20:]))
        print("  redump tail:", repr(redump[len(raw) - 20:]))
# 关键结构统计
doms = obj["domains"]
print("domains:", [d["id"] for d in doms])
for d in doms:
    print("  %-10s %2d subs: %s" % (d["id"], len(d["subdomains"]), ",".join(s["id"] for s in d["subdomains"])))
