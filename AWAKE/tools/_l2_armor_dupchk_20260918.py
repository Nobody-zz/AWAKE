# -*- coding: utf-8 -*-
"""护甲批收尾两项自查（只读）：
① 双写一致性：`projection/authoring-out/weapons-armor-*.yaml` ↔ `workspace/full-geo1/authoring/`，
   必须 21/21 逐字节一致（生成器是双写的，但"双写"这件事本身没有断言，所以单独验一次）。
② 跨档引文查重：本批 21 张卡的正文（断言文字 + 分层表达）里，**不该有整句在别的卡里重复出现**。
   同批同一句话出现在两张卡上，说明是套模板抄的，读起来会像"同一个印章"
   （头盔批与关系卡都踩过这个形态）。判据：按句号/分号切句，句长 ≥12 字的句子不得跨卡重复。

用法: python _l2_armor_dupchk_20260918.py
"""
import io, json, os, re, sys, collections, hashlib

ROOT = r"D:/AWAKE-Dev/AWAKE"
OUT_YAML = os.path.join(ROOT, "docs/worldbook-migration/projection/authoring-out")
WS_YAML = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/authoring")
L2 = os.path.join(OUT_YAML, "_l2_armor_20260918.json")

fail = 0

# ---- ① 双写一致性 ----
names = sorted(f for f in os.listdir(OUT_YAML)
               if f.startswith("weapons-armor-") and f.endswith(".yaml"))
print("① 双写一致性：authoring-out 侧 %d 档" % len(names))
if len(names) != 21:
    print("   FAIL 档数不是 21"); fail += 1
for fn in names:
    a = os.path.join(OUT_YAML, fn)
    b = os.path.join(WS_YAML, fn)
    ha = hashlib.sha256(open(a, "rb").read()).hexdigest()
    hb = hashlib.sha256(open(b, "rb").read()).hexdigest() if os.path.exists(b) else "(缺)"
    if ha != hb:
        print("   FAIL %s 不一致  %s vs %s" % (fn, ha[:12], hb[:12])); fail += 1
print("   21/21 逐字节一致 =%s" % (fail == 0))
print("   注：workspace 侧护甲档共 %d 个"
      % len([f for f in os.listdir(WS_YAML) if f.startswith("weapons-armor-")]))

# ---- ② 跨档引文查重 ----
cards = json.load(io.open(L2, encoding="utf-8"))["cards"]
sent = collections.defaultdict(list)   # 句子 -> [(卡, 出处)]
for c in cards:
    for i, a in enumerate(c["asserts"], 1):
        for piece in re.split(r"[。；]", a["text"]):
            if len(piece.strip()) >= 12:
                sent[piece.strip()].append((c["slug"], "assert%d" % i))
        for e in a["exprs"]:
            for piece in re.split(r"[。；]", e["text"]):
                if len(piece.strip()) >= 12:
                    sent[piece.strip()].append((c["slug"], "expr:" + e["tag"]))
dup = {k: v for k, v in sent.items() if len({x[0] for x in v}) > 1}
print("\n② 跨档引文查重：句子样本 %d 条，跨卡重复 %d 条" % (len(sent), len(dup)))
for k, v in sorted(dup.items()):
    print("   FAIL 「%s…」" % k[:34])
    for slug, where in v:
        print("        %s @ %s" % (slug, where))
    fail += 1
if not dup:
    print("   0 条跨卡重复句 ✔（本批 21 张卡各自独立表述）")

print("\nVERDICT %s" % ("PASS" if fail == 0 else "FAIL(%d)" % fail))
sys.exit(0 if fail == 0 else 1)
