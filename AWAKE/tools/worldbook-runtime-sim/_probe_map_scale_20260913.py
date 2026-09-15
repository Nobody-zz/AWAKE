"""Probe the Bannerlord campaign-map coordinate scale.

Read-only. Extracts posX/posY from the vanilla settlements.xml so we can
calibrate any 'distance -> travel time' formula against the real map span.

Run:
  C:\\Users\\26811\\.workbuddy\\binaries\\python\\versions\\3.13.12\\python.exe _probe_map_scale_20260913.py
"""

import os
import re

CANDIDATES = [
    r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\settlements.xml",
    r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\settlements.xml",
    r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\ModuleData\settlements.xml",
]


def main() -> None:
    path = None
    for candidate in CANDIDATES:
        if os.path.isfile(candidate):
            path = candidate
            break
    if path is None:
        print("settlements.xml not found in candidates")
        return

    print("file:", path)
    with open(path, encoding="utf-8") as handle:
        data = handle.read()

    xs = [float(v) for v in re.findall(r'posX="([-0-9.]+)"', data)]
    ys = [float(v) for v in re.findall(r'posY="([-0-9.]+)"', data)]
    print("samples:", len(xs), len(ys))
    if not xs:
        print("no posX attributes; head:")
        print(data[:600])
        return

    print("x: min=%.2f max=%.2f span=%.2f" % (min(xs), max(xs), max(xs) - min(xs)))
    print("y: min=%.2f max=%.2f span=%.2f" % (min(ys), max(ys), max(ys) - min(ys)))

    # Extreme diagonal between the two farthest settlements (map units).
    far = 0.0
    n = len(xs)
    for i in range(0, n, max(1, n // 40)):
        for j in range(0, n, max(1, n // 40)):
            d = ((xs[i] - xs[j]) ** 2 + (ys[i] - ys[j]) ** 2) ** 0.5
            if d > far:
                far = d
    print("approx max pairwise distance (sampled): %.2f" % far)

    # What a vanilla party (speed 4 map-units/hour) covers per game day.
    for speed in (4.0, 5.0, 6.0, 8.0, 10.0):
        per_day = speed * 24.0
        print("speed=%.1f units/h -> %.0f units/day -> cross-map %.2f days"
              % (speed, per_day, far / per_day if per_day else 0.0))


if __name__ == "__main__":
    main()
