# REPORT — 三条诊断的验证 + 第 1/2 项修复执行记录

> 日期：2026-10-01　作者：巡检会话
> 性质：**验证报告 + 执行记录**。本文件记录实测结论与已落地的改动。
> 配套：`REVIEW-DESIGN-20261001.md`（设计评审）、`PLAN-REPAIR-WORLDBOOK-HASH-AND-FOUR-DEFECTS-20261001.md`（修复计划）
> 证据标注：**【实测】**＝本次亲自复现；**【审计】**＝代码/数据级审计（已核，本方未逐条复验）；**【未验】**＝需真机。

---

## 0. 起因

甲方提出三条判断：

1. 没有实际形成代码可用闭环
2. 世界知识体系一是没跑过，二是没写全
3. 角色卡质量参差不齐且规范存疑，AI 味道重、模板化现象严重

本文件是对这三条的独立验证，以及由此确定的修复顺序与执行结果。

---

## 1. 判决表

| 判断 | 判决 | 关键数字 |
|---|---|---|
| **1. 没形成代码可用闭环** | ✅ **证实**（措辞需改） | 12 个关节**断了 7 个**，且**各断各的** |
| **2a. 世界知识没跑过** | ✅ **证实，且更硬** | 最新真机加载 = **`entries=3`**（pilot）；calradia **从未加载**；**修复前也无法加载**（哈希拒绝）⇒ 800 条内容受众 = **0** |
| **2b. 世界知识没写全** | ✅ **部分证实** | 聚落 **393/393 = 100%**、氏族 74/82 = 90%；但**英雄 27/415 = 6.5%**；**738/800（92.3%）完全无条件**；war 缺 5–8×、culture 缺 10–20× |
| **3a. 模板化严重** | ✅ **证实，比原判断更精确** | 280/355 卡命中 ≥50 卡共用模板；247 卡共用同一句且**进运行时** |
| **3b. AI 味道重** | ⚠️ **用词层驳回** | 修辞模板 0.92‰；测的 20 个套路里 **9 个为 0** |
| **3c. 质量参差不齐** | ✅ **证实** | 叙事正文 196→1169 字（**6×**）；受管 735 字 vs 未跟踪 272 字 |
| **3d. 规范存疑** | ✅ **证实，分界线极干净** | 受管 76 卡 **0/76** 违规；未跟踪 279 卡 **279/279** 违规 |

### 1.1 闭环逐关节实测（唯一真机日志 `Modules\AWAKE\Logs\Awake.log`，1329 行）

| 环节 | 09-14 真机 | 判定 |
|---|---|---|
| Mod 加载 / 注册 | `register_ok` | ✅ |
| 会话生命周期 | `session_ready` → `drained success=True` | ✅ |
| AI 路由 / 出图 | `portrait_texture_ready` ×3 | ✅ |
| 命令治理 | 审计：无绕过 | ✅ |
| 运行时服务就绪 | `runtime_not_ready runtime=Created` | ⚠️ |
| 事件持久 | 事件确实 `persisted`，但后端是内存实现 | ⚠️ |
| 世界书（真内容） | 只有 3 条 pilot | ❌ |
| 世界书（人格） | `characters=0`（08-16 曾是 415） | ❌ |
| 存储绑定存档 | `unbound/` | ❌ |
| 原生探测 | `native_readiness Failed`（NRE） | ❌ |
| NPC 主动开口 | `npc_dialogue_open_failed` ×2 | ❌ |
| 周报 / 世界事实 | `root_corrupt` ×9，`count=0` | ❌ |

**12 个关节：3 通、2 降级、7 断。**
⇒ **修好任何一个，玩家侧都看不出变化**；必须同时修掉 4~5 个才有第一次可演示。这是"很多会话干完都没成果"的结构性原因。

