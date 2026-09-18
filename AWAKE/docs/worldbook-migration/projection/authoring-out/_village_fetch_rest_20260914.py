# -*- coding: utf-8 -*-
"""取数：待铺 135 村（瓦兰迪亚/库赛特/巴旦尼亚/斯特吉亚）的官方名与描述文。
DB 只读；name/descriptionText 是 {=token}，从字段本身解析 token 再查 CNs。
产出 _village_rest_20260914.json；按文化圈打印供人读（写 L2 用）。
用法：python _village_fetch_rest_20260914.py <culture-en|all>
"""
import os, io, re, json, sqlite3, sys

AO = os.path.dirname(os.path.abspath(__file__))
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
OUTJSON = os.path.join(AO, "_village_rest_20260914.json")

inv = json.load(io.open(os.path.join(AO, "_village_inventory_20260913.json"), encoding="utf-8"))

# 已入档 sid（双键：locator + entity_ids；大小写归一）
import glob
done = set()
for p in sorted(glob.glob(os.path.join(AO, "village-*.yaml"))):
    t = io.open(p, encoding="utf-8").read()
    for s in re.findall(r"bannerlord\.villages#([A-Za-z0-9_]+)", t):
        done.add(s.lower())
    for s in re.findall(r"entity\.settlement\.([A-Za-z0-9_]+)", t):
        done.add(s.lower())

rest = [v for v in inv if v["sid"].lower() not in done]
print("待铺村数:", len(rest), " 已入档:", len(inv) - len(rest))

con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
con.row_factory = sqlite3.Row

def loc(tok, lang="CNs"):
    if not tok:
        return None
    r = con.execute("SELECT text FROM localization_entries WHERE stringId=? AND language=? LIMIT 1",
                    (tok, lang)).fetchone()
    return r["text"] if r else None

out = {}
for v in rest:
    sid = v["sid"]
    row = con.execute("SELECT name, descriptionText FROM bannerlord_settlements WHERE settlementId=? LIMIT 1",
                      (sid,)).fetchone()
    if row is None:
        print("  !! DB 无此 settlementId:", sid, v["en"]); continue
    nm = row["name"] or ""
    ds = row["descriptionText"] or ""
    ntok = nm.split("{=")[1].split("}")[0] if "{=" in nm else ""
    dtok = ds.split("{=")[1].split("}")[0] if "{=" in ds else ""
    cn = loc(ntok) or v["cn"]
    desc_cn = loc(dtok)
    if not desc_cn:
        print("  !! 无中文描述:", sid, v["en"]); desc_cn = ""
    out[sid] = {"sid": sid, "en": v["en"], "cn": cn, "culture": v["culture"],
                "desc_cn": re.sub(r"\s+", "", desc_cn), "ntok": ntok, "dtok": dtok}

json.dump(out, io.open(OUTJSON, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("写出:", OUTJSON, " 条目:", len(out))

want = sys.argv[1] if len(sys.argv) > 1 else "all"
CN = {"aserai": "阿塞莱", "empire": "帝国", "vlandia": "瓦兰迪亚",
      "khuzait": "库赛特", "battania": "巴旦尼亚", "sturgia": "斯特吉亚"}
for c in ["vlandia", "khuzait", "battania", "sturgia"]:
    if want not in ("all", c):
        continue
    xs = [x for x in out.values() if x["culture"] == c]
    print("\n" + "=" * 90)
    print(f"### {CN[c]}  {len(xs)} 村")
    for i, x in enumerate(sorted(xs, key=lambda y: y["sid"]), 1):
        print(f"[{i:>2}] {x['sid']:<24} {x['en']:<16} {x['cn']}")
        print(f"      {x['desc_cn']}")
