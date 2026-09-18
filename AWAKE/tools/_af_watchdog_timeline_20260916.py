"""AnimusForge FreezeWatchdog 时间线 → 内存/阻塞走势（只读取证）。

甲方口径：AF 长期被诟病"内存占用大、游戏被拖慢"。本脚本把 36MB 的
FreezeWatchdog_Timeline.txt 拉成可判读的时间序列，回答三个问题：

  1. 内存是"高但稳定"还是"持续增长"（泄漏特征）？
  2. 主线程有没有被真正阻塞（看 activeMs，不是看 elapsedMs）？
  3. 哪些任务最耗时（elapsedMs Top）？

⚠️ 归因纪律：workingSetMB / privateMB 是**整个游戏进程**的量，本机装了 30+ 个 mod，
不能全算到 AnimusForge 头上。本脚本只报"它自己的子任务耗时"，进程内存只报走势。

用法：
    python _af_watchdog_timeline_20260916.py
"""
import os
import re
from collections import Counter

MOD = (r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
       r"\Modules\AnimusForge")
TL = os.path.join(MOD, "Logs", "FreezeWatchdog_Timeline.txt")
DUMPS = os.path.join(MOD, "Logs", "FreezeDumps")

# [2026-08-03 01:13:29.280] [FreezeWatchdog] seq=70 [MARK] name=... detail=...
LINE_RE = re.compile(r"^\[(?P<ts>[\d\- :.]+)\]\s+\[FreezeWatchdog\]\s+seq=(?P<seq>\d+)")
# process={workingSetMB=3465 privateMB=10866 ... gcMB=396 ...}
WS_RE = re.compile(r"workingSetMB=(\d+)")
PV_RE = re.compile(r"privateMB=(\d+)")
GC_RE = re.compile(r"gcMB=(\d+)")
TH_RE = re.compile(r"osThreads=(\d+)")
ACTIVE_RE = re.compile(r"activeMs=([\d.]+)")
HEART_RE = re.compile(r"heartbeatAgeMs=([\d.]+)")
# 阶段耗时行：... end <name> elapsedMs=67.59   /  ... dtMs=5.00
END_RE = re.compile(r"end (?P<name>[\w.]+) elapsedMs=(?P<ms>[\d.]+)")
DT_RE = re.compile(r"frame_begin \S+ dtMs=(?P<ms>[\d.]+)")
# 触发/告警行里的大 elapsedMs
TRIG_RE = re.compile(r"name=(?P<name>[\w.]+) detail=.*?elapsedMs=(?P<ms>[\d.]+)")


