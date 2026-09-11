# Worldbook Studio B1 Entity Catalog Checkpoint

- `task_id`: `WORLDBOOK-STUDIO-B1-ENTITY-CATALOG-20260824`
- `batch_id`: `worldbook-studio-b1-entity-catalog-20260824`
- `status`: `implemented_and_offline_verified`
- `scope`: 为中文世界书编辑器提供人物—家族作者侧目录和预览；保留 `awake.worldbook.authoring.v1`，不实现人物/家族运行时权限结算。
- `execution_lease`: none；本批次没有启动 Bannerlord，不同步游戏目录、`PlayerExports`、dist 或冻结候选。
- `world_boundary`: 战帆是卡拉迪亚世界的官方 DLC；本机未安装只表示当前没有对应运行数据，不是独立世界。目录将其标记为 `official_dlc` / `not_installed`，普通界面显示“官方 DLC，当前未安装”。

## Implementation

- 生成 `persona-entity` 目录：415 人物、82 家族；其中 362/73 为当前基础游戏对象，53/9 为官方 DLC 当前未安装对象；生成器诊断 0 error、6 warning，warning 为部分 DLC 家族中文名待补充。
- 生成稳定 pointer 和单一 current generation；Core 只读取 pointer 指向的 generation，校验四份 Schema、BuildId、manifest、registry、diagnostics 和三份文件 hash。
- 普通编辑器只接收中文名称、类型、家族名称、成员名单和可用性；不输出 `entity_id`、`hero_code`、`clan_code`、路径或 hash；人物/家族权限保存明确留到后续契约批次。
- Web 提供中文搜索、人物/家族筛选、家族成员预览和目录不可用时的中文降级提示；不扫描游戏目录。
- 发布脚本只打包 pointer 和 pointer 指向的单一 generation，不携带历史 generation 或 `.tmp` 临时文件。
- 修正文档语义：`persona-game`、`persona-family`、`war-sails-reference` 和战帆候选审计均明确“战帆为同一世界官方 DLC、当前未安装不是映射冲突、参考资料不自动成为 AWAKE 正典”。

## Verification

- `tools/worldbook-studio/scripts/test.ps1`：Release 构建成功，`PASS: Worldbook Studio harness (101/101)`。
- `tools/worldbook-studio/scripts/package.ps1`：自包含 `win-x64` 发布成功，`release-check` 通过。
- 发布目录：`tools/worldbook-studio/artifacts/WorldbookStudio`；ZIP：`tools/worldbook-studio/artifacts/WorldbookStudio-win-x64.zip`。
- 发布包 pointer 目标存在，current generation 三件套齐全，临时文件数为 0；ZIP 内实体目录只包含 pointer 和当前 generation 三文件。
- 发布包 HTTP 冒烟：`/api/health` 返回成功；`/api/editor-catalog` 返回目录可用、415 人物、76 个有中文名称的家族实体、56 个官方 DLC 未安装实体，普通 JSON 不包含 `entity.hero.*`、`hero_code` 或 `clan_code`。
- 前端 `studio-entity-catalog.js` 与 `index.html` 内联脚本语法检查此前已通过；本批次未宣称浏览器 DOM 实机渲染通过。

## Code debt audit

- 证据：`docs/evidence/worldbook-studio-b1-entity-catalog-debt-20260824.md`。
- 结论：`passed_with_advisories`；确认性死代码 0、确认性重复权威路径 0、确认性性能缺陷 0。
- 主要提示：目录请求的冷路径重复读取/hash、前端多字段别名兼容、生成器长语句、package pointer 边界防御、规模断言的后续维护成本。
- B1 收尾不做无行为重构；后续以小任务处理缓存失效、恶意 pointer fixture 和 canonical wire shape。

## Not included

- 不写入新的 authoring 人物/家族权限字段。
- 不做具体人物/家族的运行时知识权限结算。
- 不做王国、文化、聚落目录。
- 不修改 AWAKE 运行时读取器、游戏目录、`PlayerExports`、dist、冻结候选或旧四版世界书。

## Next batch

进入独立 B2 前，先由开发者确认是否需要把人物/家族选择写入新的 authoring 契约；B2 再单独审查权限保存、具体人物视角和运行时结算边界。
