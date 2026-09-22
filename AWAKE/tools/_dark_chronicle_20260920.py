# -*- coding: utf-8 -*-
"""暗面批取数第 4 步：把编年史里暗面相关的规则变体全文打出来。"""
import glob
import io
import json
import os

RULES = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules"

WANT = ['农奴', '劫匪', '沙漠强盗', '湖鼠帮', '绿林强盗', '阿塞莱阶级与奴隶制', '山贼', '海寇', '逃兵']

for fn in sorted(os.listdir(RULES)):
    if not any(w in fn for w in WANT):
        continue
    p = os.path.join(RULES, fn)
    try:
        d = json.load(io.open(p, encoding='utf-8-sig'))
    except Exception as e:
        print('!! 读不了', fn, e)
        continue
    vs = d.get('Variants') or []
    print('==== %s  （%d 变体）' % (fn, len(vs)))
    for i, v in enumerate(vs):
        c = (v.get('Content') or '').strip()
        if not c:
            continue
        print('  [%d] %s' % (i, c[:420]))
    print()
