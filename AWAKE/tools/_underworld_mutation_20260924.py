# -*- coding: utf-8 -*-
"""变异检验（v2）：证明 `_gen_dark_20260920.py` 的 FN_PREFIX 防护真的起作用。

v1 的错：探针自己去正则抠 `FN_PREFIX` 表来推算文件名 —— 于是变异 A（把
`fn = FN_PREFIX.get(...)` 还原成 `fn = doc["slug"]`）**改的根本不是探针读的那一段**，
探针照样读到表、照样算出新名 ⇒ 变异打不红。**探针测的不是被测物**（恒绿）。

v2 的改法：**让生成器本人说话。** 把生成器复制到临时目录、改掉里面的输出目录常量
（WS / AO 指向 tmp），真跑一遍 `--check` 之外的写盘路径，然后 `ls tmp` 看它到底写出了
哪些文件名。`--check` 不写盘，所以要跑真路径。

三变异：
  A 把 `fn = FN_PREFIX.get(doc["slug"], doc["slug"])` 还原成 `fn = doc["slug"]`  → 应出 8 个旧名
  B 表里删掉 town-alleys 一项                                                  → 应出 1 个旧名
  对照 不动                                                                   → 应出 0 个旧名
"""
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
REPORT = os.path.join(ROOT, "tools", "_underworld_mutation_20260924.txt")
PY = sys.executable

OLD_SLUGS = ["town-alleys", "alley-gang-leaders", "alley-struggle", "town-gangs",
             "crime-rating", "blood-money", "bandits", "smuggling"]
OLD_NAMES = set(s + ".yaml" for s in OLD_SLUGS)

lines = []


def log(s):
    lines.append(s)


