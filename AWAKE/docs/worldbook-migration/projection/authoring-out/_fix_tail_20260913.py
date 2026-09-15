# -*- coding: utf-8 -*-
"""修4 复验尾巴清理（2026-09-13）：
1) 10 档 en title 补挂（补丁首版漏掉的纯 en 档）
2) lac-lake 的 A 源从 EN7_1（与 tanaesis-lake 撞 hash）换成 EN9_1
"""
import os, re, json, hashlib
import yaml

DIR = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"

EN_TITLE = {
    "charas-town": "Charas", "charas-cortain-secret": "Charas", "charas-reign": "Charas",
    "varcheg-town": "Varcheg", "varcheg-swap": "Varcheg",
    "husn-fulq-town": "Husn Fulq", "husn-fulq-tales": "Husn Fulq",
    "lycaron-town": "Lycaron", "lycaron-mines": "Lycaron", "lycaron-rock-tales": "Lycaron",
}

for slug, en in EN_TITLE.items():
    fp = os.path.join(DIR, slug + ".yaml")
    txt = open(fp, encoding="utf-8").read()
    tm = re.search(r"title:\n  zh-CN: ([^\n]+)\n(status:)", txt)
    assert tm, slug + ": 多行 title 未匹配"
    txt = txt[:tm.end(1)] + f"\n  en: {en}\n" + txt[tm.end(1):]
    yaml.safe_load(txt)
    open(fp, "w", encoding="utf-8", newline="\n").write(txt)
    print(f"[OK] {slug}: en={en}")

# lac-lake: EN7_1 -> EN9_1
CNJ = json.load(open(os.path.join(DIR, "_fix2_cn_texts_20260913.json"), encoding="utf-8"))
fp = os.path.join(DIR, "lac-lake.yaml")
txt = open(fp, encoding="utf-8").read()
old_quote = CNJ["castle_village_EN7_1"]
new_quote = CNJ["castle_village_EN9_1"]
new_hash = hashlib.sha256(new_quote.encode("utf-8")).hexdigest().upper()
n = txt.count(old_quote)
assert n >= 1, "EN7_1 引文未找到"
txt = txt.replace("bannerlord.db#settlements.castle_village_EN7_1.descriptionText",
                  "bannerlord.db#settlements.castle_village_EN9_1.descriptionText")
txt = txt.replace(old_quote, new_quote)
# quote_hash 同步重算（同文本替换法，保持档内其余格式不动）
old_h = hashlib.sha256(old_quote.encode("utf-8")).hexdigest().upper()
txt = txt.replace(old_h, new_hash)
yaml.safe_load(txt)
open(fp, "w", encoding="utf-8", newline="\n").write(txt)
print(f"[OK] lac-lake: A源 EN7_1→EN9_1, hash {new_hash[:12]}…")