**物证**：`PlayerExports\AwakeState\` 下同时存在 `1IgZ8yHJynfn\`（09-12，含 `awake.npc.memories` 等）与 `unbound\`（09-14）。⇒ 缺陷 4 不再是「推断，未证实」；09-12 写下的记忆被孤儿化。

### 1.2 世界知识的两个反直觉事实

- **唯一加载过的 v2 世界书是 3 条的 pilot 测试包**（09-11、09-14 各一次）。800 条的 `awake:worldbook.calradia` **一次都没进过游戏**。
- **三份 DEPLOY 报告自己写着「真机确认未做／待人工」**（`DEPLOY-V38:126`、`V39:152`、`V40:148`）——是路线图 §4.1 的「✅ 已投送」把这个 caveat 抹掉了。**漂移发生在汇总层，不在原始报告层。**

### 1.3 角色卡：两条纠正

- **「AI 味道」在词汇层不成立**：`某种意义上`／`换言之`／`归根结底`／`值得注意的是`／`与此同时`／`一方面…另一方面`／`与其…不如` 全部为 **0**。真正的"AI 味"是**结构同构**——不是整卡复制（句集 Jaccard ≥0.5 的近重复对 = **0**），而是「**每卡独特正文 + 共享样板句**」的批量填空，且集中在**边界字段**（`selfClaimRules`／`realSelfBehaviors`），而这两个字段**会进 prompt**。
  ⇒ 不能靠"重写正文"解决，必须改生成流程并加**跨卡骨架句门**。规范当前只管 `selfClaimExamples`，恰好在盲区。
- **规范不是没写好，是门禁够不着那 279 张卡**：`AUTHORING-GUIDELINES` **v4.3 已正确诊断并补齐**（W10 产线归属门禁、未注册 tag 改判死、补 tag 注册表校验），但 **v4.3 本身尚未提交**（HEAD 仍是 v4）。

### 1.4 一个新问题（任何文档都未记录）

**73/76 张运行时 `definition.json` 与源卡不一致，差异 100% 落在 `selfClaimExamples`**（唯一"决策样本"字段）。definition 全部物化于 **09-20 00:31**，受管卡最后修改于 **09-24**。
⇒ **运行时用的是比作者手上早 4 天的快照。改卡之后不重跑 `materialize-definitions.ps1`，改动进不了游戏。**

---

## 2. 三条是同一个病

| | 内容生产在哪 | 产物状态 | 门禁为何没拦住 |
|---|---|---|---|
| 世界书 | 权威源 `full-geo1/authoring/`（844 YAML）**被 gitignore** | 投送后 **16 天没进过游戏** | 哈希校验器口径错；`DEPLOY_VALIDATE_OK` 只比字符串 |
| 角色卡 | 279 张卡**未跟踪**，门禁够不着 | definition 比源卡**早 4 天** | 模板门只管 `selfClaimExamples`；v4.3 未提交 |
| 通用 | — | — | `SdkSmoke` 未挂进 `build.ps1`；`maf-lint` 永不失败 |

**共同结构**：**内容在门禁看不见的地方生产，产物是过期快照，而门禁测不出自己该测的东西。**

**角色卡审计实证了设计评审的总论**：**schema 是"代数"**（`additionalProperties:false` + 枚举 + pattern）→ 一校验抓出 **279/279**；**规范是"散文"**（M1–M9 讲"什么算好例子"）→ 门禁**一条也没抓住**。

---

## 3. 修复顺序（由依赖关系强制，非偏好）

| # | 动作 | 为何必须在此位置 | 状态 |
|---|---|---|---|
| **1** | 修世界书哈希那一行 | **"任何内容工作变成可观测"的前置条件**；不做它，再写 800 条受众仍是 0 | ✅ **已完成**（见 §4） |
| **2** | 把 `SdkSmoke` 挂进 `build.ps1` | 否则第 1 项修好下次照样回归 | ✅ **已完成**（见 §4） |
| **3** | 279 张卡入库 + 提交 v4.3 规范 | 未受管的卡不受任何门禁约束；§1 的问题全在它们身上 | ⬜ 待办 |
| **4** | 重跑 `materialize-definitions.ps1` | 否则改卡进不了游戏（当前是 09-20 快照） | ⬜ 待办 |
| **5** | 14 个未注册 tag 按 W7-a 分流 | 否则 276 张卡运行时降级 `IDENTITY_ONLY`，**人设全丢** | ⬜ 待办 |
| 6 | 才轮到内容扩充（英雄 6.5% → 覆盖；war / culture 补写） | **在 1–5 之前不该开工** | ⬜ 待办 |

---

## 4. 执行记录：第 1、2 项【实测】

### 4.1 改动

| 文件 | 改动 | diff |
|---|---|---|
| `AWAKE/src/WorldbookPackageIntegrity.cs` | `WriteString` 里 `case '"'` 的转义由 `\"` 改为 `\u0022`（+ 6 行说明注释） | +7 / −1 |
| `AWAKE/tools/build.ps1` | `TESTS_OK` 之后新增**真跑** `Awake.SdkSmoke.exe` 的步骤；非 0 即 `throw`；新增 `-SkipSmoke` 开关 | +28 / −1 |

**根因一句话**：包是 Studio 用 System.Text.Json 默认编码器写的，它把字符串里的双引号输出为 `\u0022`；而运行时把它重写成 `\"`。同一份内容算出不同的规范字节流 ⇒ `contentHash` 不符。
**触发条件**：正文里出现 ASCII 双引号。pilot 包无此字符故一直绿；calradia 包有 **2 处**故必红。长度差恰好 8 = 2 × 4 字符（`\"` 比 `\u0022` 少 4），即两串**只差这一处**。

### 4.2 红 → 绿证据

**红（修复前）**：

```
FAIL_CASE dialogue-chain-redtest
FAIL_MSG  WB2-HASH-MISMATCH:content
RESULT    total=67 passed=66 failed=1
退出码     1
```

