# -*- coding: utf-8 -*-
"""修复 TOPIC-CODE.md：09-18 11:47 的 shell 重定向（`cat >> ... << EOF`）把文件**从头覆盖**了约 19 行。

⚠️ 本脚本**已于 09-18 11:5x 执行过一次并成功**（1147 → 1167 行，头部已恢复、新节已归末尾）。
   再跑会命中"已修复"分支并直接退出 —— 幂等，不会二次改动。

修法：① 被覆盖的头部（标题头 ＋ `## 构建与验证` ＋ 5 条 bullet）按 09-17 归档补回；
      ② 我那节《应当联系判定》从头部挪到末尾；③ 丢掉切在字节中间的残片行 `**。`。

★ 事故教训（已写进 2026-09-18.md）：**往记忆/日志这类中文大文件写内容，一律走 Edit/Write 工具，
  不要用 shell 重定向** —— 这是 09-12 就立下的铁律，本次是"知而不行"的又一次。
  症状不是乱码，是**从头覆盖**：因为写入落在 offset 0，按字节切断了行。
"""
import io, os, hashlib

CUR = r"D:\AWAKE-Dev\.workbuddy\memory\TOPIC-CODE.md"
OLD = r"D:\AWAKE-Dev\.workbuddy\memory\_archive-20260917\TOPIC-CODE.md"


def rd(p):
    return io.open(p, encoding="utf-8").read().split("\n")


cur = rd(CUR)

# ---- 幂等检测：已修好就退出 ----
if cur[0].startswith("# 模组主体（代码层）"):
    bad = [i + 1 for i, l in enumerate(cur) if l.strip() == "**。"]
    print("检测到已修复（首行＝文件标题）。残片行：%s" % (bad or "无"))
    assert not bad, "仍存在残片行，需人工处理"
    assert any("提及边 · 细则" in l for l in cur[-45:]), "新节不在末尾"
    print("核验通过，无需改动。")
    raise SystemExit(0)

old = rd(OLD)

# ---- 首次修复：前置自检 ----
assert cur[20].startswith("- **复核**") and "_link_audit" in cur[20], "第21行不是新节末行：%r" % cur[20][:60]
assert cur[21].strip() == "**。", "第22行不是残片：%r" % cur[21]
assert old[0].startswith("# 模组主体（代码层）"), "归档首行不对：%r" % old[0]
assert "CS1069" in old[17], "归档第18行不含 CS1069：%r" % old[17][:60]
print("自检 4/4 通过")

newsec = cur[0:21]      # 我那节新内容
head = old[0:18]        # 恢复的头部
tail = cur[22:]         # 当前第 23 行起（含"症状很有误导性…"续行）
before = len(cur)
io.open(CUR, "w", encoding="utf-8", newline="\n").write("\n".join(head + tail + ["", "---", ""] + newsec))

# ---- 写回后复核（断言要对准"接缝"，不是对准新节位置）----
chk = rd(CUR)
seam = next(i for i, l in enumerate(chk) if "CS1069" in l)
ok1 = chk[0].startswith("# 模组主体（代码层）")
ok2 = any("提及边 · 细则" in l for l in chk[-45:])
ok3 = not any(l.strip() == "**。" for l in chk)
ok4 = "症状很有误导性" in chk[seam + 1] and "⇒ **凡是新增" in chk[seam + 2]
print("复核：首行=%s 新节末尾=%s 残片清=%s 接缝正确=%s" % (ok1, ok2, ok3, ok4))
print("行数 %d → %d（差 %+d）" % (before, len(chk), len(chk) - before))
assert ok1 and ok2 and ok3 and ok4, "复核不过"
print("OK")