def run_gen_in_tmp(raw_src: bytes, tag: str):
    """把生成器放到临时目录、输出指向 tmp，真跑一遍，返回它写出的 yaml 文件名集合。"""
    tmp = tempfile.mkdtemp(prefix="awake_mut_%s_" % tag)
    try:
        # 目录结构：tmp/authoring (WS) 与 tmp/ao (AO)
        ws = os.path.join(tmp, "authoring")
        ao = os.path.join(tmp, "ao")
        os.makedirs(os.path.join(ws, "sources"))   # 生成器还要往 WS/sources/ 落快照（第 197 行）
        os.makedirs(ao)

        # ⚠️ 生成器要从 AO 读它的输入 `_dark_A_20260920.json`（第 79 行），
        #    输入不搬过去，临时目录里就跑不起来。只搬这一个输入文件，不搬产物。
        indir = os.path.join(ROOT, "docs", "worldbook-migration", "projection", "authoring-out")
        for fn in os.listdir(indir):
            if fn.startswith("_dark_A_") and fn.endswith(".json"):
                shutil.copy2(os.path.join(indir, fn), os.path.join(ao, fn))

        src = raw_src.decode("utf-8")
        # 重定向两个输出目录常量
        src2, n1 = re.subn(r'^WS = r"[^"]*"', 'WS = r"%s"' % ws.replace("\\", "/"), src, flags=re.M)
        src2, n2 = re.subn(r'^AO = r"[^"]*"', 'AO = r"%s"' % ao.replace("\\", "/"), src2, flags=re.M)
        if n1 != 1 or n2 != 1:
            return None, "!! 目录常量改写失败 WS=%d AO=%d" % (n1, n2)

        # 不要 --check（要写盘）
        gpath = os.path.join(tmp, "gen.py")
        with io.open(gpath, "w", encoding="utf-8", newline="\n") as f:
            f.write(src2)

        r = subprocess.run([PY, gpath], capture_output=True, timeout=600, cwd=ROOT)
        so = (r.stdout or b"").decode("utf-8", "replace")
        se = (r.stderr or b"").decode("utf-8", "replace")

        produced = set(x for x in os.listdir(ws) if x.endswith(".yaml"))
        ao_prod = set(x for x in os.listdir(ao) if x.endswith(".yaml"))

        if not produced:
            return None, ("!! 生成器没写出任何档。rc=%d\nstdout 尾:%s\nstderr 尾:%s"
                          % (r.returncode, so[-500:], se[-500:]))
        if produced != ao_prod:
            return None, "!! WS 与 AO 写出的档不一致（%d vs %d）" % (len(produced), len(ao_prod))
        return produced, None
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def main():
    with open(GEN, "rb") as f:
        raw0 = f.read()

    log("== 变异检验 v2：FN_PREFIX 防护（让生成器本人说话）==")
    log("")
    log("判据：把生成器复制到临时目录真跑一遍，它写出的 8 个暗面档文件名里，")
    log("      还有几个是旧前缀。")
    log("")

    results = []

    def probe(tag):
        produced, err = run_gen_in_tmp(raw0 if tag == "对照" else mutated, tag)
        return produced, err

    # ---------- 对照 ----------
    produced, err = run_gen_in_tmp(raw0, "base")
    if err:
        log("[对照] " + err)
        log("")
        log("结论：!! 探针自身跑不通，检验作废（不是防护有问题，是探针有问题）")
        with io.open(REPORT, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(lines) + "\n")
        print("\n".join(lines))
        return 2
    hit = sorted(n for n in produced if n in OLD_NAMES)
    log("[对照] 未变异")
    log("   写出档数 = %d ；其中旧前缀名 = %d  %s" % (
        len(produced), len(hit), "OK（预期 0）" if len(hit) == 0 else "!! 应为 0"))
    results.append(("对照：不改动", len(hit), 0))

    # ---------- 变异 A ----------
    mutated = raw0.replace(
        b'fn = FN_PREFIX.get(doc["slug"], doc["slug"]) + ".yaml"',
        b'fn = doc["slug"] + ".yaml"')
    if mutated == raw0:
        log("")
        log("[变异 A] !! 字符串未命中，变异作废")
        results.append(("A：还原成 slug", -1, 8))
    else:
        produced, err = run_gen_in_tmp(mutated, "mutA")
        log("")
        log("[变异 A] fn 还原成 doc[\"slug\"] + \".yaml\"")
        if err:
            log("   " + err)
            results.append(("A：还原成 slug", -1, 8))
        else:
            hit = sorted(n for n in produced if n in OLD_NAMES)
            log("   写出档数 = %d ；其中旧前缀名 = %d  %s" % (
                len(produced), len(hit), "OK（打回红）" if len(hit) == 8 else "!! 应为 8"))
            if hit:
                log("   旧名清单：%s" % ", ".join(hit))
            results.append(("A：还原成 slug", len(hit), 8))

    # ---------- 变异 B ----------
    mutated = raw0.replace(b'"town-alleys":        "underworld-alleys",\n', b"")
    if mutated == raw0:
        log("")
        log("[变异 B] !! 字符串未命中，变异作废")
        results.append(("B：表删一项", -1, 1))
    else:
        produced, err = run_gen_in_tmp(mutated, "mutB")
        log("")
        log("[变异 B] FN_PREFIX 表里删掉 town-alleys 一项")
        if err:
            log("   " + err)
            results.append(("B：表删一项", -1, 1))
        else:
            hit = sorted(n for n in produced if n in OLD_NAMES)
            log("   写出档数 = %d ；其中旧前缀名 = %d  %s" % (
                len(produced), len(hit), "OK（打回红）" if len(hit) == 1 else "!! 应为 1"))
            if hit:
                log("   旧名清单：%s" % ", ".join(hit))
            results.append(("B：表删一项", len(hit), 1))

    # ---------- 源文件未被动过 ----------
    with open(GEN, "rb") as f:
        raw1 = f.read()
    ok_restore = (raw1 == raw0)
    log("")
    log("[自校验] 源生成器文件按字节未被动过 %s" % ("OK" if ok_restore else "!! 被改了"))

    log("")
    log("===== 汇总 =====")
    allok = ok_restore
    for tag, got, want in results:
        good = (got == want)
        allok = allok and good
        log("  %-18s 实测 %d / 期望 %d   %s" % (tag, got, want, "OK" if good else "!!"))
    log("")
    log("结论：%s" % ("三条全部符合预期，防护有效" if allok else "!! 有不符合项"))

    with io.open(REPORT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    return 0 if allok else 1


if __name__ == "__main__":
    raise SystemExit(main())
