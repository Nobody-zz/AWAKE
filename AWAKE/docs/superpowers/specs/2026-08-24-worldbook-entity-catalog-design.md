# Worldbook Studio B1：实体目录与中文选择设计

_基于用户已确认的 Worldbook Studio 总方案；本批次先经过独立只读审查，再进入实现。_

## 目标

为 Worldbook Studio 增加一套由程序生成、只读、带来源指纹的人物/家族目录，使零基础内容编辑者可以通过中文名称选择具体人物和家族，而不必接触游戏内部代码。战帆属于同一套卡拉迪亚世界的官方 DLC，不是独立世界观；本机未安装 DLC 时，目录只标记“官方 DLC、当前未安装”，不把它降级成另一个世界。B1 不宣称生成王国、文化、聚落的中文选择目录；这些对象只保留已确认的关联代码，待后续补齐权威本地化输入后再单独扩展。B1 不改变现有世界书 v1 契约、运行时读取器、游戏目录或冻结候选。

## 当前事实

- `persona-family-mapping.v1.json` 已有 415 条人物—家族映射，其中 362 条为当前基础游戏精确映射，53 条为官方战帆 DLC 对象映射；后者不是另一套世界，而是本机未安装 DLC 时无法在当前运行时实例化的同世界实体。
- `persona-game-mapping.v1.json` 已有当前游戏人物、家族、王国、文化和主聚落信息。
- 两份映射报告都能可靠提供人物/家族代码与中文人物/家族名；王国、文化、聚落通常只有 ID 或本地化 key，不能直接作为中文选项来源。
- 当前 `profile` 注册表是抽象身份画像，不应承载具体人物和家族。
- `AuthoringEditorModel.BuildCatalog()` 当前只输出 profile、referral 和枚举选项。
- 本工作区不是 Git 仓库，所有修改必须依靠文件哈希、测试和 checkpoint 追踪。

## 方案

1. 新增独立实体目录契约和离线生成器；规范输入只来自两份已核对的 mapping JSON，不在 Studio 启动时扫描游戏目录。
2. B1 只生成 `hero` 和 `clan` 两类规范记录。王国、文化、聚落只作为人物/家族记录中的关联代码，不生成没有可靠中文名的独立作者选项；角色/身份继续由 profile registry 管理。
3. 实体内部 ID 使用稳定规则：`entity.hero.<hero_code>`、`entity.clan.<clan_code>`；代码只允许小写 ASCII、数字、`.`、`_`、`-`，中文名变化不得改变 ID。
4. 每条记录分开保存世界来源、运行时可用性、映射状态和名称来源：
   - `world_source`: `base_game` 或 `official_dlc`；
   - `runtime_availability`: `installed`、`not_installed` 或 `unknown`；
   - `mapping_status`: `exact_base`、`exact_official_dlc_not_installed` 或 `needs_review`；
   - `name_source`: `af_filename`、`mapping_report` 或 `unknown`。
5. 将目录以只读 catalog 接入现有 `BuildCatalog()`，只追加可选字段；不把目录内容写入 authoring 文档，也不让普通编辑器修改目录。B1 的实体选择只用于 catalog/预览，不产生旧 v1 不认识的持久化字段。
6. 生成 manifest 和 diagnostics，记录两个输入 mapping 文件的规范化相对路径、文件 SHA-256、mapping 报告自身 SHA-256、报告生成时间、生成器版本、生成器脚本 SHA-256、规范化规则、记录数量和诊断数量。
7. 生成器先写入同一临时目录，再一次性发布三件套；registry、manifest、diagnostics 共享 `catalog_build_id`。加载器拒绝三件套缺失、版本不一致或 manifest 输入哈希不匹配的组合，并返回可诊断的空实体 catalog。
8. ~~B1 不实现 `hero_ids`/`clan_ids` 运行时权限条件；作者选择结果先作为可预览的实体绑定数据，运行时契约另立后续批次。~~
   **★ 2026-09-25 更新（甲方裁定覆盖）**：上句已作废。B1 的实体选择**现已接上运行时条件**——
   `clan_ids` 与 `hero_ids` 均已在 `awake.worldbook.authoring.v1` 落地为可用的运行时权限条件
   （`clan_ids` 见提交 `8edbff9`；`hero_ids` 同批补，见 `docs/DECISION-20260924-CLAN-BINDING.md`）。
   ⇒ 原文的分批边界**不再成立**；本设计稿其余各条（目录只读接入、不写进 authoring 文档、
   不改 profile/referral registry 等）**仍然有效**。

## 预期目录

```text
docs/mappings/persona-entity/
    generations/<catalog_build_id>/
        entity-registry.v1.json
        entity-registry-manifest.v1.json
        entity-registry-diagnostics.v1.json
    current-pointer.v1.json

docs/worldbook-studio-plan/
    entity-registry.v1.schema.json
    entity-registry-manifest.v1.schema.json
    entity-registry-diagnostics.v1.schema.json
```

