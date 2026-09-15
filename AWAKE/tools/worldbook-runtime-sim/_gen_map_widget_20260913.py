"""Generate an SVG fragment showing the real campaign map with a courier route.

Read-only. Uses the vanilla settlement coordinates (posX/posY from
settlements.xml, the same system as the runtime CampaignVec2) and the official
Chinese names from the BannerlordSage index.

North is up: the XML posY grows northwards, so screen y is flipped.

Run:
  C:\\Users\\26811\\.workbuddy\\binaries\\python\\versions\\3.13.12\\python.exe _gen_map_widget_20260913.py
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
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_map_widget_20260913.txt")

PAD = 44
WIDTH = 680
PLAN_W = 660.0   # x: 100 .. 760
PLAN_H = 490.0   # y: 100 .. 590
SCALE = min((WIDTH - PAD * 2) / PLAN_W, 1.0)

# The two towns we highlight.
A_ID = "town_V8"   # 奥斯蒂港, far north-west
B_ID = "town_K6"   # 奥多赫, far east

COURIER = 8.0
CAVALRY = 5.2
INFANTRY = 4.0


def find_xml():
    for c in XML_CANDIDATES:
        if os.path.isfile(c):
            return c
    raise SystemExit("settlements.xml not found")


def load_settlements():
    with open(find_xml(), encoding="utf-8") as h:
        data = h.read()
    out = {}
    for body in re.findall(r"<Settlement\b(.*?)>", data, re.S):
        sid = re.search(r'\bid="([^"]+)"', body)
        x = re.search(r'(?<!gate_)posX="([-0-9.]+)"', body)
        y = re.search(r'(?<!gate_)posY="([-0-9.]+)"', body)
        token = re.search(r'name="\{=([^}]+)\}', body)
        if sid and x and y:
            out[sid.group(1)] = (float(x.group(1)), float(y.group(1)),
                                 token.group(1) if token else None)
    return out


def load_names(tokens):
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


def main():
    coords = load_settlements()
    names = load_names({s: v[2] for s, v in coords.items()})
    named = {s: (v[0], v[1], names[s]) for s, v in coords.items() if s in names}
    towns = {s: v for s, v in named.items() if s.lower().startswith("town")}

    def sx(x):
        return PAD + (x - 100.0) * SCALE

    def sy(y):
        return PAD + (590.0 - y) * SCALE

    ax, ay, an = named[A_ID]
    bx, by, bn = named[B_ID]
    dist = math.hypot(ax - bx, ay - by)
    hours = max(1, math.ceil(dist / COURIER))

    parts = []
    parts.append('<svg viewBox="0 0 %d 525" width="100%%" style="max-width:100%%" '
                 'xmlns="http://www.w3.org/2000/svg" role="img">' % WIDTH)
    parts.append("<title>真实战役地图坐标上的信使路线</title>")
    parts.append("<desc>库赛特的奥多赫与瓦兰迪亚的奥斯蒂港之间，信使耗时 82 游戏小时。</desc>")

    # faint outline: the 53 towns only, so the shape reads as a map without noise
    parts.append('<g fill="#85B7EB" opacity="0.7">')
    for sid, (x, y, _n) in towns.items():
        if sid in (A_ID, B_ID):
            continue
        parts.append('<circle cx="%.0f" cy="%.0f" r="3"/>' % (sx(x), sy(y)))
    parts.append("</g>")

    # the route
    parts.append('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#185FA5" '
                 'stroke-width="1.8" stroke-dasharray="6 4"/>'
                 % (sx(ax), sy(ay), sx(bx), sy(by)))

    # endpoints
    for x, y, name in ((ax, ay, an), (bx, by, bn)):
        parts.append('<circle cx="%.1f" cy="%.1f" r="5" fill="#185FA5"/>' % (sx(x), sy(y)))
        parts.append('<circle cx="%.1f" cy="%.1f" r="9" fill="none" stroke="#185FA5" '
                     'stroke-width="1" opacity="0.5"/>' % (sx(x), sy(y)))

    # labels (kept away from the frame edges)
    parts.append('<text x="%.1f" y="%.1f" font-size="13" font-weight="500" '
                 'fill="#185FA5" text-anchor="start">%s</text>'
                 % (sx(ax) + 12, sy(ay) + 4, an))
    parts.append('<text x="%.1f" y="%.1f" font-size="13" font-weight="500" '
                 'fill="#185FA5" text-anchor="end">%s</text>'
                 % (sx(bx) - 12, sy(by) + 4, bn))

    # distance readout at the midpoint
    mx = (sx(ax) + sx(bx)) / 2.0
    my = (sy(ay) + sy(by)) / 2.0
    parts.append('<rect x="%.1f" y="%.1f" width="150" height="44" rx="8" '
                 'fill="var(--color-background-primary)" stroke="var(--color-border-tertiary)" '
                 'stroke-width="0.5"/>' % (mx - 75, my - 58))
    parts.append('<text x="%.1f" y="%.1f" font-size="13" font-weight="500" '
                 'fill="var(--color-text-primary)" text-anchor="middle">信使 %.0f 小时</text>'
                 % (mx, my - 40, hours))
    parts.append('<text x="%.1f" y="%.1f" font-size="12" '
                 'fill="var(--color-text-secondary)" text-anchor="middle">%.0f 地图单位 · %.1f 天</text>'
                 % (mx, my - 24, dist, hours / 24.0))

    # north marker + scale hint
    parts.append('<text x="%d" y="24" font-size="12" fill="var(--color-text-tertiary)">'
                 '北 ↑　53 座城镇的真实地图坐标（settlements.xml）</text>' % PAD)
    parts.append("</svg>")

    svg = "\n".join(parts)
    with open(OUT, "w", encoding="utf-8") as h:
        h.write(svg)
    print("wrote:", OUT)
    print("route: %s (%.1f, %.1f) -> %s (%.1f, %.1f)" % (an, ax, ay, bn, bx, by))
    print("distance %.1f | courier %.0f h (%.2f d) | cavalry %.0f h | infantry %.0f h"
          % (dist, hours, hours / 24.0,
             math.ceil(dist / CAVALRY), math.ceil(dist / INFANTRY)))


if __name__ == "__main__":
    main()
