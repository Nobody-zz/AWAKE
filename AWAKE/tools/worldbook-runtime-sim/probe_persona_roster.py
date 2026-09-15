#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""丙-a 前置探针：清点 persona definition 名册（repo 侧 / 游戏侧）。

用法：
    python probe_persona_roster.py

只读。输出每个目录的 status / characterId 分布，并逐条列出非 approved 的 id。
"""
import json
import os
from collections import Counter

ROOTS = [
    ("repo", r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\persona_definitions"),
    ("game", r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE\ModuleData\Worldbook\persona_definitions"),
]


def scan(def_dir):
    rows = []
    if not os.path.isdir(def_dir):
        return rows
    for name in sorted(os.listdir(def_dir)):
        if not name.lower().endswith(".json"):
            continue
        path = os.path.join(def_dir, name)
        try:
            with open(path, "r", encoding="utf-8") as handle:
                obj = json.load(handle)
        except Exception as exc:  # noqa: BLE001
            rows.append({"file": name, "error": str(exc)})
            continue
        items = obj if isinstance(obj, list) else [obj]
        for index, item in enumerate(items):
            if not isinstance(item, dict):
                continue
            rows.append({
                "file": name if len(items) == 1 else "%s[%d]" % (name, index),
                "id": item.get("id", ""),
                "characterId": item.get("characterId", ""),
                "identityId": item.get("identityId", ""),
                "role": item.get("role", ""),
                "scope": item.get("scope", ""),
                "status": item.get("status", "<missing>"),
                "priority": item.get("priority", 0),
                "tags": len(item.get("tags", []) or []),
            })
    return rows


def main():
    for label, root in ROOTS:
        def_dir = os.path.join(root, "definitions")
        registry = os.path.join(root, "tag_registry.json")
        rows = scan(def_dir)
        print("=" * 72)
        print("[%s] %s" % (label, def_dir))
        print("  files=%d entries=%d registry_exists=%s" % (
            len(set(r["file"].split("[")[0] for r in rows)), len(rows), os.path.isfile(registry)))
        if not rows:
            print("  (empty)")
            continue
        status = Counter(r.get("status", "<missing>") for r in rows)
        print("  status: " + ", ".join("%s=%d" % (k, v) for k, v in status.most_common()))
        by_char = [r for r in rows if r.get("characterId")]
        print("  has characterId: %d / %d" % (len(by_char), len(rows)))
        dup_char = [c for c, n in Counter(r.get("characterId") for r in by_char).items() if n > 1]
        if dup_char:
            print("  DUPLICATE characterId: " + ", ".join(sorted(dup_char)))
        print("  --- non-approved ---")
        for row in rows:
            if row.get("status") != "approved":
                print("    %-28s id=%-34s char=%-12s status=%s" % (
                    row.get("file", ""), row.get("id", ""), row.get("characterId", ""), row.get("status", "")))
        print("  --- approved ---")
        for row in rows:
            if row.get("status") == "approved":
                print("    %-28s id=%-34s char=%-12s scope=%s" % (
                    row.get("file", ""), row.get("id", ""), row.get("characterId", ""), row.get("scope", "")))


if __name__ == "__main__":
    main()