生成器固定为 `tools/build_persona_entity_registry.ps1`，输入为工作区内两份 mapping JSON，输出为上述 `docs/mappings/persona-entity/`；Studio 运行时只从已打包的 `schemas/mappings/persona-entity/` 读取副本。`scripts/package.ps1` 必须显式复制 `docs/mappings/persona-entity/` 到包内 `schemas/mappings/persona-entity/`。生成器先在同一父目录创建 `staging-<catalog_build_id>`，写完并复核三件套后，将完整目录发布为 `generations/<catalog_build_id>`，最后只用一次原子替换更新 `current-pointer.v1.json`。加载器先读取 pointer，再只读取该 build 目录，并验证三件套的 `catalog_build_id` 与内部 hash；不会同时读取一个正在更新的平面目录。生成器和打包复制都必须使用 UTF-8 无 BOM、LF 和稳定字段排序；不得读取游戏目录作为 Studio 启动时的隐式副作用。

## 作者可见形态

普通模式显示：

- 中文名称；
- 实体类型；
- 所属家族；
- 王国、文化、主聚落的关联状态（B1 不提供未核对的中文名称）；
- 来源状态；
- “当前游戏可用/仅参考/需要复核”说明。

高级模式才显示：

- `entity_id`；
- `hero_id`、`clan_id` 等原始代码；
- 输入来源和哈希。

## 不变量

- 不修改 `awake.worldbook.authoring.v1` 的字段和枚举。
- 不把实体绑定写进 profile/referral registry。
- 不把实体目录当作世界书正典内容。
- 同一个实体 ID 只能有一个规范记录。
- 标题或中文显示名变更不能改变实体 ID。
- 来源不明确的实体不能显示为当前已安装游戏精确实体；官方 DLC 未安装只表示当前不可用，不表示内容不属于卡拉迪亚。
- 目录三件套缺失、损坏、哈希不匹配或版本不一致时，Studio 仍可使用已有 profile/referral catalog，并返回 `entityCatalogAvailable=false`、稳定的空 `entities` 数组和中文诊断；不能静默使用陈旧或半套目录。
- 任何一条坏记录只隔离该记录并产生错误诊断；只有 registry 根结构、manifest 或 build ID 不一致时才使整个实体 catalog 不可用。
- `catalog_build_id` 由 `generator_version`、generator 脚本原始字节 SHA-256、三份 schema 原始字节 SHA-256、两个 mapping 输入原始字节 SHA-256 和规范化算法版本共同计算；输入 hash 使用原始 UTF-8 文件字节，输出 hash 使用规范化 JSON 字节。
- 生成阶段必须验证 generator/version/schema 指纹；运行时加载阶段验证 pointer、build ID、registry/manifest/diagnostics 互相引用的 hash 和支持的 schema/generator 版本。generator 脚本哈希是构建证据，不要求已发布包内带脚本。

## 验收

- registry、manifest、diagnostics 及三份 schema 均可解析并通过契约校验。
- 生成结果逐行保留 415 个人物映射，并去重得到 82 个家族实体；人物记录计数、362 条基础游戏精确记录、53 条官方 DLC 未安装记录、73 个基础游戏家族、9 个官方 DLC 家族和实体来源分类均有断言，不出现重复稳定 ID。
- hero→clan 关联完整性、同一家族多人物合并、家族中文名冲突、坏记录隔离和 `needs_review` 记录行为均有断言。
- 中文人物和家族搜索可以得到卡片，并显示中文名、实体类型、所属家族或成员、DLC 可用性和来源状态。
- 王国、文化、聚落没有可靠中文名时只显示“关联名称待补充”，不得把本地化 key 当中文名。
- 缺失、冲突、哈希过期、版本不一致或坏记录产生中文诊断，不静默猜测或覆盖。
- missing package artifact、mixed `catalog_build_id`、stale generator/schema version、changed mapping input hash、malformed manifest、pointer 指向不存在目录和发布中断均有失败测试；失败时 profile/referral catalog 仍可用。
- catalog API/前端普通输出不默认暴露英文代码、文件路径、哈希和生成器信息；高级诊断接口才提供这些信息。
- 旧 A1/A5 测试和既有 `99/99` Studio harness 不回归。
- 不启动 Bannerlord，不修改 `Modules\\AWAKE`、`PlayerExports`、`dist` 或冻结候选。

## 后续边界

- 五领域 `geography`、知识引用图、人物/家族权限条件和新版运行时契约不在 B1 内实现。
- B1 通过后再进入 B2 Schema 批次；B2 需要独立的迁移和运行时契约审查，并在补齐权威本地化输入后再扩展王国、文化、聚落作者选项。
