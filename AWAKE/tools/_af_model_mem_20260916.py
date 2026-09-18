"""模型到底吃多少内存、是不是它拖慢的 —— 针对性验证。

三个问题：
  Q1 模型什么时候加载的？（启动时 / 首次对话时）—— 看 onnx=True 首次出现前后的内存跳变
  Q2 它占了多少？—— 对比 onnx=True 行与全体的 workingSet 分布
  Q3 卡顿时刻内存高不高？—— 若 activeMs 大的时刻内存并不高，则"内存→拖慢"不成立

⚠️ 归因纪律：workingSetMB/privateMB 是整个游戏进程的量（本机 30+ mod），
所以只用"同一会话内的相对变化"来判断，不把它绝对值当 AF 的占用。
"""
import os
import re

MOD = (r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
       r"\Modules\AnimusForge")
TL = os.path.join(MOD, "Logs", "FreezeWatchdog_Timeline.txt")

LINE_RE = re.compile(r"^\[(?P<ts>[\d\- :.]+)\]\s+\[FreezeWatchdog\]\s+seq=(?P<seq>\d+)")
WS = re.compile(r"workingSetMB=(\d+)")
PV = re.compile(r"privateMB=(\d+)")
GC = re.compile(r"gcMB=(\d+)")
ACT = re.compile(r"activeMs=([\d.]+)")
FRAME = re.compile(r"frame=(\d+)")


def main():
    rows = []   # dict per watchdog sample
    with open(TL, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            m = LINE_RE.match(line)
            if not m:
                continue
            w = WS.search(line)
            if not w:
                continue
            pv = PV.search(line)
            gc = GC.search(line)
            ac = ACT.search(line)
            fr = FRAME.search(line)
            rows.append({
                "ts": m.group("ts"),
                "seq": int(m.group("seq")),
                "ws": int(w.group(1)),
                "pv": int(pv.group(1)) if pv else -1,
                "gc": int(gc.group(1)) if gc else -1,
                "act": float(ac.group(1)) if ac else 0.0,
                "frame": int(fr.group(1)) if fr else -1,
                "onnx": "onnx=True" in line,
            })

    print(f"采样 {len(rows):,} 条｜其中 onnx=True {sum(r['onnx'] for r in rows)} 条\n")

    # ---------- Q1 加载时机 ----------
    print("=" * 70)
    print("Q1 模型何时加载：onnx=True 首次出现前后")
    idx = next((i for i, r in enumerate(rows) if r["onnx"]), None)
    if idx is None:
        print("  没找到 onnx=True")
    else:
        lo, hi = max(0, idx - 6), min(len(rows), idx + 7)
        print(f"  {'':2}{'时间':<22}{'seq':>7}{'frame':>9}{'workMB':>9}{'privMB':>9}{'onnx':>6}")
        for i in range(lo, hi):
            r = rows[i]
            mark = "  <== 首次 onnx=True" if i == idx else ""
            print(f"  {i:<2}{r['ts']:<22}{r['seq']:>7}{r['frame']:>9}"
                  f"{r['ws']:>9}{r['pv']:>9}{str(r['onnx']):>6}{mark}")
        before = [r["ws"] for r in rows[:idx]]
        after = [r["ws"] for r in rows[idx:idx + 40]]
        if before and after:
            print(f"\n  首次之前 workMB：min={min(before)} max={max(before)} "
                  f"末值={before[-1]}")
            print(f"  首次之后 40 条   ：min={min(after)} max={max(after)} "
                  f"首值={after[0]}")
            print(f"  ⇒ 跳变 ≈ {after[0] - before[-1]:+d} MB")

    # ---------- Q2 占用对比 ----------
    print("\n" + "=" * 70)
    print("Q2 onnx=True 与整体 的内存分布对比")
    on = [r["ws"] for r in rows if r["onnx"]]
    off = [r["ws"] for r in rows if not r["onnx"]]
    allv = [r["ws"] for r in rows]
    for label, v in (("全体", allv), ("onnx=True", on), ("onnx=False", off)):
        if v:
            s = sorted(v)
            print(f"  {label:<12} n={len(v):<7} min={s[0]:<7} 中位={s[len(s)//2]:<7} "
                  f"max={s[-1]:<7} 均值={sum(v)//len(v)}")
    if on and off:
        print(f"\n  onnx=True 均值 − onnx=False 均值 = "
              f"{sum(on)//len(on) - sum(off)//len(off):+d} MB")

    # ---------- Q3 卡顿时刻的内存 ----------
    print("\n" + "=" * 70)
    print("Q3 卡顿时刻（activeMs>50）内存高不高")
    stalls = [r for r in rows if r["act"] > 50]
    print(f"  卡顿采样 {len(stalls)} 条")
    if stalls:
        sw = sorted(r["ws"] for r in stalls)
        print(f"  这些时刻 workMB：min={sw[0]} 中位={sw[len(sw)//2]} max={sw[-1]}")
        print(f"  全体        workMB：min={min(allv)} 中位={sorted(allv)[len(allv)//2]} "
              f"max={max(allv)}")
        print(f"\n  最卡的 10 条：")
        for r in sorted(stalls, key=lambda x: -x["act"])[:10]:
            print(f"    activeMs={r['act']:>8}  workMB={r['ws']:>6}  "
                  f"gcMB={r['gc']:>5}  @ {r['ts']}")


if __name__ == "__main__":
    main()
