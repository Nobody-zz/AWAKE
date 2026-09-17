# -*- coding: utf-8 -*-
"""护甲形制批 · 快照 + 来源登记（只读 XML/DB，写 workspace 来源池，2026-09-18）。

与头盔批**共用同一物理来源**（游戏 items XML），但**独立 source_id**：
  source.calradia.game.items-armor  /  game-items-armor.txt
⇒ 不动 `source.calradia.game.items-head` 的快照 hash，已上线的 10 张头盔卡一字不动。

快照每行＝一件单人护具的官方属性转录（A 级 game_snapshot），供卡内 quote 定位。
盾的属性在 `<Weapon>` 节点（不是 `<Armor>`）：护值 `body_armor`、盾类 `weapon_class`、
长度 `weapon_length`、耐久 `hit_points`。
"""
import hashlib
import io
import os
import re
import sqlite3
import xml.etree.ElementTree as ET

GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBoxCore/ModuleData/items"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
WS_AUTH = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
SRC_ID = "source.calradia.game.items-armor"
SRC_VER = "bannerlord-1.3.15.110062"
SNAP = "game-items-armor.txt"
REG = "source-game-items-armor.yaml"

PARTS = {"BodyArmor": ("躯干", "body_armor"), "LegArmor": ("腿", "leg_armor"),
         "HandArmor": ("手", "arm_armor"), "Cape": ("披风", "body_armor")}
MATLAB = {"Plate": "板", "Chainmail": "链甲", "Leather": "皮", "Cloth": "布"}
SHIELDLAB = {"LargeShield": "大盾", "SmallShield": "小盾", "Shield": "盾"}
EXCLUDE = re.compile(r"^dummy_|_dummy|^spawned_|^test_", re.I)


def sha(b):
    return hashlib.sha256(b).hexdigest().upper()


def main():
    con = sqlite3.connect("file:%s?mode=ro" % DB, uri=True)
    cn = {r[0]: r[1] for r in con.execute(
        "SELECT stringId, text FROM localization_entries WHERE language='CNs'").fetchall()}

    rows = []
    for fn in sorted(f for f in os.listdir(GAME) if f.lower().endswith(".xml")):
        root = ET.parse(os.path.join(GAME, fn)).getroot()
        for it in root.iter("Item"):
            typ = it.get("Type") or ""
            if typ not in PARTS and typ != "Shield":
                continue
            eid = it.get("id") or ""
            if eid.startswith("mp_") or EXCLUDE.search(eid):
                continue
            nm = it.get("name") or ""
            m = re.match(r"^\{=([^}]+)\}(.*)$", nm)
            tok, en = (m.group(1), m.group(2).strip()) if m else ("", nm.strip())
            zh = cn.get(tok) or en
            cul = (it.get("culture") or "").replace("Culture.", "")
            weight = it.get("weight")

            if typ == "Shield":
                w = it.find(".//Weapon")
                wa = dict(w.attrib) if w is not None else {}
                seg = "%s | Shield | 盾类 %s | 护 %s | 长 %s | 耐久 %s | 重 %s | 属 %s" % (
                    zh, SHIELDLAB.get(wa.get("weapon_class"), wa.get("weapon_class") or "-"),
                    wa.get("body_armor") or "-", wa.get("weapon_length") or "-",
                    wa.get("hit_points") or "-", weight or "-", cul or "-")
            else:
                part, field = PARTS[typ]
                arm = it.find(".//Armor")
                a = dict(arm.attrib) if arm is not None else {}
                seg = "%s | %s | 护 %s | 重 %s | 材质 %s | 属 %s" % (
                    zh, part, a.get(field) or "-", weight or "-",
                    MATLAB.get(a.get("material_type"), a.get("material_type") or "-"), cul or "-")
                if a.get("has_gender_variations") == "true":
                    seg += " | 分男女"
                if re.search(r"over_", eid, re.I):
                    seg += " | 叠穿"
            seg += " | 出处 %s#%s | 译名token %s" % (fn, eid, tok)
            rows.append((eid, seg))

    rows.sort(key=lambda x: x[0])
    snap_text = "".join("armor.%s => %s\n" % (eid, seg) for eid, seg in rows)
    h = sha(snap_text.encode("utf-8"))
    out_snap = os.path.join(WS_AUTH, "sources", SNAP)
    io.open(out_snap, "w", encoding="utf-8", newline="\n").write(snap_text)

    # 手写 YAML（照 source-game-items-head.yaml 的键序与引号约定；不引 pyyaml）
    reg_text = (
        "source_id: %s\n"
        "source_version: %s\n"
        "source_nature: game_snapshot\n"
        "universe: awake_current\n"
        "era: current\n"
        "locator_root: %s\n"
        "source_content_hash: %s\n"
        "content_tier: base\n"
        "license_status: permitted\n"
        "use_status: active\n"
        "valid_until: null\n"
        "imported_at: '2026-09-18T01:00:00Z'\n"
        "normalization_version: utf8-lf-no-bom-v1\n"
    ) % (SRC_ID, SRC_VER, SNAP, h)
    io.open(os.path.join(WS_AUTH, "sources", REG), "w",
            encoding="utf-8", newline="\n").write(reg_text)

    by_type = {}
    for fn in sorted(f for f in os.listdir(GAME) if f.lower().endswith(".xml")):
        root = ET.parse(os.path.join(GAME, fn)).getroot()
        for it in root.iter("Item"):
            t = it.get("Type") or ""
            e = it.get("id") or ""
            if (t in PARTS or t == "Shield") and not e.startswith("mp_") and not EXCLUDE.search(e):
                by_type[t] = by_type.get(t, 0) + 1

    print("rows = %d  %s" % (len(rows), by_type))
    print("snapshot: %s  %d B" % (SNAP, os.path.getsize(out_snap)))
    print("hash    : %s" % h)
    print("--- sample ---")
    for eid, seg in rows[:2] + rows[-2:]:
        print("   ", seg)


if __name__ == "__main__":
    main()
