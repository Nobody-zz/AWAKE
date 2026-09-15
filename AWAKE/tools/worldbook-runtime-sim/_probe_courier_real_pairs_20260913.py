"""Pick real settlement pairs and show the courier delivery time.

Read-only. Joins the vanilla settlement coordinates (settlements.xml posX/posY,
same coordinate system as the runtime CampaignVec2) with the official Chinese
names from the BannerlordSage SQLite index, then reports delivery time for the
current formula: travelHours = max(1, ceil(distance / 8)).

Run:
  C:\\Users\\26811\\.workbuddy\\binaries\\python\\versions\\3.13.12\\python.exe _probe_courier_real_pairs_20260913.py
"""

import math
import os
import re
import sqlite3

XML_CANDIDATES = [
    r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\settlements.xml",
    r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\settlements.xml",
    r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\ModuleData\settlements.xml",
]

DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"

COURIER_UNITS_PER_HOUR = 8.0   # AWAKE courier constant
VANILLA_PARTY_SPEED = 4.0      # DefaultPartySpeedCalculatingModel.BaseSpeed
CAVALRY_SPEED = 5.2            # pure-cavalry practical ceiling


def travel_hours(distance):
    if distance <= 0:
        return 1
    return max(1, math.ceil(distance / COURIER_UNITS_PER_HOUR))


def find_xml():
    for candidate in XML_CANDIDATES:
        if os.path.isfile(candidate):
            return candidate
    return None


def load_settlements():
    path = find_xml()
    if not path:
        raise SystemExit("settlements.xml not found")
    with open(path, encoding="utf-8") as handle:
        data = handle.read()

    # Attributes span several lines; the element is <Settlement ...> ... </Settlement>.
    # The trailing tag is <Components>/<Town>, so stop at the first '>' closing the header.
    pattern = re.compile(r"<Settlement\b(.*?)>", re.S)
    out = {}
    for body in pattern.findall(data):
        sid = re.search(r'\bid="([^"]+)"', body)
        # (?<!gate_) so we never pick up gate_posX / gate_posY.
        x = re.search(r'(?<!gate_)posX="([-0-9.]+)"', body)
        y = re.search(r'(?<!gate_)posY="([-0-9.]+)"', body)
        raw_name = re.search(r'name="\{=([^}]+)\}', body)
        if not (sid and x and y):
            continue
        out[sid.group(1)] = (
            float(x.group(1)),
            float(y.group(1)),
            raw_name.group(1) if raw_name else None,
        )
    return path, out


def load_names(tokens):
    """tokens: {settlementId: localization token}. Returns {settlementId: official CN name}."""
    con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
    con.row_factory = sqlite3.Row
    cur = con.cursor()
    names = {}
    for sid, token in tokens.items():
        if not token:
            continue
        cur.execute(
            "SELECT text FROM localization_entries WHERE language='CNs' AND stringId=? LIMIT 1",
            (token,),
        )
        row = cur.fetchone()
        if row:
            names[sid] = row["text"]
    con.close()
    return names


def kind_of(sid):
    low = sid.lower()
    if low.startswith("castle_village"):
        return "村庄"
    if low.startswith("village"):
        return "村庄"
    if low.startswith("castle"):
        return "城堡"
    if low.startswith("town"):
        return "城镇"
    if low.startswith("hideout"):
        return "藏匿点"
    return "聚落"


def main():
    path, coords = load_settlements()
    print("xml:", path)
    print("settlements with coords:", len(coords))

    names = load_names({sid: v[2] for sid, v in coords.items()})
    named = {}
    for sid, (x, y, _token) in coords.items():
        cn = names.get(sid)
        if cn:
            named[sid] = ((x, y), cn)
    print("with official CN name:", len(named))

    towns = {s: v for s, v in named.items() if kind_of(s) == "城镇"}
    print("towns with name:", len(towns))
    print()

    def dist(a, b):
        return math.hypot(a[0] - b[0], a[1] - b[1])

    # --- 1) all town pairs: nearest / median / farthest ---
    keys = sorted(towns.keys())
    pairs = []
    for i in range(len(keys)):
        for j in range(i + 1, len(keys)):
            a, b = keys[i], keys[j]
            pairs.append((dist(towns[a][0], towns[b][0]), a, b))
    pairs.sort()
    mid = len(pairs) // 2

    def show(label, item):
        d, a, b = item
        h = travel_hours(d)
        print("%-12s %s（%s） ↔ %s（%s）" % (label, towns[a][1], a, towns[b][1], b))
        print("             距离 %.1f 地图单位 | 信使 %.0f 小时（%.1f 天） | 骑兵(5.2) %.0f 小时 | 步兵(4) %.0f 小时"
              % (d, h, h / 24.0, math.ceil(d / CAVALRY_SPEED), math.ceil(d / VANILLA_PARTY_SPEED)))
        print()

    print("=== 全 %d 个城镇两两配对 ===" % len(towns))
    show("最近", pairs[0])
    show("中位", pairs[mid])
    show("最远", pairs[-1])

    # --- 2) compass extremes among towns: full east-west / north-south crossings ---
    print("=== 东西 / 南北 极点城市对 ===")
    by_x = sorted(towns.items(), key=lambda kv: kv[1][0][0])
    by_y = sorted(towns.items(), key=lambda kv: kv[1][0][1])
    for label, a, b in (
        ("最西↔最东", by_x[0][0], by_x[-1][0]),
        ("最南↔最北", by_y[0][0], by_y[-1][0]),
    ):
        show(label, (dist(towns[a][0], towns[b][0]), a, b))
    print()

    # --- 3) extremes on the map ---
    xs = [v[0][0] for v in named.values()]
    ys = [v[0][1] for v in named.values()]
    print("=== 地图尺度（全部有名字的聚落）===")
    print("X span %.1f | Y span %.1f" % (max(xs) - min(xs), max(ys) - min(ys)))
    print()
    print("=== 全部聚落里最远的两点 ===")
    allkeys = sorted(named.keys())
    best = (0.0, None, None)
    for i in range(len(allkeys)):
        for j in range(i + 1, len(allkeys)):
            a, b = allkeys[i], allkeys[j]
            d = dist(named[a][0], named[b][0])
            if d > best[0]:
                best = (d, a, b)
    d, a, b = best
    print("%s（%s, %s） ↔ %s（%s, %s） 距离 %.1f | 信使 %.0f 小时（%.1f 天）"
          % (named[a][1], kind_of(a), a, named[b][1], kind_of(b), b, d,
             travel_hours(d), travel_hours(d) / 24.0))


    print("=== 53 城镇清单（id / 官方中文名 / 坐标）===")
    for sid, ((x, y), cn) in sorted(towns.items(), key=lambda kv: kv[1][1]):
        print("%-14s %-12s (%7.1f, %7.1f)" % (sid, cn, x, y))
    print()


if __name__ == "__main__":
    main()
