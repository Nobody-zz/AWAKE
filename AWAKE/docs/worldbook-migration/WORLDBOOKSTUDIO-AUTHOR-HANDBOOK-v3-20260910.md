# Worldbook Studio 世界书作者手册 v3（2026-09-10）

> **定位**：本文是 Worldbook Studio 的**唯一权威作者文档**，合并并取代以下四份前身文档：
> 写作标准（`WORLDBOOK-CALRADIA-KNOWLEDGE-INDEX-V2-20260909.md`）、
> 操作模板（`tools/worldbook-studio/新手指引_世界书内容编辑者.md`）、
> 样例参照（`tools/worldbook-studio/tests/fixtures/official-reference/pravend-codex-reference.md`）、
> 验收报告（`WORLDBOOKSTUDIO-AUTHOR-LOOP-ACCEPTANCE-20260910.md`）。
> 四份前身文档保留为历史记录与交付物，**不再作为权威**；与本文冲突时以本文为准。
>
> **规则分层**（不要混谈）：
> `L1` 编辑器代码 / JSON Schema / taxonomy 实际强制的内容；
> `L2` 作者写作规范（本文 B 部分，编辑器不会自动执行）；
> `L3` 提案规则（登记在 `WORLDBOOKSTUDIO-AI-PROPOSED-RULES-BACKLOG-20260909.md`，未经转正不得当作能力）。
> 转正门槛：① 有代码实现；② 有自动化测试；③ 有真实生成/校验证据；④ 经用户批准。

---

# A 部分：操作手册

面向不懂编程、不熟悉数据结构的作者：你只需要在浏览器里的中文表单中填文字、选选项，然后保存和预览。

## A1 先记住三件事

1. **世界书不是小说正文。** 它记录 NPC 可以知道、理解和说出的世界知识（某个国家由谁统治、某座城镇靠什么产业生存、某场战争为什么发生、平民/士兵/贵族分别知道到什么程度）。它不是让所有 NPC 都知道全部历史，也不是把一整段百科塞给 AI。
2. **一条知识分成两层。** *客观事实*＝作者希望世界书记录的事实本身；*NPC 表达*＝某种身份的 NPC 实际会怎样说这件事。

   > 客观事实：北方战争在连续三年歉收后爆发。
   > 平民表达：北边那几年连麦子都收不上来，后来领主们就打起来了。
   > 贵族表达：战争起因包括粮税争议、边境继承权和三年歉收造成的财政危机。

3. **AI 只提供建议，不替你决定世界观。**

## A2 第一次打开程序

1. **解压程序包**：解压到普通文件夹，不要在压缩包内直接运行。
2. **启动 Launcher**：运行 `Awake.WorldbookStudio.Launcher.exe`。必须使用当前压缩包内的 Launcher，不要继续使用旧候选包已经打开的浏览器页面。
3. **选择工作区**：工作区是你保存档案的地方，可以自定位置。
4. **分清“工作区”和“程序包”**：程序包是软件本体，升级换包时不要覆盖工作区；备份或换电脑时优先复制**整个工作区**，不要只复制程序包。

## A3 编辑一份世界书档案

进入编辑器后优先使用**普通编辑模式**，不要一开始就打开“高级模式”或直接编辑 YAML。

### A3.1 新建档案

新建窗口只需填写：档案标题、最主要的知识分类、该分类下的二级主题、可选的相关分类、内容层。
**不用**填文件位置、英文 ID、版本号或注册表信息——程序会自动生成安全的保存位置与稳定内部编号；以后改标题也不会破坏档案关联。

一份档案最好只记录一个明确主题（“瓦兰迪亚的粮食贸易”“北方战争的起因”“某城镇的宗教习俗”“贵族婚姻与继承规则”）。不要把一国的政治、经济、文化、战争、地理全塞进一份档案：主题越清楚，NPC 调取越准确。

### A3.2 档案标题

写一个编辑者一眼能看懂的中文标题。推荐“北方战争的起因”；不推荐“新档案 1”“重要设定”“war_01”。

### A3.3 知识分类

工作室固定使用五大分类：

- **政治**：王位、王国、领地、官职、法律、外交、家族、继承
- **经济**：土地、粮食、贸易、税收、货币、工坊、债务、商路
- **文化**：信仰、习俗、语言、身份、婚姻、服饰、节日、艺术
- **战争**：战史、军制、兵种、武器、战术、要塞、军需、俘虏
- **地理**：地形、气候、方位、河流、道路、聚落、边界、资源、航路

