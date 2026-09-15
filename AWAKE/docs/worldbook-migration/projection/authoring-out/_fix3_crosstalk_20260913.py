# -*- coding: utf-8 -*-
"""修3：A89432B9 交叉双投裁定（2026-09-13）。
裁定：泽翁尼卡城描述文全文归 perassic-sea（季风=海的气象现象）；
nahasa-desert 改引"泽翁娜之风"主题句（子串，hash 不同 → 交叉警消）。
corrections_20260913/nahasa-desert.yaml.md 原值逐字保留。
"""
import os, json, hashlib, re
import yaml

DIR = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
COR = os.path.join(DIR, "corrections_20260913")
os.makedirs(COR, exist_ok=True)

OLD = ("泽翁尼卡位于滨海的伊勒塔尔丘陵之上，这是一片盛产美酒、谷物与骏马的土地。它的名字来源于泽翁娜女士，"
       "少数几位统治过帝国的女性之一——名义上依旧作为她儿子的摄政，而非帝国的女皇。"
       "此地也因泽翁娜之风而闻名，这是一股夏季朝北吹的季风，将纳哈撒沙漠的炎热带过珀拉斯海，来到帝国的南方。"
       "该地其他季节的天气都十分温和，这样的气候也孕育了无数隐喻激情稍纵即逝的诗歌。")
NEW = "此地也因泽翁娜之风而闻名，这是一股夏季朝北吹的季风，将纳哈撒沙漠的炎热带过珀拉斯海，来到帝国的南方。"

fp = os.path.join(DIR, "nahasa-desert.yaml")
txt = open(fp, encoding="utf-8").read()
n = txt.count(OLD)
assert n == 3, f"预期 3 处，实际 {n}"
new_hash = hashlib.sha256(NEW.encode("utf-8")).hexdigest().upper()
old_hash = "A89432B9C37D286A3250A4A1E4F34EFF0D5E21A102AB8F97CD1064F032EE5867"
txt = txt.replace(OLD, NEW)
txt = txt.replace(old_hash, new_hash)
txt = re.sub(r"(?m)^revision: (\d+)$", lambda m: f"revision: {int(m.group(1)) + 1}", txt, count=1)
d = yaml.safe_load(txt)
srcs = [s for s in d["sources"] if s.get("quote_hash") == new_hash]
assert srcs and all(s["quote"] == NEW for s in srcs)
open(fp, "w", encoding="utf-8", newline="\n").write(txt)
print(f"[OK] nahasa-desert: 3 处 quote→子串, 新 hash {new_hash[:12]}…")

# corrections 留痕（原值逐字保留）
md = f"""# corrections_20260913 · nahasa-desert.yaml

> 对账整改批（WORLDBOOK-AUDIT-40-20260913 §2.3）：A89432B9 交叉双投裁定。
> 裁定：泽翁尼卡城描述文（localization.kghCLS9q）全文归 `perassic-sea`（泽翁娜之风=珀拉斯海的气象现象）；
> `nahasa-desert` 改引主题句（子串，以沙漠炎热为主语，贴本档表达"夏季泽翁娜之风从沙漠过海"）。
> 依据：章法 §2.3 多域不双投（同变体不双投）；quote_hash 随 quote 子串重算。
> 原 quote_hash: {old_hash} → 新 quote_hash: {new_hash}

## quote（原值，逐字）
```
{OLD}
```

## quote（新值，子串）
```
{NEW}
```

三处引用（顶层 sources / 断言 sources / 表达 sources）同步替换；perassic-sea 侧不动。
"""
open(os.path.join(COR, "nahasa-desert.yaml.md"), "w", encoding="utf-8", newline="\n").write(md)
print("[OK] corrections_20260913/nahasa-desert.yaml.md 已落")
