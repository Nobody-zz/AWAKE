# -*- coding: utf-8 -*-
"""检查：官方 Prefab 的 <ItemTemplate> 内部会不会给控件写 Id=？

背景：AWAKE 的 NpcDialogue 在 ItemTemplate 里放了 Id="ChatRowBody"，
      给 DimensionSyncWidget 的 WidgetToCopyHeightFrom 当目标。
      官方那几处 sync 目标（GameMenu.ItemListPanel / HintTooltip.HintInnerPanel /
      SingleQueryPopup.SingleQueryContentList）都在模板**外面**。
      ⇒ 必须查清模板内写 Id 是否有先例，否则是自创写法。

用法：python _probe_template_id_20260915.py
"""
import os
import sys
import xml.etree.ElementTree as ET

MODULES = "D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules"
NATIVE = ("Native", "SandBox", "SandBoxCore", "StoryMode", "Multiplayer")


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    files = 0
    tmpl_total = 0
    tmpl_with_id = 0
    samples = []
    id_names = {}

    for m in NATIVE:
        d = os.path.join(MODULES, m, "GUI", "Prefabs")
        if not os.path.isdir(d):
            continue
        for dirpath, _dn, fns in os.walk(d):
            for fn in fns:
                if not fn.lower().endswith(".xml"):
                    continue
                p = os.path.join(dirpath, fn)
                try:
                    root = ET.parse(p).getroot()
                except Exception:
                    continue
                files += 1
                for el in root.iter():
                    if el.tag != "ItemTemplate":
                        continue
                    tmpl_total += 1
                    ids = [x.get("Id") for x in el.iter() if x.get("Id")]
                    if ids:
                        tmpl_with_id += 1
                        for i in ids:
                            id_names[i] = id_names.get(i, 0) + 1
                        if len(samples) < 15:
                            samples.append("%s  %s" % (fn, ", ".join(sorted(set(ids))[:4])))

    print("扫描文件数            :", files)
    print("<ItemTemplate> 总数   :", tmpl_total)
    print("其中内部写了 Id 的    :", tmpl_with_id)
    print()
    print("--- 样例（模板内 Id）---")
    for s in samples:
        print("  ", s)
    print()
    print("--- 出现最多的模板内 Id top 15 ---")
    for k, v in sorted(id_names.items(), key=lambda kv: -kv[1])[:15]:
        print("  %-40s %d" % (k, v))


if __name__ == "__main__":
    main()