### A3.4 二级主题怎么选

二级主题不是技术字段，而是帮助你判断“这份档案到底在写什么”。示例：

- “某城向王室缴纳什么税” → **政治 → 法律** 或 **经济 → 税收**（看档案重点）
- “某城位于河流东岸” → **地理 → 聚落** 或 **地理 → 河流**
- “两国因山口归属交战” → 主分类 **政治** 或 **战争**，并把 **地理** 设为相关分类
- “某条商路经过哪些山口” → **经济 → 商路**，把 **地理** 设为相关分类

一份档案只选一个**最主要**的主分类，其余明显相关的领域用“相关分类”补充；不要为了保险把五类全选上。
地理是独立的第五类，不是政治或经济的附属：地点在哪、道路怎么走、河流山脉如何分布 → 地理；谁拥有这片土地、如何征税交易 → 政治或经济。

### A3.5 知识适用时期（原“所属时代”）

表示**这条知识发生、成立或有效的时间范围**。

- 界面中的“世界观开始年份 / 结束年份”填的是**世界观年表年份**，不是游戏天数、存档运行天数或游戏内倒计时；
- 它不负责触发事件；要定时发生的事交给事件系统；
- 常见取值：当前世界、历史时期、某次战争前、某次战争后、某位国王统治时期、长期存在、时间未确定；
- 无法确定精确年份时填相对时期或“时间未确定”，**不要自行编造年份**；年份不确定时起止年份都可以留空。

### A3.6 确定程度

表示作者对这条知识有多大把握：**确定**（已确认，作为客观基础）、**较可信**（基本确定，仍需复核）、**存疑**（保留待查）。

## A4 怎样填写“客观事实”

### A4.1 一条事实只说一件主要事情

不要把多个事件、多个人物、多个年份挤进同一条事实。如果你发现自己在一条事实里写“因为…所以…后来…”，先考虑拆成多条。

### A4.2 事实的写法

写客观陈述，不写修辞，不写“据说”“可能”这类模糊语（除非你确实要表达传闻，那就选对应的类型）。事实文本与来源引文应保持可区分——不要整句照抄引文当事实。

### A4.3 事实类型怎么选

| 内容性质 | 推荐类型 |
|---|---|
| 可直接核验的事实 | `fact` |
| 某对象在某时期的状态 | `state` |
| 两个对象之间的关系 | `relation` |
| 作者的推断性解释 | `interpretation` |
| 传闻、流言 | `rumor` |

**不得**把“某次入侵是正义的”这类价值判断写成 `fact`；必须保留时标为 `interpretation` 或 `rumor`。

## A5 怎样填写“NPC 表达”

表达层按身份区分，同一个事实可以有多种说法：

- **平民**：只知道身边能接触到的部分，语气粗粝、信息零碎。例：北边那几年连麦子都收不上来，后来领主们就为粮税和地盘打了起来。
- **村庄或城镇头人**：比平民更了解本地与本国事务（税收、征兵、治安、地方史），可以说得更完整，但**不应**自动知道贵族密谈、王室秘密或军事机密。
- **公证商人、赎金经纪人、酒馆老板**：知识面较广的中间人，通常知道跨地区消息、债务与赎金、商路与价格、贵族间公开流传的传闻、士兵和旅行者带来的消息；可以充当“转介绍对象”。知道得广 ≠ 知道全部秘密。
- **士兵**：了解本国战争、征兵与军粮、军队日常、常见兵种与战术、自己参加过或听军官讲过的战事；**不应**自动知道国王密令、贵族私下交易或完整战略计划。
- **贵族**：按年龄、管理能力与实际经历区分——
  - 年轻、低管理能力：家族与本地事务，接触的国家秘密较少；
  - 有管理经验：税收、派系、继承、外交、行政；
  - 年长且管理能力高：大部分公开国家知识与部分高级秘密；
  - 贵族兵种／贵族军官：军事领域接近贵族，其他领域不应自动全知。

不要因为身份是“贵族”就让他知道所有历史和所有秘密。

## A6 设置“谁知道这条知识”

在“知识权限 / 知道这条知识的身份”区域选择身份；普通编辑模式显示中文名称，程序自动转换内部 ID，**不要手填英文 ID**。

常见身份：普通平民、村庄头人、城镇头人、公证商人、赎金经纪人、酒馆老板、士兵、贵族、贵族军官或贵族兵种。

