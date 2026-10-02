# 第 7 道门（8 场景复读自检）全语料结果 — 2026-10-02

本文件记录 AWAKE 角色卡产线**第 7 道门**在 355 张卡全语料上的首次完整跑批结果，以及跑批暴露的 9 张红卡与修法。

## 一、这道门是什么

官方六道门禁链（`1-schema` / `2-affiliations` / `3-text` / `4-enhancement` / `5-provenance` / `6-compile`）全部是**静态**检查：读卡、比契约、编译 DSL。它们看不见「这张卡被真模型用起来会不会变成复读机」。

第 7 道门补的就是这一段。装置：

- **真实 DSL**：`tools/worldbook-runtime-sim/persona_scenario8_focused.ps1` 把目标卡复制到临时目录 → 调 `materialize-definitions.ps1` 只物化这几张 → 调 `PersonaDialogueSim persona <defsDir> <registry> <heroId> <dslPath> 8000 --force-approved`。
- **真实提示词模板**：`src/Prompts/NpcPromptTemplate.cs`（模拟器内部引用）。
- **本机 Ollama**：`http://127.0.0.1:11434/api/chat`，模型 `qwen2.5:latest`。
- **8 个固定场景**，每卡各跑一次、独立单轮空历史：`1_普通闲聊` / `2_利益交换` / `3_玩家履约` / `4_玩家失约` / `5_公开场合` / `6_私下场合` / `7_资源压力` / `8_核心价值挑战`。
- **判据**：`checks.anyReplyRepeatedSlogan`。逐对答复算 LCS 覆盖率，超过 `overlapThreshold=0.30` 且**跨两个场景**命中同一段文本 ⇒ `anyReplyRepeatedSlogan=true`。`status` 只有三种：`pass`（8/8 可用且无复读）/ `repetition_detected` / `in_doubt`。

与 `docs/PERSONA-SPOTCHECK-11-20260914.md` 那套 11 张点测装置同源，但场景是固定的、可铺满全语料，不需要逐卡手写探针。

## 二、怎么跑

```powershell
# 1. 起 Ollama（一次即可）
Start-Process -FilePath 'C:\Users\26811\AppData\Local\Programs\Ollama\ollama.exe' -ArgumentList 'serve' -WindowStyle Hidden

# 2. 沙箱/权限前提：TEMP 必须落在可写目录
$env:TEMP = $env:TMP = 'D:\AWAKE-Dev\.tmp-scenario8'

# 3. 单卡或一小批（-Cards 必须是【单个逗号连接】的参数，不能写数组）
& powershell -NoProfile -ExecutionPolicy Bypass `
  -File 'D:\AWAKE-Dev\AWAKE\tools\worldbook-runtime-sim\persona_scenario8_focused.ps1' `
  -Cards 锁合台,阿尔忒诺斯
```

全语料跑批用包装脚本每批 5 张，逐块把结果行追加到日志；355 张约 2 小时（≈20 秒/张）。

**读数在 stdout**：每张一行 `lord_XXXX pass hero=<中文名> usable=8/8 CLEAN`，外加 `PROGRESS done=N/355 pass=.. fail=..` 与末尾 `FINAL pass=.. fail=.. total=355`。

每张另有一份 JSON 报告 `D:\AWAKE-Dev\.tmp-scenario8\<heroId>-scenario8.json`，**UTF-8 带 BOM**，Python 读必须 `encoding="utf-8-sig"`，否则 `JSONDecodeError: Unexpected UTF-8 BOM`。

## 三、结果

### 3.1 跑批读数

```
FINAL pass=346 fail=9 total=355
```

9 张红卡全部是 `repetition_detected`，全部 `usable=8/8`（答复本身都可用，问题只在复读）。

### 3.2 修复后读数

9 张逐张改完后**逐张重测**（每张 `usable=8/8 CLEAN`），再对全部 355 份报告做一次终局扫描：

```
REPORTS=355
allUsable                355
anyReplyRepeatedSlogan     0
anyError                   0
dslGenerated             355
```

⇒ **第 7 道门在 355 张卡上零复读。**

### 3.3 9 张红卡与修法

| heroId | 中文名 | 复读来源 | 修法 |
|---|---|---|---|
| `lord_1_62` | 塞亚戎 | `selfClaimExamples[0]` 整句「革耳塞戈斯堡的攻城梯我量过，比旧的高三尺。三尺，就是少死二十个人的距离。」 | 改写成平铺处境叙事（带工匠重量、先填沟再上梯、自己这边少了十七个） |
| `lord_4_23_2` | 安丝特鲁达 | `selfClaimExamples[0]`「我驯那匹烈马用了四个月…」 | 换成「有人牵三岁生马来求我」的处境；同时把 `realSelfBehaviors[1]` 拆掉与 `core` 的三重冗余 |
| `lord_1_1_14` | 提莉安娜 | `selfClaimExamples[1]`「密泽亚的渡钱…」与 `[2]`「这些年我没让他失望过一次」 | 两条都改成有具体动作的场景叙事，拆掉可整句背的引语 |
| `lord_1_1_10` | 洛达 | `selfClaimExamples[0]`「北边那段墙塌了七尺，我报上去，半个月没人理…」 | 改写；并把 `[1]` 里的格言「墙比人靠得住」换成具体动作 |
| `lord_1_44` | 涅摩斯 | `selfClaimExamples[0]` 整句「铁与木不会说谎。尺寸差一寸，城墙上就多死十个人。我说的话很短——省下的字可以用来量木头。」（`core` 里也有） | `[0]`/`[1]` 改写为具体战役与换栅木；`core` 里那两句格言一并改写成叙述 |
| `lord_1_27_3` | 瓦西利娅 | `selfClaimExamples[0]`「你今日欠谁一句话，明年这句话能替你开一扇门…」（`core` 里也有） | `[0]` 换成替小商户递话换商路的具体事；`core` 里那句改成「钱数完就完了，人不一样」 |
| `lord_5_10` | 科林 | `selfClaimExamples[2]`「遇上拿战功激我的，我当即起身：口说无凭。牵马，进林子…」 | `[1]`/`[2]` 都改成有过程、有结果的叙事 |
| `lord_6_19_1` | 锁合台 | `selfClaimExamples[4]` 结尾对仗「这门里的账，你若认，我等你到明年；你若赖，这碗茶我绝不再续。」 | 结尾回到平铺叙事；`[1]` 里独立成句的格言「抹账容易，抹掉话难」一并拆掉 |
| `lord_1_57` | 阿尔忒诺斯 | 同一件「洛泰商队」往事同时出现在 `core` 两段、`selfClaimExamples[0]`、`[1]` 与 `contradictionDescription` ⇒ 场景 5 与场景 8 都伸手去拿它 | `core` 第三段改成一般立场；`[0]`/`[1]` 换成两件**互不相同**的事（分冬衣、替老兵垒屋） |

