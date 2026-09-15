# -*- coding: utf-8 -*-
"""修 WB-YAML-006：合并批重排时 yaml.safe_dump 产生了锚点/别名。
JSON 往返断开共享引用 + ignore_aliases 重排，清除所有 &id001/*id001。
"""
import io, yaml, re, json

BASE = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out/"
FILES = ["charas-bay.yaml", "der-furs.yaml", "kach-land.yaml", "kach-tales.yaml",
         "lac-lake.yaml", "paravenos.yaml"]

class NoAliasDumper(yaml.SafeDumper):
    def ignore_aliases(self, data):
        return True

for fn in FILES:
    path = BASE + fn
    text = io.open(path, encoding="utf-8").read()
    if "&id0" not in text and "*id0" not in text:
        print(fn, "无锚点，跳过")
        continue
    doc = yaml.safe_load(text)
    doc = json.loads(json.dumps(doc, ensure_ascii=False))  # 断开共享引用
    out = yaml.dump(doc, Dumper=NoAliasDumper, allow_unicode=True,
                    sort_keys=False, width=120)
    assert "&" not in re.sub(r"&[a-z#0-9]+;", "", out).replace("&&", "")  # 粗查无锚点残留
    io.open(path, "w", encoding="utf-8", newline="\n").write(out)
    print(fn, "重排完成")
print("ALIAS-FIX-DONE")