### A6.1 知识范围

**本地**（只在村庄/城镇或附近流传）、**本国**（本国不同地方的相关人物可能知道）、**跨国**（商人、外交人员、旅行者、高级贵族可能知道）、**秘密**（只有明确授权的少数身份知道）。

### A6.2 最低表达详细度

**摘要**（只能说一两句概括）、**常规**（可说明主要经过）、**详细**（可谈多个原因、人物和后果）、**机密**（只有特别授权的角色可以说出）。
一个身份可以“知道事实”，但只能说摘要——**知道和能完整说出不是一回事**。

### A6.3 什么时候不要选“所有人”

王室密令、贵族私下交易、尚未公开的继承安排、军事部署、只有少数商人掌握的债务关系、尚未证实的幕后真相——一般不要授权给所有人。只有少数身份知道，就明确选少数身份。

## A7 推荐的实际编辑流程

1. 先定主题：一句话说明这份档案要记录什么。
2. 选主分类和二级主题，再按需选相关分类。
3. 填知识适用时期和确定程度；不知道就标“时间未确定”或“存疑”。
4. 写摘要：说明范围，不写完整百科。
5. 添加客观事实：一条事实一件主要事情。
6. 给事实分配身份权限：谁知道、知道范围、表达详细度。
7. 为重要身份写 NPC 表达（至少区分平民、头人、士兵、贵族中的相关角色）。
8. 运行本地检查：修空字段、重复 ID、权限缺失、格式问题。
9. 使用 AI 建议：逐条阅读，不要一键盲目接受。
10. 预览不同身份：分别查看平民、士兵、贵族会看到什么。
11. 保存：看到保存成功后再离开页面。
12. 导出或提交审核：只有确认过的版本才交给下一位作者或集成者。

## A8 保存、导出和备份

**保存前确认**：标题非空、领域已选、至少一条客观事实、重要事实至少有一个知道它的身份、NPC 表达没有明显超出身份权限。
如果提示“档案已被其他操作更新”，先重新读取档案再重新检查自己的修改；**不要反复点击保存覆盖别人版本**。

**导出前**：先运行检查 → 预览至少两种身份 → 确认内容状态 → 在备注写明本次修改目的 → 使用带日期或版本名的导出文件夹。

**备份**：按批次备份工作区（如 `WorldbookStudio-工作区-<日期>`、`政治档案-第一轮审核`、`战争知识-作者确认版`）。
**任何情况下**不要把 API Key 写进文本文件、档案正文、截图或导出包。

## A9 常见错误

| 提示 | 处理 |
|---|---|
| “客观事实不能为空” | 至少写一条事实再保存 |
| “没有指定知识权限” | 给重要事实选择知道它的身份 |
| “我不知道应该给谁知道” | 按 A6 从“谁最可能接触到这件事”倒推身份 |
| “AI 建议和我的设定不一样” | 世界观由作者决定，建议可以忽略 |
| “浏览器打不开” | 关闭旧窗口，只保留一个新版窗口后重试 |
| “工作区无法使用” | 确认工作区目录存在且可写，且未被占用 |
| “端口被占用” | 关闭其它占用了工作室端口的程序后重启 Launcher |

工作区为空或搜索不到档案时，可直接使用页面提供的“创建第一份档案 / 新建档案 / 清除搜索”按钮。
普通编辑模式默认不显示文件路径、英文内部编号、版本号和注册表哈希；排查技术问题时再展开“高级技术信息”。

## A10 交稿前检查清单

- [ ] 标题是中文可读标题，不是“新档案 1”
- [ ] 只选一个主分类，相关分类确有需要
- [ ] 时期与确定程度已填，且没有编造年份
- [ ] 每条事实只讲一件主要事情
- [ ] 事实类型（fact/state/relation/interpretation/rumor）与内容性质一致
- [ ] 重要事实都有明确的身份权限
- [ ] 没有把机密开放给平民或无关身份
- [ ] NPC 表达符合身份的年龄、阶层、职业与经历
- [ ] 至少预览过两种身份
- [ ] 已运行本地检查并修复全部格式/权限问题
- [ ] 保存成功，导出目录带日期或版本名

---

# B 部分：写作标准

## B1 领域与归类

固定五类：`politics` / `economy` / `culture` / `war` / `geography`。

