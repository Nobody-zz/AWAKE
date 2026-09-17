# -*- coding: utf-8 -*-
"""单人头盔批 · 快照 + 来源登记（只读 XML/DB，写 workspace 来源池）。

与 poc2 批**共用同一物理来源**（游戏 items XML），但**独立 source_id**：
  source.calradia.game.items-head  /  game-items-head.txt
⇒ 不碰 poc2 快照 hash，既有 13 张器物档一字不动。

快照每行＝一件单人头盔的官方属性转录（A 级 game_snapshot），供卡内 quote 定位。
"""
import hashlib
import io
import os
import re
import sqlite3
import xml.etree.ElementTree as ET
import yaml

GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBoxCore/ModuleData/items"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
WS_AUTH = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
SRC_ID = "source.calradia.game.items-head"
SRC_VER = "bannerlord-1.3.15.110062"
SNAP = "game-items-head.txt"
REG = "source-game-items-head.yaml"

MATLAB = {"Plate": "板", "Chainmail": "链甲", "Leather": "皮", "Cloth": "布"}
CANLAB = {"all": "全", "type1": "型一", "type2": "型二", "type3": "型三",
          "type4": "型四", "none": "不遮"}


def sha(b):
    return hashlib.sha256(b).hexdigest().upper()


def main():
    con = sqlite3.connect("file:%s?mode=ro" % DB, uri=True)
    cur = con.execute("SELECT stringId, text FROM localization_entries WHERE language='CNs'")
    cn = {r[0]: r[1] for r in cur.fetchall()}

    rows = []
    for fn in sorted(f for f in os.listdir(GAME) if f.lower().endswith(".xml")):
        root = ET.parse(os.path.join(GAME, fn)).getroot()
        for it in root.iter("Item"):
            if (it.get("Type") or "") != "HeadArmor":
                continue
            eid = it.get("id") or ""
            if eid.startswith("mp_"):
                continue
            nm = it.get("name") or ""
            m = re.match(r"^\{=([^}]+)\}(.*)$", nm)
            tok, en = (m.group(1), m.group(2).strip()) if m else ("", nm.strip())
            zh = cn.get(tok) or en
            arm = it.find(".//Armor")
            a = dict(arm.attrib) if arm is not None else {}
            ff = {}
            for c in it:
                if c.tag == "Flags":
                    ff = dict(c.attrib)
            cul = (it.get("culture") or "").replace("Culture.", "")
            seg = "%s | HeadArmor | 护头 %s | 重 %s | 材质 %s | 属 %s" % (
                zh, a.get("head_armor"), it.get("weight"),
                MATLAB.get(a.get("material_type"), a.get("material_type") or "-"),
                cul or "-")
            hc = a.get("hair_cover_type")
            bc = a.get("beard_cover_type")
            if hc:
                seg += " | 遮发 %s" % CANLAB.get(hc, hc)
            if bc:
                seg += " | 遮须 %s" % CANLAB.get(bc, bc)
            if a.get("stealth_factor"):
                seg += " | 潜行 %s" % a.get("stealth_factor")
            if ff.get("UseTeamColor"):
                seg += " | 认队色"
            if a.get("has_gender_variations") == "true":
                seg += " | 分男女"
            if re.search(r"over_", eid, re.I):
                seg += " | 叠穿"
            seg += " | 出处 %s#%s | 译名token %s" % (fn, eid, tok)
            rows.append((eid, seg))

    rows.sort(key=lambda x: x[0])
    snap_text = "".join("head.%s => %s\n" % (eid, seg) for eid, seg in rows)
    h = sha(snap_text.encode("utf-8"))
    io.open(os.path.join(WS_AUTH, "sources", SNAP), "w", encoding="utf-8", newline="\n").write(snap_text)

    reg = {
        "source_id": SRC_ID, "source_version": SRC_VER, "source_nature": "game_snapshot",
        "universe": "awake_current", "era": "current", "locator_root": SNAP,
        "source_content_hash": h, "content_tier": "base",
        "license_status": "permitted", "use_status": "active", "valid_until": None,
        "imported_at": "2026-09-16T12:00:00Z", "normalization_version": "utf8-lf-no-bom-v1",
    }
    with io.open(os.path.join(WS_AUTH, "sources", REG), "w", encoding="utf-8", newline="\n") as f:
        yaml.safe_dump(reg, f, allow_unicode=True, sort_keys=False)

    print("rows:", len(rows))
    print("snapshot:", SNAP, os.path.getsize(os.path.join(WS_AUTH, "sources", SNAP)), "B")
    print("hash:", h)
    print("--- sample ---")
    for eid, seg in rows[:3] + rows[-3:]:
        print("   ", seg)


if __name__ == "__main__":
    main()
