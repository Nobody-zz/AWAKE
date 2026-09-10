# Codex 参考样本簇：帕拉汶德

> **已并入唯一权威文档**：`docs/worldbook-migration/WORLDBOOKSTUDIO-AUTHOR-HANDBOOK-v3-20260910.md`（D 部分，2026-09-10）。
> 本文保留为样例参照与历史记录；内容以 v3 手册为准。

> 用途：作为 Worldbook Studio Quick Authoring 真实生成结果的验收参照。  
> 性质：全部 `status: reference_only`，不是正典，不是迁移候选，不写入游戏目录，不得进入 `approved/canon/compiled/published`。  
> 资料来源：Bannerlord 官方中文本地化 XML（`game_snapshot`）。  
> 遵循：`WORLDBOOK-CALRADIA-KNOWLEDGE-INDEX-V2-20260909.md` 的五领域与条目边界拆分原则。

## 1. 簇结构与文件

一个作者主题（帕拉汶德）不压缩成一条巨型简介，而是拆成三条 authoring 文档，避免“整段历史塞进单一 geography intro”：

```text
official-reference/pravend-cluster/
├── authoring/
│   ├── geography/
│   │   └── pravend.yaml                     # doc.geography.pravend
│   └── politics/
│       ├── pravend-historical-name.yaml     # doc.politics.pravend-historical-name
│       └── pravend-succession.yaml          # doc.politics.pravend-succession
└── sources/
    ├── source.bannerlord.sandbox.settlements.history.yaml
    └── pravend-official-cns-extract.txt
```

## 2. 来源登记（game_snapshot）