- 自然地形、聚落位置、方位、河流、道路走向 → `geography`
- 主权边界、聚落行政、国家、官职、法律、继承、外交、家族 → `politics`
- 土地生产、粮食、交易、税收、物价、商路经营 → `economy`
- 习俗、语言、信仰、荣誉、婚姻、葬礼、节庆、偏见 → `culture`
- 军制、战争、兵种、战役、防御、征召 → `war`

## B2 时间维度

必须落到 `era`：`current` / `historical` / `pre_war` / `during_war` / `post_war` / `long_term` / `unknown`。
`current_and_historical` 只用于叙述性过渡，**不应**作为正式存档值。历史事实不得写成 current。

## B3 内容状态与建设优先级

| 内容状态 | 含义 | 是否进入 Runtime |
|---|---|---|
| `canon` | 已批准当前正典 | 可 |
| `needs_review` | 待人工复核 | 否 |
| `reference_only` | 参考层 | 否 |
| `rumor` | 传闻层 | 仅受控表达 |
| `future` | 未来/扩展层 | 否 |

建设优先级 `p0` / `p1` / `p2` 只表示**建设顺序**，不表示内容正确或正典。

## B4 条目边界规则（v2 最重要的补充）

**一个作者主题不必然等于一个 document。** 以下内容不能写进同一条简介：地点简介、地点历史沿革、地点当前政治归属、相关家族关系、相关战争事件、宗教或文化解释。

### B4.1 帕拉汶德最小拆分示例

```text
doc.geography.pravend
  - 当前名称
  - 历史名称/别名
  - 聚落类型
  - 北部平原关系

doc.politics.pravend-historical-name
  - 巴拉维诺斯旧名
  - 建立与首都关系
  - 帝国重心东移后的西部经济重镇地位

doc.politics.pravend-succession
  - 奥斯里克入侵
  - 与元老协商投降
  - 戴·提尔家族旁支传承
```

若实体目录中已有稳定 ID（如 `entity.settlement.town_v3`），别名/旧称应通过 `aliases` 或 `redirects` 保留，**不要**新建一个看起来像正式实体 ID 的自造 ID。

## B5 Assertion 类型边界

五种 `assertion.kind`：`fact` / `interpretation` / `rumor` / `relation` / `state`。
帕拉汶德映射示例：

| 内容 | 推荐 kind |
|---|---|
| 巴拉维诺斯由卡拉狄乌斯大帝建立 | `fact` |
| 后取代沙拉斯成为首都 | `fact` |
| 帝国重心东移后仍是西部经济重镇 | `state` |
| 奥斯里克入侵后与元老协商投降 | `fact` |
| 传承到戴·提尔家族旁支 | `relation` |
| 现名是帕拉汶德 | `fact` |

## B6 来源优先级与登记

| 优先级 | 来源 | 用途 | 登记建议 |
|---|---|---|---|
| A | Bannerlord 游戏数据与官方本地化文本 | 对象存在、名称、文化、身份、聚落、王国、兵种 | `game_snapshot` |
| B | 卡拉迪亚编年史 | 历史、人物背景、政治与文化叙述 | `chronicle` |
| C | 卡拉迪亚之王资料提取 | 国家、文化、宗教、制度、事件、地点、军事 | `mod_extract` |
| D | 开发者原创裁定 | 解决冲突、填补空白、项目独有逻辑 | `developer_original` |
| E | 战团未来、CK3、成人拓展、未核实素材 | 可选/未来/扩展层 | `future_era` / `adult_extension` / `under_review` |

任何进入正典的 source 必须登记到 source registry，且 `license_status`、`use_status`、`source_content_hash` 可核验。

**官方中文 XML 的定位**：`SandBox/ModuleData/Languages/CNs` 与 `SandBoxCore/ModuleData/Languages/CNs` 属官方本地化文本，可用于确认对象存在、官方名称与文化/地点/历史叙述；**不是**可直接导入的 authoring 文档，必须先摘录成带 locator 的 source 才能登记。

**口径**：官方中文译名优先用于显示；官方叙述冲突时由项目主编裁定；开发者裁定必须标 `source_nature=developer_original`；同一对象的名称、别名与 redirect 必须保持稳定。

**引文粒度**：一段官方文本可能同时包含多个时间层与主题（建城／首都／经济／易主／传承／改名）。**引文粒度必须细于断言**——同一段被不同断言引用时，应各自使用对应子引文与不同 quote_hash，不允许“整段引用冒充局部支撑”。

## B7 身份表达与知识可见性

