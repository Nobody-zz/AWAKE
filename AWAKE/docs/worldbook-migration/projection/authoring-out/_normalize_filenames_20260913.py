# -*- coding: utf-8 -*-
"""全库文件名规范化（第 2 步）：统一为「次级分类-词条名」式（item-/town-/village- 同风格）。

覆盖 33 档（geography 自然地理 12 / culture 6 / economy 2 / politics 7 / war 6）。
规则：文件名与档内 id 尾同步改为 <次级分类>-<名>；档内 assertion./expr. 前缀同步；
字段顺序与风格对齐主流（sources 先于 authority、block 风格）。
保留：subdomain、title、entity_ids、全部 sources/assertions 内容。
双写 authoring-out ＋ workspace/authoring；旧文件移除。无跨档 id 引用（已核）。
"""
import io, os, re, yaml, json

OUT = os.path.dirname(os.path.abspath(__file__))
WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"

TOP_ORDER = ["schema_version", "revision", "id", "title", "status", "domain", "subdomain",
             "universe", "era", "content_tier", "aliases", "entity_ids", "summary",
             "registry_bindings", "sources", "authority", "assertions"]

# (old_file, new_file, old_id, new_id)
JOBS = [
    # --- geography 自然地理 ---
    ("charas-bay.yaml", "bay-charas.yaml", "doc.geography.charas-bay", "doc.geography.bay-charas"),
    ("dawn-mtn.yaml", "mountains-dawn.yaml", "doc.geography.dawn-mountains", "doc.geography.mountains-dawn"),
    ("devseg-plateau.yaml", "plateau-devseg.yaml", "doc.geography.devseg-plateau", "doc.geography.plateau-devseg"),
    ("dryatic-mountains.yaml", "mountains-dryatic.yaml", "doc.geography.dryatic-mountains", "doc.geography.mountains-dryatic"),
    ("kach-land.yaml", "peninsula-kachar.yaml", "doc.geography.kachar-peninsula", "doc.geography.peninsula-kachar"),
    ("lac-lake.yaml", "lake-lakonis.yaml", "doc.geography.lakonis-lake", "doc.geography.lake-lakonis"),
    ("llyn-modris.yaml", "lake-llyn-modris.yaml", "doc.geography.llyn-modris", "doc.geography.lake-llyn-modris"),
    ("miron-river.yaml", "river-miron.yaml", "doc.geography.miron-river", "doc.geography.river-miron"),
    ("nahasa-desert.yaml", "desert-nahasa.yaml", "doc.geography.nahasa-desert", "doc.geography.desert-nahasa"),
    ("perassic-sea.yaml", "sea-perassic.yaml", "doc.geography.perassic-sea", "doc.geography.sea-perassic"),
    ("sethys-river.yaml", "river-sethys.yaml", "doc.geography.sethys-river", "doc.geography.river-sethys"),
    ("tanaesis-lake.yaml", "lake-tanaesis.yaml", "doc.geography.tanaesis-lake", "doc.geography.lake-tanaesis"),
    # --- culture ---
    ("charas-origin-tales.yaml", "tale-charas-origin.yaml", "doc.culture.charas-origin-tales", "doc.culture.tale-charas-origin"),
    ("dawn-taboo.yaml", "tale-dawn-taboo.yaml", "doc.culture.dawn-taboo", "doc.culture.tale-dawn-taboo"),
    ("husn-fulq-tales.yaml", "tale-husn-fulq.yaml", "doc.culture.husn-fulq-tales", "doc.culture.tale-husn-fulq"),
    ("kach-tales.yaml", "tale-kachar-three.yaml", "doc.culture.kachar-three-tales", "doc.culture.tale-kachar-three"),
    ("lac-tales.yaml", "tale-lakonis-lake.yaml", "doc.culture.lakonis-lake-tales", "doc.culture.tale-lakonis-lake"),
    ("lycaron-rock-tales.yaml", "tale-lycaron-rock.yaml", "doc.culture.lycaron-rock-tales", "doc.culture.tale-lycaron-rock"),
    # --- economy ---
    ("der-furs.yaml", "furs-deriat.yaml", "doc.economy.deriat-furs", "doc.economy.furs-deriat"),
    ("lycaron-mines.yaml", "mine-lycaron.yaml", "doc.economy.lycaron-mines", "doc.economy.mine-lycaron"),
    # --- politics ---
    ("saneopa.yaml", "throne-saneopa.yaml", "doc.politics.saneopa", "doc.politics.throne-saneopa"),
    ("paravenos.yaml", "throne-paravenos.yaml", "doc.politics.paravenos", "doc.politics.throne-paravenos"),
    ("charas-reign.yaml", "territory-charas-reign.yaml", "doc.politics.charas-reign", "doc.politics.territory-charas-reign"),
    ("dawn-stew.yaml", "territory-dawn-stewardship.yaml", "doc.politics.dawn-stewardship", "doc.politics.territory-dawn-stewardship"),
    ("kach-own.yaml", "territory-kachar-ownership.yaml", "doc.politics.kachar-ownership", "doc.politics.territory-kachar-ownership"),
    ("varcheg-swap.yaml", "territory-varcheg-swap.yaml", "doc.politics.varcheg-swap", "doc.politics.territory-varcheg-swap"),
    ("charas-cortain-secret.yaml", "clan-charas-cortain-secret.yaml", "doc.politics.charas-cortain-secret", "doc.politics.clan-charas-cortain-secret"),
    # --- war ---
    ("crossbow.yaml", "weapon-crossbow.yaml", "doc.war.crossbow", "doc.war.weapon-crossbow"),
    ("mamluk.yaml", "troop-mamluk.yaml", "doc.war.mamluk", "doc.war.troop-mamluk"),
    ("royal-guard.yaml", "troop-royal-guard.yaml", "doc.war.royal-guard", "doc.war.troop-royal-guard"),
    ("khuzait-military.yaml", "military-khuzait.yaml", "doc.war.khuzait-military", "doc.war.military-khuzait"),
    ("sturgia-military.yaml", "military-sturgia.yaml", "doc.war.sturgia-military", "doc.war.military-sturgia"),
    ("vlandia-military.yaml", "military-vlandia.yaml", "doc.war.vlandia-military", "doc.war.military-vlandia"),
]


