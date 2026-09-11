# PLAN: Worldbook Authoring v1 投影批 — 20260912

> 状态：`prepared_for_review`（E0 计划，未实施）。
> 前置基线：R3 修订批已签收（`docs/review-state/WORLDBOOK-SEMANTIC-R3-REVISION-BATCH-20260912.review.json`，
> 12 档 / 41 claim，独立审查 round 2 APPROVED + user_signoff）。
> 本批目标：把 12 个候选文档投影为 **Studio authoring 工作区内的正式档案**，并清偿全部审查挂账；
> 不编译发布、不同步游戏目录、不改任何 C# 代码（无 BuildId 需求）。

## 0. 输入与事实基础

- 候选：`D:\AWAKE-Archive\...\r3-revision\documents\*.r3.json`（12 档，place_cluster/split_from/layer 齐备）
- 决策记录：`SEMANTIC-R3-DECISION-RECORD-20260911.json`（deviation_log 五条 + claim_authority：doc 为权威版）
- Studio 工作区：`tools\worldbook-studio\workspace\`（authoring / authoring-v1 sidecar / compiled / export 结构齐备）
- 权威门：Authority Gate P0 已落地（register→approve→compile-proof 不可变凭证；未接线必须阻断发布）
- 实体登记表：`docs\mappings\persona-entity\`（current-pointer + 世代哈希，CAS 保护）
- 身份口径：手册 A5/A6（平民/头人/中间人/士兵/贵族 + 知识范围 本地/本国/跨国/秘密 + 详细度 摘要/常规/详细/机密）

## 1. 工作项（按依赖序）

### W1 实体盘点与两分建档（2026-09-12 用户定则修订）
- **实体两分**：游戏锚定实体（有代码对应，正式 ID）vs 语义实体（游戏无对应代码但知识世界自有一致身份，如卡恰尔半岛/拉科尼斯湖/黎明山脉/合儿必特部/阿赫哈克）。
  语义实体**不能因为没有实际参考就不建立联系**——走独立 lore 命名空间（`entity.lore.*`，绝不冒用正式实体 ID），
  在 persona-entity 登记表扩 lore 分区收录（数据变更，需过登记表 schema；编译器对登记表内 ID 即正常解析折入 keywords，
  对未登记 ID 宽容返回空——两种情况都不阻断编译，已核 `RuntimePackageCompiler.LookupEntityAnchorNames`）。
- 语义实体同一性：名称归一 + 所指一致 → 同一 lore ID，**人工映射、禁止自动猜测**；
  游戏侧交叉引用（卡恰尔半岛 ↔ town_S1 官方描述提及）作为元数据参照，不改变 lore 身份。
- 逐 claim 盘点产出 `ANCHOR-BINDING-PLAN.json`：每档 → 实体（game-anchored 或 lore）+ 依据；
  建档前未定者沿用 `authoring_provisional` 占位。

### W2 正式建档（12 档）
- 经 Studio 保存链写入 `workspace\authoring`（非直接写盘，走 API/保存链以获得审计事件与稳定内部编号）；
- 每档：标题=候选 title、domain/subdomain 按 taxonomy v1、客观事实=claims（fact/relation/state）、
  interpretation/rumor 段按各自类型建档、NPC 表达=expression spans（layer + 身份）、
  元数据带 place_cluster / split_from / decision_record 引用；
- 全部档案 `needs_review`，**不自动 approve**。

### W3 子引文细化
- 现状：origin 绑整 variant 引文；本批按 B6"引文粒度细于断言"逐 claim 定位子引文，
  生成新 locator + quote_hash（对应 v1 variant 内字符区间），替换整段引用；
- 同步修：a48e49e8 predicate 正式落位、kach-tales 框架句（首句并入首 span 或单列 framing claim 需裁定）、
  claim 元数据现代措辞统一（"考古与移民史佐证"→世界观内表述）。

### W4 grants/profile/scope 映射
- 每个表达段按 A6 口径落：身份（profile_id 取自 persona-entity 登记表；不在表中的身份——如"北行学士/跑船人"——
  映射到最近通用身份或触发**登记表扩充裁定**）、layer（rumor/summary/detail/secret）、scope、min_detail；
- 落守则红线：宁可窄授权，不默认"所有人"。

### W5 账目清理（迁移文档层，不动 Studio）
- worksheet 孤儿 `origin.r3.*`（14 个孤儿 claim）清理；der-04 action_map 归属修正（57e81a17 归 der-01/03 拆分）；
- SUPERSEDED-MAP 与 action-queue 对账基线统一为 22。

### W6 结构验证与证据
- Studio 本地校验（`/api/validate` 或 CLI）0 blocking；
- 三向闭合移植校验：档案内 assertion/expression 与 R3 claim 一一对应（复用 validate 思路对 authoring 产物再验）；
- 证据目标 E2；产物清单 + 校验报告落 `docs/evidence/`（该目录 gitignore，报告摘要入本计划追记）。

## 2. 明确不做

- 不 approve 任何档案（审批是用户在 Studio 内的人工动作，属 Authority Gate 主权）；
- 不编译进 `compiled\`、不 export、不发布、不替换 010 试点包、不同步游戏目录；
- 不改 C#/Studio 工具代码；不动 v1 源文、r2 候选、归档区；
- 对象簇嵌套裁定：倾向"区域档与地点档靠锚点平级关联，不做嵌套容器"——本批按此执行，异议在审查轮提出。

## 3. 风险与开放问题

| # | 风险 | 缓解 |
|---|---|---|
| 1 | 四个地理对象无游戏锚定实体 | W1 三分处理：game-anchored 挂靠 / **lore 命名空间建档**（登记表扩 lore 分区，编译器已核实容错）/ 暂缓占位 |
| 2 | 登记表可能不含"学士/跑船人/旅人"等表达身份 | W4 触发登记表扩充裁定，宁缺勿造 |
| 3 | 子引文细化工作量最大（41 claim × variant 定位） | 按 5 源条目分五小组提交，组间可独立校验 |
| 4 | Authority Gate 保存链对批量写入的会话/审计要求 | 建档走官方保存链；如遇批量限制，分批提交 |

## 4. 流程

本计划 → grill-me 拷问 → 独立只读审查 → 你签收 → 实施（W1→W6）→ 证据汇报。
