# -*- coding: utf-8 -*-
"""把「设计初衷」这条口径写进记忆两件（MEMORY.md / 当日日志 / TOPIC-CODE）。

MEMORY.md 已顶满 3000 字 ⇒ 新增一条必须先压掉同等字数（本文件自己的维护约定）。
做法：行首/行内锚点定位，先压后加，最后断言不超限才写回。
"""
import io
import sys

MEM = r"D:\AWAKE-Dev\.workbuddy\memory"
TOPIC = MEM + r"\TOPIC-CODE.md"
DAILY = MEM + r"\2026-09-17.md"
LONG = MEM + r"\MEMORY.md"


def load(p):
    with io.open(p, "r", encoding="utf-8") as fh:
        return fh.read().split("\n")


def save(p, lines):
    with io.open(p, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines))


def cut(lines, old, new):
    hit = 0
    for i, ln in enumerate(lines):
        if old in ln:
            lines[i] = ln.replace(old, new)
            hit += 1
    if hit == 0:
        sys.exit("找不到片段（引号可能不同，需人工看）：%r" % old[:40])
    return hit


# ══════════════ 1. MEMORY.md：加"设计初衷"，先压两处腾字数 ══════════════
long_mem = load(LONG)
if any("设计初衷" in ln for ln in long_mem):
    print("MEMORY.md 已写过，跳过")
else:
    cut(long_mem,
        "**更别拿自造题集去改语料**（09-17 裁定：「废题别留着」「不让进本来就不应该成提示词」「好马被多个词条占据也是正常现象」⇒ 无唯一答案的题**直接删**、不为命中而给词条补题面词；多个答案都对＝**靶子任意**，标 `countInGate=false` 移出计数，**不许自己改判成命中**）",
        "**更别拿自造题集去改语料**（09-17 三条裁定 ⇒ 无唯一答案的题**直接删**、不为命中而给词条补题面词；多个答案都对＝**靶子任意**，标 `countInGate=false` 移出计数，**不许自己改判成命中**）")
    cut(long_mem,
        "机制与接口细则（文化→数值注入点 `AddModel<T>`／Worldbook 包形态三边约定／RAG 无中文分词／`AGENTS.md` 已过时）**见 `CROSSLINE.md` §4.4**。",
        "机制与接口细则（文化→数值注入点／包形态三边约定／RAG 无中文分词）**见 `CROSSLINE.md` §4.4**。")
    # 腾字数（本文件自己的约定：新增先压旧）
    for old, new in (
        ("（世界书／代码／UI／美术／角色卡）＋一条主控线", "＋一条主控线"),
        ("（见 `AWAKE/docs/RECONCILE-20260915.md`；仍有约 593 个未跟踪）",
         "（见 `RECONCILE-20260915.md`；约 593 个未跟踪）"),
        ("改历史产物走 `corrections_<date>`。过程脚本（36 个 `_` 前缀）也留档。",
         "改历史产物走 `corrections_<date>`。过程脚本（`_` 前缀）也留档。"),
        ("对象库被删事件（09-15）已修，救法见技能", "对象库被删事件救法见技能"),
        ("不是错误码也不是 null ⇒ `IsStorageKeyNotFound`", "（非错误码、非 null）⇒ `IsStorageKeyNotFound`"),
        ("**改题集必须同步两处验台的门禁常量，并做变异检验**。见 `DECISION-20260917` §7.10。",
         "改题集须同步两处门禁＋变异检验。"),
        ("**不得据以判断当前状态**。", "**不得据以判断状态**。"),
        ("`…\\Modules\\AWAKE\\PlayerExports\\AwakeState\\<campaignId|unbound>\\`（**非** ProgramData）。按 mtime 找最新 pid。",
         "`…\\Modules\\AWAKE\\PlayerExports\\AwakeState\\`（**非** ProgramData）。按 mtime 找最新 pid。"),
        ("是记录，**不是开工单**（记录 ≠ 立项）。", "是记录，**不是开工单**。"),
        ("⚠️ 用例间有共享状态 ⇒ 失败会级联，**先看序号最小的那条**。",
         "⚠️ 用例间共享状态 ⇒ 失败级联，**先看序号最小那条**。"),
        ("后**同步显式编译清单**（显式 `Compile`／`<Reference>`）", "后**同步显式编译清单**（`Compile`／`<Reference>`）"),
        ("别把主控线的口径当成自己那一线的", "别把主控线口径当自己那一线的"),
    ):
        cut(long_mem, old, new)

    NEW = ("- **🎯 设计初衷＝用 AI 模拟真实知识传播与对话**（09-17 甲方重申）：判据是"
           "「**谁知道**（身份·阶层·专业·地域·亲历·时代·关系）×**何时知道**（玩家传授／NPC 转述／周报事件）"
           "×**信不信**（来源与证据）」，**不是检索命中率**；**答不出常是正确**（`not_found` ≠ 病）。"
           "见 `docs/DECISION-20260917-设计初衷-知识传播与对话.md`。")
    idx = [i for i, ln in enumerate(long_mem) if ln.startswith("## 四、坐标与口径")]
    if len(idx) != 1:
        sys.exit("MEMORY.md 找不到 §四 标题")
    long_mem[idx[0] + 1:idx[0] + 1] = [NEW]

    size = len("\n".join(long_mem))
    print("MEMORY.md 新字数 = %d（限 3000）" % size)
    if size > 3000:
        sys.exit("仍超限 %d 字，未写回" % (size - 3000))
    save(LONG, long_mem)
    print("MEMORY.md 已更新")