每条 expression 必须明确：`layer`（`rumor|summary|detail|secret`）、`grants`（`profile_id` / `scope` / `min_detail`）、`denies`（`profile_id` / `scope`）、`fallback_referral_ids`。
**不允许**只写“三种身份表达”而不指定 profile 与 layer。

## B8 生命周期

世界知识必须考虑：`revision`、`supersedes`、`split_from`、`merged_into`、`event_id`、`valid_from` / `valid_until`。
旧名称应通过 `aliases` 或 `redirects` 保留，**不直接删除或改写同一条 `id`**。

## B9 不写入当前正史

- 动态游戏所有权与当前战争结局；
- 未经游戏数据支持的正式实体 ID；
- 精确建立年份（官方文本未给出时）；
- 官方文本未出现的私人关系、额外人物；
- 任何未登记的 AI 生成事实；
- 旧四版成人化世界书内容；
- 战团未来、CK3 扩展与未核实素材。

## B10 工作原则

```text
先按五领域归类，再拆条目边界；
先登记官方来源，再生成作者档案；
先写 fact/state/relation，再写 interpretation/rumor；
先做地理聚落简介，再做政治沿革和家族关系；
先完成 P0 骨架，再补 NPC 本地生活表达。
```

---

# C 部分：AI 辅助与 Quick Authoring

## C1 两种模式不得混淆

```text
Quick Authoring（快速建档）
  简单提示词 + 参考资料 → facts → metadata → expressions
  → 完整 authoring candidate → evidence/warnings/unresolved → needs_review

Semantic Migration（语义迁移）
  source atom → proposition inventory → claim normalization
  → perspective/time/epistemic 分析 → target span → 人工语义复核 → authoring candidate
```

Quick Authoring **不能**冒充高保真语义迁移；它不产出逐命题的语义闭环，不得用于需要精确 claim 分解的迁移任务。

## C2 Quick Authoring 全流程

首页的“从资料生成待审核档案”适合处理一份资料：

1. 粘贴或导入参考资料。
2. 填写“我想整理什么”，说明最终希望得到哪类世界知识档案。**用户的简单生成指令必须真正进入请求字段**（见 C3），不能只当作界面文字被丢弃。
3. 如有需要，在“还有什么特别要求”中补充整理重点；它是**附加约束**，不能覆盖系统规则与安全约束。
4. 明确选择内容范围。成人拓展需额外确认；**空白范围不会自动变成基础内容**。
5. 点击“开始生成草稿”：一次整理资料、绑定来源并生成待审核候选，不需要再手动按三个技术阶段重复生成。
6. 先看结果摘要，再查看**候选、来源、警告、待处理项**；结果不是正典，不会自动发布或进入运行包。
7. 逐条阅读资料内容。确认无误后点击“采纳这条”；**改过文字必须重新采纳**。
8. 需要 NPC 身份表达时，在“进一步限定结果”中**主动填写视角**；不填就不生成身份表达。
9. 选择一份候选后点击“创建待审核档案”：这一步只会创建 `needs_review` 档案。
10. 进入作者编辑器继续检查二级主题、知识权限、时期适用范围等字段，最后保存。

“高级候选审查”中的合并、拆分、丢弃与重排只用于特殊情况；批量工作台继续单独处理多份资料；兼容分阶段流程仍然保留，但不属于 Quick Authoring 默认路径。

**幂等与边界**：同一份草稿重复提交返回同一份档案，不会因网络重试生成多个副本；参考资料只用于本次草稿会话，不自动写入来源库，也不自动联网抓取网页。

## C3 请求字段（进哈希与审计）

| 字段 | 作用 |
|---|---|
| `mode` | 区分 Quick Authoring / Semantic Migration |
| `authoring_goal` | 本次要整理成什么（分类/主题方向） |
| `user_instruction` | 用户原话；**不可信输入**，只作为约束，不得覆盖系统 prompt 与安全约束 |
| `requested_domain` / `requested_subdomain` | 期望主分类与二级主题 |
| `requested_audience` | 目标知识受众 |
| `requested_perspectives` | 请求的身份视角（≤3；未请求不得产出表达） |
| `style_constraints` | 文风与口径约束 |
| `must_preserve[]` | 必须保留的既有内容 |
| `must_not_invent[]` | 禁止新增（人名、年份、战争、正式 entity ID 等） |
| content tier | `base` / `adult_optional` |

