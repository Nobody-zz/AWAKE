# 角色卡 AI 感：可复现检查器

作者和审阅者的操作规范见 [AI-FEEL-REVIEW-SPEC.zh-CN.md](AI-FEEL-REVIEW-SPEC.zh-CN.md)。

入口：`persona_aifeel.py`。旧交接目录的七个脚本不再作为放行依据；本工具不修改角色卡、tag registry 或游戏目录。只看物化后的九个文本字段；`tags` 由真 DSL 生成器处理。作者草稿字段不计入。

## 使用

在 `D:\AWAKE-Dev` 执行：

```powershell
py -3 -m unittest discover -s AWAKE/tools/persona-workbench/qc -p test_persona_aifeel.py -v
py -3 AWAKE/tools/persona-workbench/qc/persona_aifeel.py audit --definitions .workbuddy/tmp/pilot_trial/def_after
py -3 AWAKE/tools/persona-workbench/qc/persona_aifeel.py answers .workbuddy/tmp/pilot_trial/answers_ministral-3_8b.json
py -3 AWAKE/tools/persona-workbench/qc/persona_aifeel.py relations --definitions .workbuddy/tmp/pilot_trial/def_after --links AWAKE/tools/persona-workbench/persona_links.pilot.json
```

`audit`、`answers` 和 `relations` 只读现有快照。`answers` 默认只看 `after`，绝不把“比旧稿好”当放行证据。原始回答必须直接是 JSON 对象（代码围栏不算），含非空 `reply` / `mood`；chat 模式不得带 `command`。不满足这些基础约束的样本列入 `invalid`，不参与风格统计。这**不是**完整 JSON Schema 验证。旧答案文件没有完整 prompt/模型版本证明，只作历史观察。`relations` 只列出关联证据的上限和待核行为，不做语义放行。

若所选版本全部无效，报告仍列逐条原因、状态为 `no_valid_replies`，命令以非零码退出；加全局 `--out` 可保留这份诊断报告。
其中严格围栏包裹且内部 JSON 仍含 `reply` 的样本会另列入 `diagnostic_only`，只用于读其文风，**不计入** `valid` 或放行读数；其他坏输出不猜测正文。

真链路重跑（须先编译两个离线验台；示例角色卡路径按实际选取）：

```powershell
dotnet build AWAKE/tools/worldbook-runtime-sim/WorldbookRuntimeSim.csproj -c Release
dotnet build AWAKE/tools/worldbook-runtime-production-smoke/WorldbookRuntimeProductionSmoke.csproj -c Release
$card = (Get-ChildItem AWAKE/tools/persona-workbench/characters -Filter '*ulbos*.persona.json' | Select-Object -First 1).FullName
py -3 AWAKE/tools/persona-workbench/qc/persona_aifeel.py chain $card
```

`chain` 要求每张卡有 `.origins.json`，只复制选定卡及 sidecar 到系统临时目录；同名卡或重复 `heroId` 会阻断。`materialize-definitions.ps1 -OutDir` **仅指向该临时目录**。然后调用真实 `WorldbookRuntimeSim persona`；若出现 `fallback=True` 或 `WARN persona.tag_unregistered`，立即 `EVALUATION_BLOCKED`，绝不把 ID/NAME fallback 算作人格回答。该验台使用 `--force-approved`，只供离线试验，**不证明源卡具备生产批准状态**。最后调用生产 smoke 中的 `NpcPromptTemplate.TemplateText` 和 `NpcDialoguePromptPipeline.RenderTemplate`，不在 Python 复制模板或渲染逻辑。报告含源码/程序集/卡与 sidecar/DSL/提示词 SHA-256、真实 DSL 和渲染后的完整提示词。先按上方命令重新构建，单有哈希不能证明旧程序集与当前源码一致。可选 `chain --model ministral-3:8b $card` 调本地 `/api/chat`；自动绕过本机代理。模型看到的是固定离线情境，不是游戏实时状态。`--out` 是全局选项，放在子命令**之前**，可把完整 JSON 报告写到指定文件。

## 读数、分辨力与盲区

这些读数是定位器，不是 AI 感分数；少于三张卡时 `insufficient_cross_card_sample=true`，不得据此推断群体风格，更不设未经校准的“合格阈值”。

| 读数 | 能抓什么 | 怎样证伪 / 已有对照 | 不能证明什么 |
|---|---|---|---|
| `frame_families` / `first_family_share` | 新稿自身是否反复用“先做 X，再做 Y”一类关系骨架，哪怕换了专名和动词 | 同骨架换词、把连接词改成“第一步…接着…”都应保持高读数；换不同句法应降低 | “先”本身不坏；未列入的同义句式仍可绕过，需人工盲读 |
| `family_card_coverage` / `first_family_card_coverage` | 这种骨架覆盖多少张**不同**人物卡 | 一张卡写八条、另两张不写时，按句占比可很高，但跨卡覆盖仍为一张 | 覆盖广也可能是情境要求相同；不是“角色没有个性”的充分证据 |
| `top_shape_share` / `shape_diversity` | 标点和连接词骨架是否集中 | 同骨架换实体应仍相似；改变分句和连接应分散 | 很短或固定字段的自然一致性；跨文化不能直接比 |
| `length_min/median/max/cv` | 多条例句被卡在同一长度带 | 窄长度对照应比宽长度对照 `cv` 小 | 长短参差不代表自然；故意拉长可作弊 |
| `top_ending_share` | 例句是否都以同一种姿态收尾 | 全句号对照应为 1；混合收尾对照应降低 | 句号通常合理，单看它不能判红 |
| `review_flags` | 例句出现“你”，可能把原场景对象误认作玩家 | 注入“你”应有定位行；无“你”应无此标记 | 引语中的“你”可能完全合理，不自动拒绝 |
| `invalid` | 本地模型答复缺基础 chat 字段或违规带 `command` | 破坏 JSON、删去 `mood`、加入 `command` 都必须进入 invalid，不能悄悄回退成正文 | 基础校验成功不等于完整 schema 合法，更不表示答得好 |

运行 `test_persona_aifeel.py` 是检查**读数分辨力**，不是用造出来的文本证明实卡通过。真正应用时先看 `n/cards`，再看 `top_shapes.examples` 与 `manual_review_queue` 的原句，最后人工盲读三位人物在同一情境下的完整回答。把专名遮掉，逐对问：能否认出是谁、有没有自己特有的取舍、说话是否像一个具体的人？记录判断理由，允许“无法判定”。

## 人工放行单：不可伪装成自动指标

对每条 `selfClaimRules / realSelfBehaviors / selfClaimExamples` 按报告的 `manual_review_queue` 填：原句、声称的事实或职权、独立证据位置、证据**确切支持到哪层**、是否只是 `confirmedFor: place`、若失去该素材人物会如何选择、读者是否认为自然。`confirmedFor: place` 只证实地点关系，不能推出征税权、经营责任或政治动机；缺证据可留白，不得宣称“没有关系”。`depth` 不是字段白名单。例句要单独核对场景、说话对象与玩家是否混淆；不能用“开头有括号”充当场景锚判据。

建议把人工盲读结果与 JSON 报告一起保留，记录卡哈希、模板哈希、模型标识/参数/种子和样本数。若改稿只是换词、扩长、加逗号而人物决策仍互换，不能放行。若检测器没报但人工判为同质化，保存该反例并扩充正反对照测试；不要反过来调阈值直到它“变绿”。

本工具不在游戏中生效，是离线 E2 证据；不会替代游戏内 E4/E5 验证。MCM 无需调整：这里只增加作者质检 CLI 和离线验台入口，没有玩家可调行为。
