# -*- coding: utf-8 -*-
"""暗面批前缀归一 · 第四段：改 `doc` id 首段（Б 案：文件名＋doc id 首段）。

依据（照 09-14 流程，不照我自己想的）：
  `docs/worldbook-migration/corrections_20260914/CLASSIFICATION-V2-20260914.md`
  §2 第 3 条「前缀复数化（414 档改名）」：
    · 范围＝**B 案**（用户选）：**文件名 ＋ `doc` id 首段**用新前缀；
      `assertion`／`expr` id 起始段**保留历史单数形态**，不跟着改。
    · 做法：两镜像目录同步；改完**同批重登记**；再 select→approve→proof→compile。
  §2 第 4 条的教训（裸字符串全局替换越界）—— 所以本次**只改第 3 行的 `doc.` id**，
  绝不做全文替换，`assertion`/`expr` 行一律不碰。

★ 关键认知（先前搞错的地方）：
  `doc.<域>.<slug>` 的第三段**就是**条目 id `awake:entry:<域>.<slug>` 的 slug，
  两者同一个东西 ⇒ **改 doc id 首段 = 条目 id 跟着变**，这是规范要求的形态，不是事故。
  （09-14 实证：`awake:entry:culture.tale-charas-origin` → `...culture.tales-charas-origins`）

★ 只改「第三段的 slug」，**不改域段**（politics / economy）。
"""
import hashlib
import io
import os
import shutil

ROOT = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1", "authoring")
AO = os.path.join(ROOT, "docs", "worldbook-migration", "projection", "authoring-out")
ARCH = os.path.join(ROOT, "tools", "_archive-underworld-idfix-20260924")
REPORT = os.path.join(ROOT, "tools", "_underworld_idfix_20260924.txt")

# 档名 -> (域, 旧 slug, 新 slug)
TABLE = [
    ("underworld-alleys.yaml",        "politics", "town-alleys",        "underworld-alleys"),
    ("underworld-gang-leaders.yaml",  "politics", "alley-gang-leaders", "underworld-gang-leaders"),
    ("underworld-struggle.yaml",      "politics", "alley-struggle",     "underworld-struggle"),
    ("underworld-gangs.yaml",         "politics", "town-gangs",         "underworld-gangs"),
    ("underworld-crime-rating.yaml",  "politics", "crime-rating",       "underworld-crime-rating"),
    ("underworld-blood-money.yaml",   "politics", "blood-money",        "underworld-blood-money"),
    ("underworld-bandits.yaml",       "politics", "bandits",            "underworld-bandits"),
    ("underworld-smuggling.yaml",     "economy",  "smuggling",          "underworld-smuggling"),
]

lines = []


def log(s):
    lines.append(s)


def sha256(p):
    with open(p, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest().upper()


def fix_text(txt, dom, old, new):
    """只动 id: 那一行（档内唯一以 `id: doc.` 开头的行）。
    返回 (新文本, 改了没, 说明)。"""
    old_line = "id: doc.%s.%s" % (dom, old)
    new_line = "id: doc.%s.%s" % (dom, new)
    out = []
    n = 0
    for ln in txt.split("\n"):
        if ln == old_line:
            out.append(new_line)
            n += 1
        else:
            out.append(ln)
    return "\n".join(out), n == 1, "旧行=%r 命中 %d 次" % (old_line, n)


def main():
    log("== 暗面批 · 第四段：doc id 首段归一（B 案）==")
    log("")
    log("只改每档第 3 行的 `id: doc.<域>.<slug>` —— 与档名同步。")
    log("`assertion.*` / `expr.*` 子 id 一律**保留旧形态**（B 案明文）。")
    log("")

    if not os.path.isdir(ARCH):
        os.makedirs(ARCH)

    ok = True
    for fn, dom, old, new in TABLE:
        log("### %s" % fn)
        for label, d in (("WS", WS), ("AO", AO)):
            p = os.path.join(d, fn)
            if not os.path.exists(p):
                log("   !! %s 缺档" % label)
                ok = False
                continue
            raw = open(p, "rb").read()
            crlf = raw.count(b"\r\n")
            txt = raw.decode("utf-8")
            after, changed, why = fix_text(txt, dom, old, new)
            if not changed:
                log("   !! %s %s" % (label, why))
                ok = False
                continue
            # 先备份（按字节，首次见到时）
            bkp = os.path.join(ARCH, label + "-" + fn)
            if not os.path.exists(bkp):
                with open(bkp, "wb") as f:
                    f.write(raw)
            # 写回：保持原行尾风格（09-14 教训：别把 CRLF 搞坏）
            data = after.encode("utf-8")
            with open(p, "wb") as f:
                f.write(data)
            # 复核：doc id 已换、assertion/expr 未换、行数不变
            t2 = io.open(p, encoding="utf-8").read()
            good_doc = ("id: doc.%s.%s" % (dom, new)) in t2
            stale_doc = ("id: doc.%s.%s" % (dom, old)) in t2
            sub_ok = ("assertion.%s-1" % old) in t2 and ("expr.%s-rumor" % old) in t2
            n_before = len(txt.split("\n"))
            n_after = len(t2.split("\n"))
            tag = "OK" if (good_doc and not stale_doc and sub_ok and n_before == n_after) else "!!"
            log("   %s  doc→%s  旧doc残留=%s  子id保留=%s  行数 %d→%d  %s" % (
                label, new, "有" if stale_doc else "无", "是" if sub_ok else "否",
                n_before, n_after, tag))
            if tag == "!!":
                ok = False

    log("")
    log("备份目录：%s（8 档 × 2 目录）" % ARCH)
    log("")
    log("结论：%s" % ("doc id 首段已与档名同步，子 id 保留旧形态" if ok else "!! 有不符合项"))

    with io.open(REPORT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