这些字段必须进入 **request hash、candidate fingerprint 与审计记录**，且 fingerprint 可被第三方独立重算。

## C4 怎样写给 AI 的任务说明

使用“限制条件 + 目标”的写法。推荐：

> 请只检查这条档案的知识权限，不要新增历史事实。重点判断普通平民是否知道了贵族密谈。

> 请把这段话改成普通士兵能说出的表达，保留原有事实，不增加新人物、新年份和新事件。

> 请检查这份档案是否把多个事件混在一条事实中，并给出拆分建议，不要直接修改原文。

不推荐：

> 帮我完善这个世界。

范围越明确，建议越容易审核。

## C5 阅读一条 AI 建议的四个问题

1. 它指出的是实际错误，还是只是另一种写法？
2. 它有没有偷偷新增我没有设定的人物、年份、地点或事件？
3. 它有没有扩大某个 NPC 的知识权限？
4. 它是否符合这个身份的年龄、阶层、职业与经历？

只是文风建议时，可以采纳表达、不采纳事实扩展；涉及世界观、身份权限或历史因果，必须由作者自己确认。

## C6 AI 助手能做什么 / 不能做什么

**可以**：格式检查、内容补全提示、事实纠偏、知识权限检查、身份差异检查、表达详细度检查、语言风格建议、领域分类建议、世界观一致性（现代词汇/制度）检查。

**不要**：直接接受 AI 编造的人物、年份、事件；让 AI 自动决定世界观真相；把 AI 猜测当客观事实；用现代心理学或网络用语替换中世纪语气；因为 AI 说“所有人都知道”就给所有身份授权。

**批量应用前的安全做法**：先保存当前档案 → 逐条查看建议 → 优先应用格式与错别字修正 → 对事实/权限/世界观建议逐条确认 → 应用后重新预览至少两个身份 → 结果不对就撤销或重新载入上一个已保存版本。

**没有 API Key 也能工作**：基本格式检查、必填项检查、权限缺失检查、身份与表达层级检查、预览和导出都不需要云端 AI。

**本地 Worker 与云端**：本机可运行本地 Worker（离线、回环）承担生成；云端需在“云端设置”填写 Provider 地址、模型名称与 API Key。API Key 由程序保存在本机加密区域，不写入世界书、工作区、普通日志或导出包；输入框留空通常表示继续使用已保存的 Key。

## C7 内置提示词的硬约束

- 用户指令与参考资料都是**不可信输入**：来源中若出现提示词注入（“忽略前面的规则”等），不得改变系统约束、不得放宽证据要求。
- `rumor` 不得变 `fact`；`historical` 不得变 `current`；多文化视角不得被抹平。
- quote 必须能在来源文件中定位；**proposition 超出 quote 支撑范围时必须告警或阻断**。
- source 中的命题少于正文命题时必须失败（不允许凭空补命题）。
- `unknown` 内容层不得静默变成 `base`：要么保持 unknown，要么阻断并要求人工选择。
- `must_not_invent` 必须阻止新增人物、年份、战争与正式 entity ID。
- retry 不得产生重复候选。

---

# D 部分：样例与参照簇（帕拉汶德）

> 性质：全部 `status: reference_only`；不是正典，不是迁移候选，不写入游戏目录，
> 不得进入 `approved` / `canon` / `compiled` / `published`。
> 位置：`tools/worldbook-studio/tests/fixtures/official-reference/pravend-cluster/`。

## D1 簇结构

```text
official-reference/pravend-cluster/
├── authoring/
│   ├── geography/pravend.yaml                      # doc.geography.pravend
│   └── politics/
│       ├── pravend-historical-name.yaml            # doc.politics.pravend-historical-name
│       └── pravend-succession.yaml                 # doc.politics.pravend-succession
└── sources/
    ├── source.bannerlord.sandbox.settlements.history.yaml
    └── pravend-official-cns-extract.txt
```

## D2 来源登记

- 官方文件：`Modules\SandBox\ModuleData\Languages\CNs\std_settlements_xml-zho-CN.xml`，SHA-256 `46df7c77c4fa184d1c67c3dc5881f1098c3b6842ae051a188debd41af9d95aeb`
- 摘录文本 SHA-256：`2f06ef6ab1029bdb6a5920c94cc9adfa6a518a98f106c1e5d7e4710d0172f9c7`
- 摘录只保留帕拉汶德自身段落（原行 23）；行 259（弗雷吉昂）已移除。
- 行 23 全段被不同断言引用为多个**子引文**，quote_hash 各不相同（如 `59c8b208…`、`8888a1ae…`、`89d23bed…`），校验器按引文内容独立核验。

