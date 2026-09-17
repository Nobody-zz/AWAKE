# -*- coding: utf-8 -*-
"""
AWAKE「空知识格」三档写法对照（2026-09-18）

用场：拆开「知道」与「开口」之后，not_found 轮次会把一个**空的知识格**交给模型。
本探针不改产品代码，只把同一条提示词里的知识格换成三种写法，各让本机模型答一次，
看它会不会编 —— 为「模板里那句该写成什么样」提供实测依据，不靠推测。

输入：AWAKE.Tests 的 knowledge-gate-prompt 用例落盘的真实渲染结果
      （真策略 WorldKnowledgeDecisionPolicy + 真模板 NpcPromptTemplate + 真渲染 RenderTemplate）。
      先跑 dotnet run --project AWAKE.Tests，用例会打印落盘路径。

用法：
  python _ollama_knowledge_gap_probe_20260918.py [提示词txt路径] [模型名] [每档采样次数]

为什么每档要采样多次：temperature=0.7 下同一条提示词会给出不同回答，"编不编"是概率性的，
单次采样区分不了"从不编"和"偶尔编"。默认每档 3 次，报的是"3 次里编了几次"。

读法：
  A 是现状。逐条比对各档回答里有没有冒出世界书里的具体事实（人名、地名、物产、战事、数字）
  ——有就是编；说不知道／含糊带过，就是没编。

为什么还要跑"信封"那一轮（2026-09-18 补）：
  产品真发给模型的**不是裸模板**。NpcDialogueService.cs:305 调网关时 includeContext=true，
  网关 AiTaskGateway.cs:386-492 会把输入包成 {"task":"<整段模板>","context":[…]}
  （Formatting.None ⇒ 模板里的换行变成字面的 \n）。只有 _host.Context 为空或规划失败才回落到裸输入。
  ⇒ 上面那轮"裸模板"是仓里既有口径（worldbook-runtime-sim/e2e_ollama.py:43 也是这么发的），
    但它不等于真机形态。信封那一轮补这个落差。
  ⚠️ 仍存的口径差：本档 context 用空数组；真机里可能带贡献项。离线无法确证。

不启动游戏、不联网（只打本机 Ollama）、不动 src/。
"""
import json
import os
import sys
import time
import urllib.request

TEMP = os.environ.get("TEMP", r"C:\Users\26811\AppData\Local\Temp")
PROMPT_PATH = sys.argv[1] if len(sys.argv) > 1 else os.path.join(TEMP, "awake-knowledge-gate-prompt.txt")
MODEL = sys.argv[2] if len(sys.argv) > 2 else "qwen2.5:latest"
REPEATS = int(sys.argv[3]) if len(sys.argv) > 3 else 3
OLLAMA = "http://127.0.0.1:11434/api/chat"

TAG_A = "【检索到的知识】"
TAG_B = "【跨会话记忆】"

# A = 今天的真实形态：知识格被渲染成 JSON 空串 ""（实测，见 AWAKE.Tests 的 PROBE 行）。
# B / C 是候选写法，用来问一句：光"告知它空着"够不够，还是必须写成禁令。
VARIANTS = [
    ("A 原样（今天的真实形态）", '""'),
    ("B 告知式", '""\n（本轮没有任何世界书知识）'),
    (
        "C 禁令式",
        '""\n这一格空着，表示你确实不知道这件事。'
        '你可以承认不清楚、可以说只是听人讲起过、也可以反问；'
        '但不得编造世界书里的具体事实（人名、地名、物产、战事）。',
    ),
]

# ★ 阳性对照（变异检验）：不是候选写法，是**探针自检**。
# 如果连"知识格里放一条真事实"都不能让模型照着说，那 A/B/C 三档全编就只说明探针太钝，
# 说明不了"告知/禁令无效"。形态照 BuildPromptBlock(WorldKnowledgeModels.cs:269-275) 写。
CONTROL = (
    "D 阳性对照（真知识，用来证明探针有分辨力）",
    "知识状态：known\n"
    "知识来源：probe\n"
    "身份：probe\n"
    "知识范围：probe\n"
    "允许详细度：probe\n"
    "命中档案：none\n"
    "知识正文：\n"
    "（探针注入）今年本村遭了蝗灾，麦子歉收，村里正在减税。",
)


