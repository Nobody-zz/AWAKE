# -*- coding: utf-8 -*-
"""查「这一带有好马吗？」这条题的靶子是不是也是任意的（2026-09-17）。

甲方口径：**「不让进」本来就不应该成提示词；「好马」被多个词条占据也是正常现象。**
⇒ 于是要核：全库到底有多少条目在讲"这地方出好马"，拉迈萨是不是唯一答案。
若有多条同质条目 ⇒ 这条题和那条废题一样"没有唯一答案"，不该拿它给检索定罪。
"""
import json
import sys

sys.stdout.reconfigure(encoding="utf-8")

PKG = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\packages\calradia\runtime.json"

HORSE_WORDS = ["好马", "骏马", "战马", "马匹", "养马", "放养", "马场", "马价", "马驹", "马市"]


def zh(v):
    if isinstance(v, dict):
        return v.get("zh-CN") or v.get("en") or str(v)
    return str(v)


def blob(e):
    parts = [zh(e.get("title")), zh(e.get("summary"))]
    parts += [zh(k) for k in (e.get("keywords") or [])]
    parts += [zh(x.get("text")) for x in (e.get("expressions") or [])]
    return " ".join(parts)


data = json.load(open(PKG, encoding="utf-8"))
items = list(data["entries"].items()) if isinstance(data["entries"], dict) \
    else [(e.get("id"), e) for e in data["entries"]]

# 只要地理条目
rows = []
for eid, e in items:
    if ":geography." not in eid:
        continue
    body = blob(e)
    hits = [w for w in HORSE_WORDS if w in body]
    if hits:
        rows.append((eid, zh(e.get("title")), hits, zh(e.get("summary"))))

print("全库地理条目里，正文提到马相关词的：%d 条" % len(rows))
print()
for eid, title, hits, summary in rows:
    print("-" * 78)
    print("  %s   %s" % (title, eid))
    print("  命中词：%s" % "、".join(hits))
    print("  摘要：%s" % summary[:200])
print()
print("=" * 78)
print("只看『养／放养／出产』语义的（真正在讲『这地方出好马』的）：")
for eid, title, hits, summary in rows:
    if any(w in hits for w in ["养马", "放养", "马场", "马驹"]):
        print("  %-12s %s" % (title, eid))