## D3 三文档拆分

| 文档 ID | domain / subdomain | era | 收录断言 |
|---|---|---|---|
| `doc.geography.pravend` | geography / settlements | `current` | current-name |
| `doc.politics.pravend-historical-name` | politics / territories | `historical` | founded-by-kaladios（fact）；once-capital（state）；western-economic-center（state） |
| `doc.politics.pravend-succession` | politics / clans | `historical` | osric-surrender（fact）；dey-tir-inheritance（relation） |

地点锚点：帕拉汶德在游戏数据中是 `town_V3`（Pravend，vlandia，clan_vlandia_2），对应 `entity.settlement.town_v3`。

## D4 预期事实总表

1. 巴拉维诺斯由卡拉狄乌斯大帝建立，是第二座重要殖民地（fact）。
2. 巴拉维诺斯随后取代沙拉斯成为卡拉德人的首都（state）。
3. 帝国统治重心东移后，巴拉维诺斯仍是西部经济重镇（state）。
4. “铁壁”奥斯里克入侵时与当地元老协商使该城投降（fact）。
5. 此后传承到奥斯里克旁支的戴·提尔家族手中（relation）。
6. 如今人们称该地为帕拉汶德（fact，现名方向）。

## D5 明确不能从本资料补出的内容

- 具体建立年份；
- 正式游戏实体 ID 的运行时注册；
- 当前动态所有权与战争状态；
- 戴·提尔家族额外人物关系、奥斯里克人物独立条目；
- 帕拉汶德 NPC 身份表达（该批不生成）；
- 未出现在引文中的贸易、人口、军事或行政细节。

**弗雷吉昂（village_V2_3 / Fregian）不纳入本簇**：它只是普通村庄、知名度低，官方中文仅一句描写；既不作帕拉汶德词条内容，也不作方位参照。其 ID 仍保留在通用地点映射（`entity.settlement.village_v2_3`）中供其他场景使用。

## D6 生成结果验收点

| 检查项 | 期望 |
|---|---|
| 条目边界 | 不得把 6 条压成一条“geography 简介”；应近似三文档拆分，或至少地理/政治沿革/传承分层 |
| 实体合并 | 巴拉维诺斯与帕拉汶德识别为同一聚落叙事 |
| 名称方向 | 巴拉维诺斯是历史名称，帕拉汶德是当前名称 |
| 时间层 | 历史沿革与“如今”不得混成同一时间层；historical 不得写成 current |
| 事实数量 | 约 6 条；不得拆成大量碎片，也不得把原文一句拆成多条同义断言 |
| NPC 表达 | 默认为空；本簇唯一授权的是 commoner summary 表达 |
| 年份/新人物/新战争 | 不生成；`must_not_invent` 生效 |
| 来源 | 每条断言可回看官方 XML 引文与 quote_hash |
| 状态 | `review_only=true`、`review_status=pending`、`needs_review`；不自动 approved/canon/compiled/published |
| 知识类型 | rumor 不得变 fact；quote 不足以支撑的 proposition 必须告警或阻断 |
| 弗雷吉昂 | 不得出现在帕拉汶德词条中 |

## D7 本地校验

把 `pravend-cluster/authoring` 与 `pravend-cluster/sources` 复制到临时 workspace 后运行 CLI `validate`；
当前结果 `Valid=true`、`Diagnostics=[]`。CLI `validate` 现同时强制“引文必须能在来源文件内定位”（R1 规则，失败报 `WB-SOURCE-001`）。

---

# E 部分：验收标准与证据

## E1 作者闭环（已通过）

链路：AI 生成待审候选 → 建档（`needs_review`）→ 作者打开档案 → 读取原文 → 编辑保存（CAS）→ 保存后校验 →（编译/导出由权威链 smoke 覆盖）。

- 实测：建档 `era=historical`、`subdomain=territories`；保存 revision 3→4 且编辑保留；校验 `Valid=true`、诊断 0。
- 证据：`docs/evidence/WORLDBOOKSTUDIO-AUTHOR-LOOP-ACCEPTANCE.json`、脚本 `scripts/author-loop-acceptance.ps1`。

## E2 可复制性（已通过）

