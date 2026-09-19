# -*- coding: utf-8 -*-
"""给我自己的四个任务对象出实测读数：① 知识库格式 ② 知识条目内容。

只读，不改任何东西。输出直接打到 stdout。
"""
import json
import pathlib
import re
import collections

ROOT = pathlib.Path(r"D:\AWAKE-Dev")
PKG = ROOT / "AWAKE/ModuleData/Worldbook/packages/calradia/runtime.json"
IDX = ROOT / "AWAKE/ModuleData/Worldbook/packages/calradia/index.json"
REG = ROOT / "AWAKE/ModuleData/Worldbook/manifest.json"


def walk(o):
    if isinstance(o, dict):
        if isinstance(o.get("id"), str) and o["id"].startswith("awake:entry:"):
            yield o
        for v in o.values():
            yield from walk(v)
    elif isinstance(o, list):
        for v in o:
            yield from walk(v)


def sec(t):
    print("\n" + "=" * 68)
    print(t)
    print("=" * 68)


# ---------------------------------------------------------------- ① 格式
sec("① 知识库格式（schema 清单与分布）")

schema_hits = collections.Counter()
where = collections.defaultdict(set)
for p in list((ROOT / "AWAKE/ModuleData/Worldbook").rglob("*.json")) + \
         list((ROOT / "AWAKE/docs/worldbook-migration/projection/authoring-out").rglob("*.yaml")) + \
         list((ROOT / "AWAKE/tools/worldbook-studio/contracts").rglob("*.json")):
    if not p.is_file():
        continue
    try:
        txt = p.read_text(encoding="utf-8", errors="replace")
    except Exception:
        continue
    for m in re.finditer(r'awake\.worldbook\.[a-z0-9.\-]+', txt):
        s = m.group(0)
        schema_hits[s] += 1
        where[s].add(p.name)

for s, n in schema_hits.most_common():
    print(f"  {s:38s} 出现 {n:5d} 次   样例文件 {sorted(where[s])[:2]}")

print("\n  --- 现行包自身的 schemaVersion ---")
for label, path in (("registry", REG), ("package manifest", PKG.parent / "manifest.json"),
                    ("runtime", PKG), ("index", IDX)):
    try:
        d = json.loads(path.read_text(encoding="utf-8"))
        print(f"  {label:18s} {d.get('schemaVersion')}")
    except Exception as e:
        print(f"  {label:18s} 读不出：{e}")

# ---------------------------------------------------------------- ② 内容
sec("② 知识条目内容（现行包 482 条的实况）")

d = json.loads(PKG.read_text(encoding="utf-8"))
entries = list(walk(d))
print(f"  条目数 {len(entries)}")

dom = collections.Counter()
sub = collections.Counter()
exp0 = kw0 = no_ref = no_summary = 0
exp_hist = collections.Counter()
lv_have = collections.Counter()
for e in entries:
    ext = e.get("extensions") or {}
    dom[e.get("domain")] += 1
    sub[f"{e.get('domain')}.{ext.get('subdomain')}"] += 1
    ex = e.get("expressions") or []
    exp_hist[len(ex)] += 1
    if len(ex) == 0:
        exp0 += 1
    if not (e.get("keywords") or []):
        kw0 += 1
    refs = ext.get("entityRefs") or []
    if not refs:
        no_ref += 1
    if not (e.get("summary") or {}).get("zh-CN"):
        no_summary += 1
    levels = {x.get("detail") for x in ex}
    for lv in ("rumor", "detail", "secret"):
        if lv in levels:
            lv_have[lv] += 1
    if not levels:
        lv_have["(无)"] += 1

print("\n  --- 按 domain ---")
for k, v in dom.most_common():
    print(f"    {k:14s} {v:4d}")
print("\n  --- subdomain 前 12 ---")
for k, v in sub.most_common(12):
    print(f"    {k:34s} {v:4d}")
print("\n  --- 空缺检查 ---")
print(f"    无 expressions      {exp0}")
print(f"    无 keywords         {kw0}")
print(f"    无 entityRefs(锚点)  {no_ref}   / 共 {len(entries)}  ⇒ 有锚点 {len(entries)-no_ref}"
      f"（{100.0*(len(entries)-no_ref)/max(1,len(entries)):.0f}%）")
print(f"    无中文 summary      {no_summary}")
print("\n  --- 三层（rumor/detail/secret）覆盖 ---")
for k in ("rumor", "detail", "secret", "(无)"):
    print(f"    含 {k:8s} 层的词条  {lv_have[k]}")
print("\n  --- expressions 条数分布（前 8 档）---")
for k, v in sorted(exp_hist.items())[:8]:
    print(f"    {k} 条表达  →  {v} 个词条")