def load_prompt():
    if not os.path.exists(PROMPT_PATH):
        sys.exit("找不到提示词落盘文件：%s\n先跑 dotnet run --project AWAKE.Tests"
                 "（用例 knowledge-gate-prompt 会落盘）" % PROMPT_PATH)
    text = open(PROMPT_PATH, encoding="utf-8").read()
    for tag in (TAG_A, TAG_B):
        if tag not in text:
            sys.exit("提示词里找不到分段标签：%s —— 模板可能改过，本探针的切法要跟着改" % tag)
    return text


def with_knowledge_block(prompt, block):
    i = prompt.index(TAG_A)
    j = prompt.index(TAG_B)
    return prompt[:i] + TAG_A + "\n" + block + "\n" + prompt[j:]


def call_ollama(prompt, timeout=300):
    body = json.dumps({
        "model": MODEL,
        "messages": [{"role": "user", "content": prompt}],
        "stream": False,
        "options": {"temperature": 0.7, "num_predict": 600},
    }, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(OLLAMA, data=body, headers={"Content-Type": "application/json"})
    t0 = time.time()
    with urllib.request.urlopen(req, timeout=timeout) as r:
        data = json.loads(r.read().decode("utf-8"))
    return data.get("message", {}).get("content", ""), time.time() - t0


def extract_reply(raw):
    """模型被要求只输出 JSON。能解出来就把 reply 字段拎出来，解不出就原样返回。"""
    text = (raw or "").strip()
    if text.startswith("```"):
        text = text.strip("`")
        if "\n" in text:
            text = text.split("\n", 1)[1]
    try:
        obj = json.loads(text)
        if isinstance(obj, dict) and isinstance(obj.get("reply"), str):
            return obj["reply"], obj.get("mood", "")
    except Exception:
        pass
    return raw, ""


def wrap_envelope(text):
    """复刻 AiTaskGateway.cs:459-492 的信封形态（Formatting.None ⇒ 换行转义成 \\n）。"""
    return json.dumps({"task": text, "context": []}, ensure_ascii=False, separators=(",", ":"))


# ★ 第三轮：只动一个字面上的冲突，不动知识格。
# 模板 NpcPromptTemplate.cs:59 要求「字数80到180」，同一份模板 :61 又要求「资料为空或没有提到时，要承认不知道」。
# 一句"这件事我没听说过"只有 8 个字 —— **两条要求互相打架**，模型为了凑长度只能编。
# 本轮把下限放开，其余一字不改（知识格保持 A 原样），看弃权会不会自己出现。
LENGTH_FLOOR_OLD = "字数80到180，"
LENGTH_FLOOR_NEW = "字数不限，"


def relax_length_floor(prompt):
    if LENGTH_FLOOR_OLD not in prompt:
        sys.exit("模板里找不到「%s」—— 模板可能改过，本轮的替换点要跟着改" % LENGTH_FLOOR_OLD)
    return prompt.replace(LENGTH_FLOOR_OLD, LENGTH_FLOOR_NEW, 1)


# ★ 第四轮：不塞说明句，改成**给模型一个看得见的状态**。
# 依据：`RenderTemplate`（NpcDialoguePromptPipeline.cs:122-132）只有 {{key}} -> JSON 字面量，
# **没有条件分支** ⇒ 说明句一旦写进模板，known/partial 轮次也会出现，那时候格子有内容，
# 旁边一句"这一格空着"就自相矛盾。所以"只在空着时说"这件事，模板层做不到。
# 能做的是改 BuildPromptBlock 的返回（它已经给 known 轮次返回「知识状态：known」开头的一段）。
# E/F 就是这个方向的两种候选，都没有 `""`。
ROUND4 = [
    (
        "E 只给状态行（形态对齐 known 轮次）",
        "知识状态：not_found\n知识正文：（无）",
    ),
    (
        "F 状态行 + 短指令",
        "知识状态：not_found\n知识正文：（无）\n"
        "（没有检索到与本问题相关的资料。不知道就直说不知道，不要替它编。）",
    ),
]


def sample(label, variant, repeats):
    """对一条已拼好的输入采样 repeats 次，打印回答。返回成功次数。"""
    ok = 0
    for k in range(repeats):
        try:
            raw, dt = call_ollama(variant)
        except Exception as e:
            print("  [%d] 调用失败：%r" % (k + 1, e))
            continue
        ok += 1
        reply, mood = extract_reply(raw)
        print("  [%d] %.1fs  mood=%s" % (k + 1, dt, mood or "—"))
        print("      " + (reply or "").strip().replace("\n", "\n      ")[:900])
    print("  —— 本档成功 %d/%d" % (ok, repeats))
    print("-" * 74)
    return ok


def main():
    prompt = load_prompt()
    print("模型 = %s（本机 Ollama %s）" % (MODEL, OLLAMA))
    print("提示词 = %s（%d 字符，来自真渲染，不是手抄）" % (PROMPT_PATH, len(prompt)))
    print("被测问题 = 「今年收成怎么样？」（世界书里没有这一条 ⇒ not_found）")
    print("每档采样 %d 次（temperature=0.7）" % REPEATS)
    print("=" * 74)
    for wrapped in (False, True):
        print("\n" + "#" * 74)
        if wrapped:
            print("# 信封轮：输入 = %s" % wrap_envelope("<整段模板>")[:120])
            print("#   —— 对齐 AiTaskGateway.cs:459-492，模板换行已被转义成字面 \\n")
        else:
            print("# 裸模板轮：输入 = 整段模板原文")
            print("#   —— 对齐仓里既有口径（worldbook-runtime-sim/e2e_ollama.py:43）")
        print("#" * 74)
        for label, block in VARIANTS + [CONTROL]:
            variant = with_knowledge_block(prompt, block)
            if wrapped:
                variant = wrap_envelope(variant)
            print("\n【%s】知识格写成：" % label)
            for line in block.split("\n"):
                print("    | " + line)
            sample(label, variant, REPEATS)

    print("\n" + "#" * 74)
    print("# 第三轮：只放开字数下限（知识格保持 A 原样）")
    print("#   「%s」 -> 「%s」" % (LENGTH_FLOOR_OLD, LENGTH_FLOOR_NEW))
    print("#   —— 模板 :59 要求字数下限，:61 又要求「没资料就承认不知道」，两者打架")
    print("#" * 74)
    relaxed = relax_length_floor(prompt)
    for label, block in [VARIANTS[0]] + [CONTROL]:
        variant = with_knowledge_block(relaxed, block)
        print("\n【%s】知识格写成：" % label)
        for line in block.split("\n"):
            print("    | " + line)
        sample(label, variant, REPEATS)

    print("\n" + "#" * 74)
    print("# 第四轮：知识格换成「看得见的状态」，不再留字面的 \"\"（两种候选 E/F）")
    print("#   —— 这一轮不是加说明句，是改 BuildPromptBlock 该返回什么")
    print("#" * 74)
    for label, block in ROUND4:
        variant = with_knowledge_block(prompt, block)
        print("\n【%s】知识格写成：" % label)
        for line in block.split("\n"):
            print("    | " + line)
        sample(label, variant, REPEATS)

    print("\n读法：A 是现状。逐条比对 A/B/C 的回答里有没有冒出世界书里的具体事实")
    print("（人名、地名、物产、战事、数字、气象年景）——有就是编；说不知道／含糊带过，就是没编。")
    print("两轮（裸模板／信封）一起看：行为一致 ⇒ 信封不是这里面最要紧的变量。")
    print("D 是探针自检，不是候选写法：它的回答里**必须**出现「蝗灾／歉收／减税」这类注入词。")
    print("  出现了 ⇒ 探针有分辨力，A/B/C 全编是真结论。")
    print("  没出现 ⇒ 探针太钝，A/B/C 全编说明不了任何事，先修探针再谈结论。")
    print("第三轮看一件事：放开字数下限后，弃权（承认不知道）会不会自己出现。")
    print("注意：这是小样本（每档 %d 次），只报方向，不下定量结论。" % REPEATS)


if __name__ == "__main__":
    main()