以第二个官方聚落「加伦／Galend（town_V5）」复跑同一流程，**只换来源与期望事实，不新增代码路径**：
期望事实 3/3、禁止项 4/4（含跨例污染检查）、状态与证据 6/6、锚点 `entity.settlement.town_v5` 解析通过 → **15/15 PASS**；帕拉汶德回归 **19/19 PASS**。

## E3 runtime 锚点（编辑器侧已通过，消费侧未验证）

- 编译产物 `runtime.json` 的条目扩展 `extensions.entityRefs` 会携带规范化锚点 `awake:<kind>:<code>`（kind 白名单 `hero|clan|settlement`）；未识别 kind 编译期 `WB-DOC-003` 阻断。
- 实测：真实本地 Worker 建档文档经 register→select→approve→compile-proof→compile 后，`entityRefs` 含 `awake:settlement:town_v3` 与 `awake:settlement:town_v7`，连续两次运行 manifest 哈希一致。
- 证据：`docs/evidence/WORLDBOOKSTUDIO-RUNTIME-ANCHOR-EVIDENCE.json`、脚本 `scripts/runtime-anchor-evidence.ps1`。
- **未验证**：AWAKE 模组本体在运行期读取并消费这些锚点（属模组本体闭环）。

## E4 语义红队九项（已覆盖）

来源注入提示词、rumor 不得变 fact、historical 不得变 current、多文化视角不得被抹平、
quote 可定位但 proposition 超出 quote 必须告警/阻断、source claim 少于正文命题必须失败、
unknown tier 不得自动变 base、`must_not_invent` 阻止新增人物/年份/战争/正式 entity ID、retry 不得产生重复候选。

相关套件：`Awake.WorldbookStudio.Draft.Tests`（含 `redteam must-not-invent blocks realistic pravend fabrication`、
`redteam claim drift beyond real frigyon quote is blocking`）。

## E5 状态与口径

- 作者可交付的判断依据：`scripts/test.ps1` 全绿 + `scripts/package.ps1` 输出 `TEST: PASS` / `CONTRACT: PASS` + 本地测试包可用。
- 任何候选在人工采纳前一律保持 `review_only=true`、`review_status=pending`、`needs_review`。
- 交付说明必须分开写：**已验证** / **未验证** / 剩余风险。

---

# F 部分：边界与红线

1. **18+ 硬校验先于一切**；年龄不明确或无法验证时按不满足处理；任何情况下不得包含未成年角色的性内容。
2. 候选**不得**自动变成 `approved` / `canon` / `compiled` / `published` / `runtime-ready`。
3. 没有人工采纳的内容不得进入 `create-document`。
4. 世界内文本使用世界观口径：禁止现代心理学术语与 21 世纪网络词，禁止现实式安全词、同意账簿、创伤求助与价值观尾巴。
5. 官方译名优先，但别名/旧称在口语表达层可以存活（例如 canon 现名是“帕拉汶德”，companion 口述仍用“巴拉维诺斯”）。
6. 会话硬边界：不启动游戏、不同步游戏目录、不访问真实云端 Provider/API Key，不修改真实源目录、迁移候选、世界书正文与 AWAKE 模组本体。

---

## 附：前身文档与关联材料索引

| 用途 | 文件 |
|---|---|
| 写作标准（前身） | `docs/worldbook-migration/WORLDBOOK-CALRADIA-KNOWLEDGE-INDEX-V2-20260909.md` |
| 操作手册（前身，随包分发） | `tools/worldbook-studio/新手指引_世界书内容编辑者.md` |
| 样例参照（前身） | `tools/worldbook-studio/tests/fixtures/official-reference/pravend-codex-reference.md` |
| 验收报告（前身） | `docs/worldbook-migration/WORLDBOOKSTUDIO-AUTHOR-LOOP-ACCEPTANCE-20260910.md` |
| 提案规则与经验沉淀 | `docs/worldbook-migration/WORLDBOOKSTUDIO-AI-PROPOSED-RULES-BACKLOG-20260909.md` |
| 可复制性验证 | `docs/worldbook-migration/WORLDBOOKSTUDIO-REPRODUCIBILITY-GALEND-20260910.md` |
| runtime 锚点批次 | `docs/worldbook-migration/WORLDBOOKSTUDIO-RUNTIME-ANCHOR-20260910.md` |
| 语义迁移方法论（另一链路） | `docs/worldbook-migration/WORLDBOOK-SEMANTIC-MIGRATION-METHODOLOGY-v1.2.md` |