`build.ps1` 在新步骤中止：

```
Awake.SdkSmoke failed with exit code 1 (offline gate is RED)
[exit code: 1]
```

**绿（修复后）**：

```
BUILD_OK  api=1.4.8 configuration=Release
TESTS_OK  configuration=Release
RESULT    total=67 passed=67 failed=0
PASS ALL  Awake.SdkSmoke
SMOKE_OK  configuration=Release
退出码     0
```

**关键行**（首次出现）：

```
PASS dialogue chain worldbook deployed redtest entries=800 package=awake:worldbook.calradia
```

⇒ **calradia 世界书包第一次通过运行时校验。800 条内容的运行时受众由 0 变为 800。**

### 4.3 产物哈希

| | SHA-256 |
|---|---|
| `_build_out\1.4.8\Release\Awake.dll`（**新**，含修复） | `248A63ACC7B952B29297681BA03D0D94F21E6EA7F20990B5148987CEB01D3207` |
| `dist\Modules\AWAKE\...\Awake.dll`（旧） | `9F3C432F992784B9FF1D3C73492AB9E2271EF7A93BD6FD10EC5773363A681BEE` |
| 游戏目录 `Awake.dll`（旧） | `9F3C432F992784B9FF1D3C73492AB9E2271EF7A93BD6FD10EC5773363A681BEE` |

⚠️ **`dist` 与游戏目录仍是旧 DLL。**「`_build_out` == `dist` == 游戏」这条 E3 不变量**现在是故意不成立的**——源码变了，同步是独立动作。**恢复它需要投送授权**（且游戏须已退出）。修复前基线已备份至 `%TEMP%\awake-fix1-backup\`。

### 4.4 已知代价（必须知情）

`Awake.SdkSmoke` 的 `dialogue-chain-redtest` 判据是**已投送的**世界书（`DialogueChainRedtest.ResolveDeployedManifest` 默认指向游戏模块目录），因此：

- **`build.ps1` 现在要求游戏侧模块已投送。** 这与 `AGENTS.md`「干净克隆下一条命令即可产出 `Awake.dll`」相冲突。
- 干净克隆上请用 `.\AWAKE\tools\build.ps1 -SkipSmoke`。
- 更好的长期解是让该用例优先校验**仓库侧**包、部署侧存在时才校验部署侧——但那要改测试，本批未做。

### 4.5 未处理

- `AWAKE.Tests` 仍有 **4 个 CS4014 警告**（预先存在，非本批引入）。质量门写的是「主工程 0 warning / 0 error」，主工程确实是 0 warning，故未纳入本批。

---

## 5. ⚠️ 操作陷阱：改 `.ps1` 必须保住 UTF-8 BOM

**本批踩到并修复，记录下来避免重演。**

- 本项目 `.ps1` 文件是 **UTF-8 with BOM**（`build.ps1` 原始即有 BOM）；`.cs` 与 `.md` 是 **无 BOM**、行尾 **LF**（`core.autocrlf=true`，那个 LF→CRLF 警告是常规提示，不是问题）。
- **Windows PowerShell 5.1 在没有 BOM 时按 ANSI 读 `.ps1`。** 文件里有中文注释/emoji 时，误码会打乱大括号位置，报出**与真实结构无关**的语法错误（本次报 `L55 }` / `L68 }`，而实际结构完好）。
- 因此：**任何用编辑器重写 `.ps1` 的操作都会剥掉 BOM，必须在写回后补回** `EF BB BF`。

**自查一行**：

```powershell
$b=[IO.File]::ReadAllBytes('AWAKE\tools\build.ps1'); ($b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF)
```

---

## 6. 待授权 / 待办

| 项 | 说明 | 需要授权？ |
|---|---|---|
| 投送修复后的 DLL 到 `dist` + 游戏目录 | 恢复 E3 不变量；游戏须已退出 | ✅ 需要 |
| 进游戏验证 `entries=800` | 唯一能判 E4 的途径；`Awake.log` 应出现 `worldbook_runtime_initialized … entries=800` | ✅ 需要（启动游戏） |
| 提交本批 2 个改动 + 3 份文档 | 精确 pathspec，禁 `git add -A` | ✅ 需要 |
| 第 3–5 项（卡入库 / 重物化 / tag 分流） | 见 §3 | 视具体动作 |
| `README.md` 与 `AGENTS.md` 的口径冲突（世界书已定为内容包） | 见 `REVIEW-DESIGN-20261001.md` | ✅ 需要决策 |

---

## 7. 本文件未做的事

- 未改动 §4.1 之外任何文件；未提交任何东西；未投送游戏目录
- §1 的部分数字来自代码/数据级审计，**本方未逐条复验**
- **所有结论均未在真机复现**；§6 的 E4 判据必须由用户进游戏后才能判
