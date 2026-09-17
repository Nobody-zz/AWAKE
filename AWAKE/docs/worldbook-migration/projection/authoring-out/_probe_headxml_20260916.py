# -*- coding: utf-8 -*-
"""探针：看清 head_armors.xml 里一个 Item 元素的完整结构（属性挂在哪一层）。"""
import os
import xml.etree.ElementTree as ET

GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBoxCore/ModuleData/items"

root = ET.parse(os.path.join(GAME, "head_armors.xml")).getroot()
print("root tag:", root.tag, "attrib:", dict(root.attrib))
print("children tags:", sorted(set(c.tag for c in root)))

n = 0
for it in root.iter("Item"):
    if it.get("id") in ("sturgian_helmet_closed", "arming_cap", "aserai_lord_helmet_a"):
        print("\n==== Item id=%s ====" % it.get("id"))
        print("Item attribs:", it.attrib)
        for ch in it:
            print("   child <%s> attrib=%s text=%r" % (ch.tag, ch.attrib, (ch.text or "").strip()[:40]))
        n += 1
    if n >= 3:
        break