class NoAlias(yaml.dumper.Dumper):
    def ignore_aliases(self, data):
        return True


def reorder(d):
    nd = {}
    for k in TOP_ORDER:
        if k in d:
            nd[k] = d[k]
    for k, v in d.items():
        if k not in nd:
            nd[k] = v
    return nd


def detect_prefix(doc):
    for a in doc.get("assertions", []):
        aid = str(a.get("id", ""))
        if aid.startswith("assertion."):
            return re.sub(r"-\d+$", "", aid[len("assertion."):])
    return None


def main():
    assert len({j[1] for j in JOBS}) == len(JOBS), "新文件名重复"
    for old_f, new_f, old_id, new_id in JOBS:
        for base, tag in ((OUT, "out"), (WS, "ws")):
            op = os.path.join(base, old_f)
            np = os.path.join(base, new_f)
            if os.path.exists(np) and os.path.exists(op):  # 新已写、旧未删（上次中断）
                os.remove(op)
                print("CLEANUP", tag, old_f, "(new 已存在)")
                continue
            if not os.path.exists(op):
                print(("SKIP" if os.path.exists(np) else "MISS"), tag, old_f)
                continue
            doc = yaml.safe_load(io.open(op, encoding="utf-8"))
            assert doc["id"] == old_id, (op, doc["id"], old_id)
            old_pfx = detect_prefix(doc)
            new_pfx = new_id.split(".")[-1]
            doc["id"] = new_id
            if old_pfx:
                oa, na = f"assertion.{old_pfx}", f"assertion.{new_pfx}"
                oe, ne = f"expr.{old_pfx}", f"expr.{new_pfx}"
                for a in doc.get("assertions", []):
                    if str(a.get("id", "")).startswith(oa):
                        a["id"] = na + a["id"][len(oa):]
                    for e in a.get("expressions", []):
                        if str(e.get("id", "")).startswith(oe):
                            e["id"] = ne + e["id"][len(oe):]
            doc = reorder(doc)
            doc = json.loads(json.dumps(doc))
            text = yaml.dump(doc, Dumper=NoAlias, allow_unicode=True, sort_keys=False,
                             default_flow_style=False, width=100)
            assert "&id" not in text and "*id" not in text
            io.open(np, "w", encoding="utf-8", newline="\n").write(text)
            os.remove(op)
            print("OK", tag, old_f, "->", new_f)
    print("done", len(JOBS))


if __name__ == "__main__":
    main()