- 官方文件：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\Languages\CNs\std_settlements_xml-zho-CN.xml`
- 文件 SHA-256：`46df7c77c4fa184d1c67c3dc5881f1098c3b6842ae051a188debd41af9d95aeb`
- 摘录文本 `pravend-official-cns-extract.txt` SHA-256：`2f06ef6ab1029bdb6a5920c94cc9adfa6a518a98f106c1e5d7e4710d0172f9c7`
- 摘录只保留帕拉汶德自身段落（原行 23）；行 259（弗雷吉昂）已移除。

| source_id | locator | 内容 | quote_hash |
|---|---|---|---|
| `source.bannerlord.sandbox.settlements.history` | `std_settlements_xml-zho-CN.xml:23` | 巴拉维诺斯建城至帕拉汶德改名全段 | `f4b24c13…` |

行 23 全段被不同断言引用为多个子引文，子引文 quote_hash 各不相同（如 `59c8b208…`、`8888a1ae…`、`89d23bed…`），校验器按引文内容独立核验，不允许“整段引用冒充局部支撑”。

**弗雷吉昂（village_V2_3 / Fregian）不纳入本簇**：它只是普通村庄、知名度低，官方中文仅有一句描写；既不作为帕拉汶德词条内容，也不作方位参照。其 ID 仍保留在通用地点映射（`entity.settlement.village_v2_3`）里，供其他场景按需使用。

## 3. 三文档拆分

| 文档 ID | domain / subdomain | era | 收录内容 | 收录断言 |
|---|---|---|---|---|
| `doc.geography.pravend` | geography / settlements | `current` | 现名帕拉汶德、别名巴拉维诺斯 | current-name |
| `doc.politics.pravend-historical-name` | politics / territories | `historical` | 旧称沿革：建城、首都、经济重镇 | founded-by-kaladios（fact）；once-capital（state）；western-economic-center（state） |
| `doc.politics.pravend-succession` | politics / clans | `historical` | 易主与传承：奥斯里克协商投降、戴·提尔旁支 | osric-surrender（fact）；dey-tir-inheritance（relation） |

地点锚点：帕拉汶德在游戏数据中是 `town_V3`（Pravend，vlandia，clan_vlandia_2）。自 2026-09-10 起实体目录已支持 `settlement` 命名空间并生成通用地点映射（390 条：53 城镇 / 67 城堡 / 270 村庄），对应条目为 `entity.settlement.town_v3`；本簇文档的 `entity_ids` 仍保持文档级锚点语义。

## 4. 预期事实总表（跨三文档）

1. 巴拉维诺斯由卡拉狄乌斯大帝建立，是第二座重要殖民地（fact）。
2. 巴拉维诺斯随后取代沙拉斯成为卡拉德人的首都（state）。
3. 帝国统治重心东移后，巴拉维诺斯仍是西部经济重镇（state）。
4. “铁壁”奥斯里克入侵时与当地元老协商使该城投降（fact）。
5. 此后传承到奥斯里克旁支的戴·提尔家族手中（relation）。
6. 如今人们称该地为帕拉汶德（fact，现名方向）。

## 5. 明确不能从本资料补出的内容

- 具体建立年份；
- 正式游戏实体 ID 的运行时注册；
- 当前动态所有权与战争状态；
- 戴·提尔家族额外人物关系、奥斯里克人物独立条目；
- 帕拉汶德 NPC 身份表达（本轮不生成）；
- 弗雷吉昂相关内容：不纳入帕拉汶德词条（重要性低；如需使用走通用地点映射，不写进本簇）；
- 未出现在上述引文中的贸易、人口、军事或行政细节。

## 6. Quick Authoring 生成结果的验收点

| 检查项 | 期望 |
|---|---|
| 条目边界 | 不得把 6 条压成一条“geography 简介”；应近似三文档拆分，或至少地理/政治沿革/传承分层 |
| 实体合并 | 巴拉维诺斯与帕拉汶德识别为同一聚落叙事 |
| 名称方向 | 巴拉维诺斯是历史名称，帕拉汶德是当前名称 |
| 时间层 | 历史沿革与“如今”不得混成同一时间层；historical 事实不得写成 current |
| 事实数量 | 约 6 条；不得拆成大量碎片，也不得把原文一句拆成多条同义断言 |
| NPC 表达 | 默认为空；本簇唯一授权的是 commoner summary 表达 |
| 年份/新人物/新战争 | 不生成；`must_not_invent` 生效 |
| 来源 | 每条断言可回看官方 XML 引文与 quote_hash |
| 状态 | `review_only=true`、`review_status=pending`、`needs_review`；保持 `reference_only`，不自动 approved/canon/compiled/published |
| 知识类型 | rumor 不得变 fact；quote 不足以支撑的 proposition 必须告警或阻断 |
| 弗雷吉昂 | 不得出现在帕拉汶德词条中（已从参考簇与摘录移除） |

## 7. 验证方式

将 `pravend-cluster/authoring` 与 `pravend-cluster/sources` 复制到临时 workspace 后运行：

```text
dotnet run --project src\Awake.WorldbookStudio.Cli\Awake.WorldbookStudio.Cli.csproj --configuration Release -- validate --workspace <tmp> --schema-root <docs\worldbook-studio-plan>
```

当前结果：`Valid=true`，`Diagnostics=[]`（2026-09-09，脚本 `_tmp\validate-pravend-reference.ps1`）。

> 说明：CLI `validate` 现同时强制“引文必须能在来源文件内定位”（R1 规则，`WB-SOURCE-001`）。本簇四条引文（含子引文）均已逐字命中官方中文摘录；若有人为改动导致无法定位即失败。

## 8. 当前状态

- 三文档骨架 + 来源登记已建为**离线参考夹具**并通过 schema/taxonomy/source 校验；
- 尚未用真实本地 Worker 生成任何 AI 候选；本簇只定义“生成结果应该长什么样、不该补造什么”；
- 2026-09-10：撤出弗雷吉昂；新增通用地点实体映射（`settlement` 命名空间，390 条）。
- 未修改任何迁移候选、世界书正文、Runtime 或游戏目录。