# ══════════════ 2. 当日日志 ══════════════
daily = load(DAILY)
if any("设计初衷" in ln for ln in daily):
    print("当日日志已写过，跳过")
else:
    if daily and daily[-1].strip() != "":
        daily.append("")
    daily.extend("""
## 19. 甲方重申设计初衷：知识传播与对话（09-17 早）

**原话**：「我要你用AI模拟真实知识传播路径和对话，这是我希望达到的效果，你不能为了达成计划和设计就忘了我的设计初衷啊」

- 查过：**这条初衷早就写在册**，不是新方向 —— `MARCUS-AWAKE-CAPABILITY-CARRYOVER-MATRIX-v1-20260824.md` §9
  「AWAKE 专属适配层」第 1／3／4 条（**NPC 知识权限** / **知识传播结算** / **NPC 对话编排**）、
  `checkpoints/AWAKE-BIG-DIRECTION-20260826-checkpoint.md` §9 讨论顺序第 1–3 条（**听到→相信→行动** /
  玩家传授错误知识 / 玩家知识向 NPC 传播纠错）、`FUNCTION-GENEALOGY-FULL-20260915.md` §3.3（**称号传播路径**）。
- **我偏在哪（点名，免得再犯）**：① 为了让"身份过滤"不影响检索数字，**特意让每条题用目标自己的身份**，
  还写下"身份过滤实际是空操作" ⇒ **把"谁知道"这一层从量尺上抹掉了**；② 把真机那条 `not_found` 记成
  "召回的病" ⇒ 按第 1 条口径，**答不出来往往正是正确行为**；③ §7.10 收口的"下一步"里**通篇没有"传播"两字**。
- **正确的三层**：谁知道（七维，含最被忽略的**亲历性**：听来的 ≠ 亲眼见的）／什么时候知道（传播结算：
  玩家传授、NPC 转述、周报事件；一条消息有**出生点·路径·时延·走样**）／信不信（来源与证据）。
  **对话是这三层的出口**，检索只是"候选从哪儿取"的一段。
- **对现役工作**：字面／语义／门槛／合并／语料拼法**不废**，但数字从"成败标准"降为"**取数段的健康度**"；
  真正的验收要能回答「**这个人该不该知道这条，以及他听来的版本对不对**」。
- **题集**：现有 24 条只测"问得出／问不出"，**不含"该不该知道"** ⇒ 下一步要补"问一件他不该知道的事，
  系统应说不知道"这一类（今天一条都没有）。

**文档**：新建 `docs/DECISION-20260917-设计初衷-知识传播与对话.md`（原话一字不改＋在册出处＋偏在哪＋三层＋两个待拍板岔口）；
`DECISION-20260917-两条通道怎么合.md` 顶部加了指向它的必读横幅。
**待甲方拍板**：① 传播**按人算还是按地算**（我的看法：有名字的按人、无名群众按地批量）；
② 传播**由时间驱动还是事件驱动**（我的看法：事件为主、时间为底线，否则人人迟早全知道，七维空转）。**未提交。**
""".strip("\n").split("\n"))
    save(DAILY, daily)
    print("当日日志已更新")

# ══════════════ 3. TOPIC-CODE：在语义链那一节开头挂指针 ══════════════
topic = load(TOPIC)
if any("设计初衷" in ln for ln in topic):
    print("TOPIC-CODE 已写过，跳过")
else:
    idx = [i for i, ln in enumerate(topic) if ln.startswith("## 本地语义层 · 接进检索链")]
    if len(idx) != 1:
        sys.exit("TOPIC-CODE 找不到语义链标题（命中 %d）" % len(idx))
    NOTE = ["> ⚠️ **先记住这件事的初衷（甲方 09-17 重申）**：**用 AI 模拟真实知识传播路径与对话** —— "
            "判据是「谁知道 × 何时知道 × 信不信」，**不是**本节的命中率。本节那些数字是「**取数段的健康度**」；"
            "**答不出常是正确行为**。见 `docs/DECISION-20260917-设计初衷-知识传播与对话.md`。", ""]
    topic[idx[0] + 1:idx[0] + 1] = NOTE
    save(TOPIC, topic)
    print("TOPIC-CODE 已更新")

print("全部完成")
