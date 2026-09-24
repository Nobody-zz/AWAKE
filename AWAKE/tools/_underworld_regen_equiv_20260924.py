# -*- coding: utf-8 -*-
"""复核：生成器跑出来的结果，与手工改完的现役档**逐字节一致**吗？

这是本任务最强的判据 —— 如果一致，说明：
  ① 手工那 8 档改对了（doc id 换了、子 id 留旧、其余一字未动）
  ② 生成器与现役档不会分叉（谁重跑都不会退回）

方法：把生成器复制到临时目录真跑（输出目录指向 tmp），
      将它写出的 8 档与仓库里现役的 8 档逐字节比对。
判据：8/8 完全一致。任何一档不一致即 FAIL（并打出首个差异行）。
"""
import difflib
import io
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = r"D:\AWAKE-Dev\AWAKE"
GEN = os.path.join(ROOT, "docs", "worldbook-migration", "projection",
                   "authoring-out", "_gen_dark_20260920.py")
LIVE_WS = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1", "authoring")
LIVE_AO = os.path.join(ROOT, "docs", "worldbook-migration", "projection", "authoring-out")
REPORT = os.path.join(ROOT, "tools", "_underworld_regen_equiv_20260924.txt")
PY = sys.executable

PAIRS = [
    "underworld-alleys.yaml", "underworld-gang-leaders.yaml", "underworld-struggle.yaml",
    "underworld-gangs.yaml", "underworld-crime-rating.yaml", "underworld-blood-money.yaml",
    "underworld-bandits.yaml", "underworld-smuggling.yaml",
]

lines = []


def log(s):
    lines.append(s)


def main():
    log("== 判据：生成器重跑结果 ≡ 现役档（逐字节）==")
    log("")

    tmp = tempfile.mkdtemp(prefix="awake_regen_")
    try:
        ws = os.path.join(tmp, "authoring")
        ao = os.path.join(tmp, "ao")
        os.makedirs(os.path.join(ws, "sources"))
        os.makedirs(ao)
        # 带输入过去
        indir = os.path.join(ROOT, "docs", "worldbook-migration", "projection", "authoring-out")
        for fn in os.listdir(indir):
            if fn.startswith("_dark_A_") and fn.endswith(".json"):
                shutil.copy2(os.path.join(indir, fn), os.path.join(ao, fn))

        src = io.open(GEN, encoding="utf-8").read()
        src, n1 = re.subn(r'^WS = r"[^"]*"', 'WS = r"%s"' % ws.replace("\\", "/"), src, flags=re.M)
        src, n2 = re.subn(r'^AO = r"[^"]*"', 'AO = r"%s"' % ao.replace("\\", "/"), src, flags=re.M)
        assert n1 == 1 and n2 == 1, "目录常量改写失败"

        g = os.path.join(tmp, "gen.py")
        io.open(g, "w", encoding="utf-8", newline="\n").write(src)

        r = subprocess.run([PY, g], capture_output=True, timeout=900, cwd=ROOT)
        se = (r.stderr or b"").decode("utf-8", "replace")
        if r.returncode != 0:
            log("!! 生成器跑失败 rc=%d" % r.returncode)
            log(se[-900:])
            with io.open(REPORT, "w", encoding="utf-8", newline="\n") as f:
                f.write("\n".join(lines) + "\n")
            print("\n".join(lines))
            return 2

        allok = True
        for fn in PAIRS:
            gp = os.path.join(ws, fn)
            lp = os.path.join(LIVE_WS, fn)
            if not os.path.exists(gp):
                log("!! 生成器没写出 %s" % fn)
                allok = False
                continue
            a = open(gp, "rb").read()
            b = open(lp, "rb").read()
            if a == b:
                log("   %-32s 逐字节一致  OK" % fn)
            else:
                allok = False
                log("   %-32s !! 不一致（生成 %d 字节 / 现役 %d 字节）" % (fn, len(a), len(b)))
                ta = a.decode("utf-8").split("\n")
                tb = b.decode("utf-8").split("\n")
                diff = list(difflib.unified_diff(ta, tb, "生成器", "现役", lineterm="", n=1))
                for d in diff[:14]:
                    log("        " + d[:150])

        # 顺带看临时 AO 侧
        log("")
        log("[AO 侧]")
        for fn in PAIRS:
            gp = os.path.join(ao, fn)
            lp = os.path.join(LIVE_AO, fn)
            if not os.path.exists(gp):
                log("   %-32s !! 生成器没写" % fn)
                allok = False
            elif open(gp, "rb").read() == open(lp, "rb").read():
                log("   %-32s 逐字节一致  OK" % fn)
            else:
                log("   %-32s !! 不一致" % fn)
                allok = False

        log("")
        log("结论：%s" % ("8/8 两目录全一致 —— 手工改动 = 生成器产出，不会分叉"
                        if allok else "!! 有分叉，手工改动与生成器不同源"))
        with io.open(REPORT, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(lines) + "\n")
        print("\n".join(lines))
        return 0 if allok else 1
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
