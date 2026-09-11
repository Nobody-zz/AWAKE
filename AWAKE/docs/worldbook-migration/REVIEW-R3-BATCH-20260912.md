# R3 修订批质量审阅 — 2026-09-12

> **✅ 已签收（2026-09-12，user-26811）**：正式审查状态见 `docs/review-state/WORLDBOOK-SEMANTIC-R3-REVISION-BATCH-20260912.review.json`
> （round 1 REVISE → round 2 APPROVED → user_signoff=true）。下一批次：Authoring v1 投影批，工作清单如下节。

> **终态更新（2026-09-12 确认复审）**：独立审查者对修订后 12 档完成确认复审，**最终 VERDICT: APPROVED**。
> 两处 P1 确认实质修复且无新增源文重合；deviation_log 五条确认完整覆盖首轮全部偏差；
> P2-3/4/5 以 claim_authority 裁定为前提挂账投影批次，处理方式被认定正当。
> 复审新增 P3 两项：a48e49e8 predicate 曾为空（已当场修复为"地形上"，生成器变量残留所致）；
> kach-tales 首句（offset 0-17）无 span/claim 覆盖——两项均已列入投影批次清单。
> 本批 12 档（41 claim）连同 SUPERSEDED-MAP 与决策记录作为通过基线，待用户签收后进入 Authoring v1 投影批次。
> **投影批次工作清单（含审查遗留）**：正式建档、实体锚点绑定、grants/profile/scope 映射、place_cluster→referralIds、
> 子引文细化、claim 元数据现代措辞统一、worksheet 孤儿 origin 清理与 der-04 action_map 错位修正、
> a48e49e8 predicate 正式落位、kach-tales 框架句绑定、对象簇嵌套裁定。

> 审阅对象：`r3-revision\documents\` 12 个拆分文档（5 源条目，41 条 claim）。
> 审阅依据：`tools-r3\WRITING-RULES.md`（用户三主旨 + 手册 15 条）+ 机器校验（validate-r3-batch.ps1 全项 PASS）。
> 本审阅为作者侧自查，**不替代独立只读审查**；结论：**有保留通过（PASS WITH NOTES）**。

## 一、逐条主旨符合性

### 1. 分层结构（中立内核 + 身份表达层）— 符合
- 每档均为"中性事实段（无 layer）+ 身份表达段（layer=detail/summary/rumor）"结构；同 span 无层级混装、无视角混装（机器校验确认）。
- 41 条 claim 全部落位：40 条进正文、1 条（der-reg，纯注册元数据）按规则入 metadata_claims 豁免正文。
- 学识梯度落地：库赛特远距离判断标 `summary`、船人怪谈标 `rumor`、学士/兵志解读标 `detail`；低了解度身份（库赛特"兴致有限"）未代为补全其知识。

### 2. 中世纪用词（一票否决项）— 正文符合，元数据有残留
- 正文零现代科学词（首稿的"铁质氧化层/有机质/巧合性对应"已全部清除，改为"锈气/沤出来的/凑不到一处"口径）；防搬运 10-gram 门 12 档全过。
- **残留（记录在案）**：claim 元数据的 object 字段仍有草稿继承的现代表述（如 sara-land 的"考古与移民史佐证"）。元数据属编辑审计层、不进 NPC 视野，但按守则口径应统一——**遗留至投影批次与子引文细化同批处理**。

### 3. 手册规则抽查
- B4 条目边界：5 源条目拆为 12 档，拆分粒度按调取场景（地理与生计 / 归属沿革 / 文化叙事），瘦文档已并回主题；每档带 `split_from` + `b4_split_note`。
- B8 生命周期：9 条被取代的 r2 claim 记入 `SUPERSEDED-MAP-20260912.json`，未删除未改写。
- A4 事实写法：内核段零"据说/可能"（模糊语只在 rumor/detail 层）；一句一事实（"因为…所以…"句式已拆）。
- B2 时间层：归属沿革为 historical、"眼下属谁"保持 unresolved，无 historical 写成 current。
- B9 禁入清单：未新增人物/年份/实体 ID；游戏锚点仅作决策记录交叉引用，未进正文正典。

## 二、审阅发现的问题（按严重度）

| # | 级别 | 问题 | 处置 |
|---|---|---|---|
| 1 | P3 | claim 元数据 object 仍有现代措辞残留（草稿继承） | 投影批次统一改写，本批挂账 |
| 2 | P3 | origin 仍绑定整 variant 引文（引文粒度规则未满足） | 已在每档 `quote_refinement_pending` 声明，投影批次细化 |
| 3 | P3 | 表达段 layer 有标注但 grants/profile 未映射（依赖实体登记表） | 已在 layer_note 声明，投影批次落位 |
| 4 | P4 | 拉科尼斯湖"运费三倍/蓝路/船坞扩建"等素材因超出 22 条已批 claim 范围未入稿 | 记为下一批 claim 候选，不悄悄丢弃 |
| 5 | P4 | dawn-taboo 第一版触发 layer 规则告警（relationship 层标 layer），校验器规则已按 B7 精神校准为"非中立视角可标" | 已闭环 |

无 P0/P1/P2 级问题。

## 三、批次账目

- 源条目 5 → 拆分文档 12（拉科尼斯湖 2 / 沙拉斯湾 2 / 卡恰尔半岛 3 / 黎明山脉 3 / 德里亚特 2）；
- claim 41 = 保留 r2 19 + r3 新增 22（与 R3 修订草稿的 22 条一一对应，ID 未变）；23 条动作全部执行；
- 机器校验：12/12 PASS（状态门、闭合、同质性、layer 规则、标记唯一、10-gram 防搬运）；
- 产物位置：归档区 `r3-revision\documents\`（不覆盖 r2）；r2 候选与源快照未动。

## 四、下一步（进审查流程前）

1. 本报告 + 12 档产物送**独立只读审查**（非作者本人），预期一轮 REVISE/APPROVED；
2. 审查通过后用户签收 → Authoring v1 投影批次（届时处理：文档正式建档、子引文细化、grants/profile 映射、元数据措辞统一）；
3. 其余 25 个 worksheet 的 claim 级处理在五候选闭环后评估（不提前扩范围）。

## 五、过程中的工具与流程资产

- `tools-r3\WRITING-RULES.md`：表述守则（后续批次写作依据）；
- `tools-r3\generate-r3-batch.ps1`：数据驱动的拆分档生成器（span 标记定位、layer 标注、split_from 生命周期）；
- `tools-r3\validate-r3-batch.ps1`：批量校验器（本机无 Python 3，原计划的 Python validator 以 PowerShell 实现）；
- `SUPERSEDED-MAP-20260912.json`：被取代 claim 生命周期映射。