def main():
    if not os.path.exists(TL):
        print("! 时间线不存在:", TL)
        return
    size = os.path.getsize(TL)
    print(f"# {TL}\n# size={size:,} B\n")

    series = []          # (ts, seq, ws, pv, gc, th)
    active_max = (0.0, "")
    active_over = []     # activeMs > 50
    heart_max = (0.0, "")
    long_tasks = []      # (ms, name)
    dt_vals = []
    trigger_long = []    # 触发行里的长 elapsedMs
    n_lines = 0

    with open(TL, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            n_lines += 1

            # ⚠️ 顺序要紧：性能明细行（"| ... end X elapsedMs=N |"）也可能同时
            # 匹配看门狗行头，所以先把 elapsedMs / dtMs 摘出来，再处理 MARK 行，
            # 不能看到 MARK 就 continue（否则长任务表永远是空的）。
            for em in END_RE.finditer(line):
                ms = float(em.group("ms"))
                if ms >= 50:
                    long_tasks.append((ms, em.group("name")))
            dm = DT_RE.search(line)
            if dm:
                dt_vals.append(float(dm.group("ms")))

            m = LINE_RE.match(line)
            if m:
                ws = WS_RE.search(line)
                pv = PV_RE.search(line)
                gc = GC_RE.search(line)
                th = TH_RE.search(line)
                am = ACTIVE_RE.search(line)
                hb = HEART_RE.search(line)
                if ws:
                    series.append((m.group("ts"), int(m.group("seq")),
                                   int(ws.group(1)),
                                   int(pv.group(1)) if pv else -1,
                                   int(gc.group(1)) if gc else -1,
                                   int(th.group(1)) if th else -1))
                if am:
                    v = float(am.group(1))
                    if v > active_max[0]:
                        active_max = (v, line[:200])
                    if v > 50:
                        active_over.append((v, m.group("ts")))
                if hb and float(hb.group(1)) > heart_max[0]:
                    heart_max = (float(hb.group(1)), line[:200])
                tm = TRIG_RE.search(line)
                if tm and float(tm.group("ms")) > 5000:
                    trigger_long.append((float(tm.group("ms")),
                                         tm.group("name"), m.group("ts")))

    print(f"总行数 {n_lines:,}｜带内存采样 {len(series):,}｜"
          f"帧间隔样本 {len(dt_vals):,}｜≥50ms 任务 {len(long_tasks):,}\n")

    # --- 1. 内存走势 ---
    print("=" * 68)
    print("1) 内存走势（整个游戏进程；本机 30+ mod，不可全归因 AF）")
    if series:
        step = max(1, len(series) // 14)
        print(f"   {'时间':<22}{'seq':>7}{'workMB':>9}{'privMB':>9}{'gcMB':>7}{'线程':>6}")
        for i in range(0, len(series), step):
            ts, seq, ws, pv, gc, th = series[i]
            print(f"   {ts:<22}{seq:>7}{ws:>9}{pv:>9}{gc:>7}{th:>6}")
        ts, seq, ws, pv, gc, th = series[-1]
        print(f"   {ts:<22}{seq:>7}{ws:>9}{pv:>9}{gc:>7}{th:>6}  <- 末尾")
        wss = [s[2] for s in series]
        pvs = [s[3] for s in series if s[3] > 0]
        gcs = [s[4] for s in series if s[4] > 0]
        print(f"\n   workMB   min={min(wss)} max={max(wss)} 首={wss[0]} 尾={wss[-1]}")
        if pvs:
            print(f"   privMB   min={min(pvs)} max={max(pvs)} 首={pvs[0]} 尾={pvs[-1]}")
        if gcs:
            print(f"   gcMB     min={min(gcs)} max={max(gcs)} 首={gcs[0]} 尾={gcs[-1]}")
        print("   ⚠️ 判读：若尾显著高于首且单调上行 ⇒ 泄漏特征；"
              "若在高位震荡 ⇒ 高占用但稳定。")

    # --- 2. 主线程阻塞 ---
    print("\n" + "=" * 68)
    print("2) 主线程阻塞（activeMs = 主线程真被卡住的毫秒）")
    print(f"   最大 activeMs = {active_max[0]}")
    if active_max[1]:
        print(f"     出处 {active_max[1]}")
    print(f"   最大 heartbeatAgeMs = {heart_max[0]}（心跳间隔，正常应 <50ms）")
    print(f"   activeMs>50 的采样数 = {len(active_over)}")
    for v, ts in sorted(active_over, reverse=True)[:8]:
        print(f"      {v:>8} ms  @ {ts}")

    # --- 3. 最耗时的子任务 ---
    print("\n" + "=" * 68)
    print("3) 最耗时的子任务 Top 20（elapsedMs，AF 自己的阶段计时）")
    for ms, name in sorted(long_tasks, reverse=True)[:20]:
        print(f"   {ms:>10.2f} ms   {name}")

    agg = Counter()
    tot = Counter()
    for ms, name in long_tasks:
        agg[name] += 1
        tot[name] += ms
    print("\n   按累计耗时排序 Top 12（次数 / 累计 ms）")
    for name, s in tot.most_common(12):
        print(f"   {s:>12.1f} ms   n={agg[name]:<6} {name}")

    # --- 4. 帧间隔 ---
    if dt_vals:
        print("\n" + "=" * 68)
        print("4) 帧间隔 dtMs（帧耗时；>16.7 即低于 60fps）")
        s = sorted(dt_vals)
        n = len(s)
        print(f"   样本 {n:,}｜min={s[0]:.2f} 中位={s[n//2]:.2f} "
              f"p95={s[int(n*0.95)]:.2f} max={s[-1]:.2f}")
        over33 = sum(1 for v in dt_vals if v > 33.3)
        print(f"   >33.3ms（低于 30fps）的帧 {over33:,} 次 "
              f"({over33 / n * 100:.2f}%)")

    # --- 5. 触发/告警里的长耗时 ---
    print("\n" + "=" * 68)
    print("5) 看门狗触发记录中的长 elapsedMs（>5s）")
    if trigger_long:
        for ms, name, ts in sorted(trigger_long, reverse=True)[:10]:
            print(f"   {ms:>12.1f} ms  {name:<40} @ {ts}")
    else:
        print("   无")

    print("\n" + "=" * 68)
    print("6) 冻结转储（游戏真的卡死过的物证）")
    if os.path.isdir(DUMPS):
        ds = sorted(os.listdir(DUMPS))
        if ds:
            for d in ds:
                print(f"   {os.path.getsize(os.path.join(DUMPS, d)):>12,} B  {d}")
        else:
            print("   目录为空")
    else:
        print("   无 FreezeDumps 目录")


if __name__ == "__main__":
    main()