### 3.4 两种根因（比 9 张卡本身更重要）

1. **可整句背的引语**（7 张）。例句一旦写成对仗、格言、座右铭，模型会把它当角色签名照抄进答复——这正是 skill `persona-authoring` 的 M1「反金句」要防的。判据是「≥10 字独立成句」。
2. **单一轶事垄断**（阿尔忒诺斯，也是最隐蔽的一种）。卡里只有一件像样的往事，却被 `core`、两条例句、`contradictionDescription` 反复讲述；模型没有第二件素材可用，只能在两个场景里讲同一件事。**修法不是删句子，是给角色第二、第三件可讲的事。**

## 四、非致命读数（记档，不判死）

- **`anyCommandInChatMode`**：355 张里命中 **1 张**（`lord_1_58` 塔叙诺耳，场景 `4_玩家失约`）。答复正文完全正常（「账目上清楚些，军资也得准时。迟半月，利息可就多了。」），只是模型在 JSON 信封里多带了一个 `command` 字段。该标记**不参与 `status` 判定**（脚本 `:214` 只看 usable 与 anyRepeat）。2440 次生成里出现 1 次（0.04%），判为模型侧噪声。
- **`anyExactSloganHit`**：17 张为 true，但其中 **0 张**被判 `repetition_detected`。⇒ **`exactSloganHit` 单独不构成红**，只有跨答复复读才判死。早前按 `exactSloganHit` 筛选会得出「10~17 张红卡」的假读数。

## 五、已知局限

1. **判定依赖本机 Ollama 与 `qwen2.5:latest`**。换模型、换温度、换量化版本都会改变读数。本文件所有数字只在「本机 + qwen2.5:latest + 脚本默认参数」下成立。
2. **`dslSha256` 不可跨宿主比较**。脚本 `:164` 用 `powershell`（PS 5.1）调物化器，而 `materialize-definitions.ps1:124` 的 `ConvertTo-Json` 在 PS 5.1 与 PS 7 下排版不同（冒号后两空格 vs 一空格）。这只影响临时目录里的**字节排版**，不影响解析后的内容与判定；但报告里的 `dslSha256` 是宿主相关的，**不能拿去和用 `pwsh` 跑出来的哈希对照**。（同一个陷阱也适用于正式物化：正式产物必须用 `pwsh` 跑，否则 355 个定义会整体重排。）
3. **8 个场景不是自然对话**。它们是固定的压力探针，覆盖不了长程记忆与多轮累积。
4. **`overlapThreshold=0.30` 是脚本默认值**，未经标定。改它会改变红卡数量。
5. **`repetition_detected` 只说明「复读」，不说明「复读的内容不好」**。有些角色本来就该反复强调同一件事；这道门判的是「模型没有别的可说」，属于表达贫乏，不是人格错误。

## 六、顺带发现的官方第 4 道门的洞（E2b 平凡满足）

修 锁合台 时读到 `tools/persona-workbench/tools/audit-character-enhancement.ps1`：

- `:57` `$hardlineWords` 与 `:60` `$condWords` **都含「宁可」**；
- `:107-121` `Test-WindowCooccur` 取的窗口 `$text.Substring(max(0,$p-$win), min(len-lo, hard.Length+2*$win))` **包含硬线词自身**；
- ⇒ 只要文本里出现「宁可」，那个「宁可」自己就充当了窗口内的条件词 ⇒ `$bound=true` ⇒ **E2b 被平凡满足**。

本意是「条件词（若/但凡/除非/一旦…）与底线词（绝不/不容/断然…）在 70 字内共现」。本文件涉及的 9 张卡都用了真条件词 + 真底线词（如 锁合台 的「若…不容」），没有依赖这个洞。

**未修**：`audit-character-enhancement.ps1` 属另一会话维护，且改词表会改变已过门 355 张卡的判定口径。仅记档。

## 七、复现命令（终局扫描）

```powershell
py -3 C:\Users\26811\AppData\Local\Temp\scan_reports.py
# 输出 C:\Users\26811\AppData\Local\Temp\scenario8_scan.txt
```

扫描器读 `D:\AWAKE-Dev\.tmp-scenario8\*-scenario8.json`（`encoding="utf-8-sig"`），汇总 `status` / `anyReplyRepeatedSlogan` / `anyExactSloganHit` / `anyCommandInChatMode`，并列出 `BAD`（`status != pass`）与 `WATCH`（`maxOverlapRatio >= 0.5` 但未判死）。
