# -*- coding: utf-8 -*-
"""生成「入口词 → 自己」的自命中 spec（真代码 probe 用）。

口径（**照 `PickIdentity` 的同一条道理**，免得量到的是权限而不是检索）：
  每条目从**它自己的 grants** 里挑一个身份（取 min_detail 最高、scope 最宽的那个），
  requested_detail 就取该 grant 的 min_detail ⇒ 目标一定够得着，权限不该成为失败原因。

问句＝该条目的**每一个入口词**（title 中文名 ＋ 全部 keywords，中英各算一类），
外加一条 **title 自问**。逐条独立，输出 sidecar 映射给判定脚本用。

⚠️ 不用 Python 重实现链路（禁平行实现）；这里只**造输入**，判定靠真代码 probe 的 hits。
"""
import collections
import io
import json
import sys

sys.stdout.reconfigure(encoding="utf-8")

PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"
SPEC = "tools/_entry_fit_spec_20260917.json"
SIDE = "tools/_entry_fit_side_20260917.json"

DETAIL_RANK = {"rumor": 0, "summary": 1, "detail": 2, "secret": 3}
SCOPE_RANK = {"local": 0, "regional": 1, "faction": 2, "national": 3, "elite": 4}


def zh(v):
    return (v or {}).get("zh-CN") if isinstance(v, dict) else (v or "")


d = json.load(io.open(PKG, encoding="utf-8"))
entries = d["entries"]
id2idx = {e["id"]: i for i, e in enumerate(entries)}


def pick_identity(e):
    """从该条目自己的 grants 里挑最宽的一个身份；返回 (profile_id, detail)。"""
    best = None
    for ex in e.get("expressions") or []:
        for g in ex.get("grants") or []:
            if g.get("conditions"):
                continue  # 带条件的先跳过（简化：优先无条件 grant）
            key = (DETAIL_RANK.get(g.get("min_detail"), -1), SCOPE_RANK.get(g.get("scope"), -1))
            if best is None or key > best[0]:
                best = (key, g.get("identity_id"), g.get("min_detail"))
    if best is None:
        return None, None
    iid = best[1] or ""
    return "profile." + iid.replace("awake:identity:", ""), best[2]


queries = []
side = {}
n = 0
no_identity = []
for e in entries:
    eid = e["id"].replace("awake:entry:", "")
    prof, detail = pick_identity(e)
    if not prof:
        no_identity.append(eid)
        continue

    def add(kind, text):
        global n
        n += 1
        name = "FIT%05d" % n
        queries.append(dict(name=name, identity=prof, text=text, requested_detail=detail))
        side[name] = dict(entry=eid, kind=kind, text=text, identity=prof, detail=detail)

    add("title", zh(e.get("title")))
    seen = set()
    for k in e.get("keywords") or []:
        if not k or k in seen:
            continue
        seen.add(k)
        add("kw_zh" if not k.isascii() else "kw_ascii", k)

with io.open(SPEC, "w", encoding="utf-8") as fh:
    json.dump({"queries": queries}, fh, ensure_ascii=False, indent=1)
with io.open(SIDE, "w", encoding="utf-8") as fh:
    json.dump({"side": side, "total": len(entries)}, fh, ensure_ascii=False, indent=1)

c = collections.Counter(v["kind"] for v in side.values())
print("条目 %d；无法挑身份 %d %s" % (len(entries), len(no_identity), no_identity[:5]))
print("生成问句 %d 条：%s" % (len(queries), dict(c)))
print("→ %s\n→ %s" % (SPEC, SIDE))
