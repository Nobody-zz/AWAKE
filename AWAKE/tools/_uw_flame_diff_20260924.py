# -*- coding: utf-8 -*-
"""《小阵营》补料后 · 新旧句对质（2026-09-24，只读）

验三件事：
  ① 新句进：三条新表达都在包里
  ② 旧句退：旧两句（"有些地界，过路是要交钱的" / "有的兄弟会一边替当局办事"）不在包里
  ③ 别档句 0：新句没串到别的档去
"""
import io
import json
import os

REPO = r"D:\AWAKE-Dev\AWAKE"
PKG = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\compiled\geo1-v27-uw-flame")

rt = json.load(io.open(os.path.join(PKG, "runtime.json"), encoding="utf-8"))
docs = json.load(io.open(os.path.join(PKG, "documents.json"), encoding="utf-8"))

# ⚠️ runtime.json 里中文是 `\uXXXX` 转义 ⇒ 直接 count 会全 0（假红）。
#    ⇒ 先 json 解析、再 ensure_ascii=False 重排，得到可搜的明文。
alltxt = json.dumps(rt, ensure_ascii=False)

NEW = {
    "rumor": "没人来掀你的摊子",
    "summary": "被遗忘者的工会",
    "detail": "癣疥之疾",
}
OLD = {
    "旧-rumor": "有些地界，过路是要交钱的",
    "旧-detail": "有的兄弟会一边替当局办事",
}

print("=" * 90)
print("《小阵营》新旧句对质 · 包 geo1-v27-uw-flame")
print("=" * 90)

print("\n① 新句进（三条，应在包内至少各出现一次）")
ok_new = True
for k, v in NEW.items():
    n = alltxt.count(v)
    hit = n > 0
    ok_new &= hit
    print("   [%s] %-20s 出现 %d 次" % ("OK " if hit else "FAIL", k, n))

print("\n② 旧句退（两条，应为 0）")
ok_old = True
for k, v in OLD.items():
    n = alltxt.count(v)
    gone = n == 0
    ok_old &= gone
    print("   [%s] %-24s 出现 %d 次" % ("OK " if gone else "FAIL", k, n))

print("\n③ 别档句 0（新句只应出现在 small-factions 一档里）")
ok_scope = True
entries = rt.get("entries") or []
for k, v in NEW.items():
    owners = [e["id"] for e in entries if v in json.dumps(e, ensure_ascii=False)]
    only = len(owners) == 1 and "small-factions" in owners[0]
    ok_scope &= only
    print("   [%s] %-20s 归属 → %s" % ("OK " if only else "FAIL", k, owners))

print()
print("=" * 90)
print("总结论：", "✅ 全绿（新句进、旧句退、别档 0）"
      if (ok_new and ok_old and ok_scope) else "❌ 有不符合项")
print("=" * 90)
