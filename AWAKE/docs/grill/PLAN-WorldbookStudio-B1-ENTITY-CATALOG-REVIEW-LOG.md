# Plan Review Log: Worldbook Studio B1 实体目录与中文选择基础

Act 1 complete — plan locked from the previously user-approved Worldbook Studio design. Independent read-only review is required before implementation.

MAX_ROUNDS=3

## Round 1 — Independent review

`VERDICT: REVISE`

主要问题：原计划把人物行误当成五类实体全集；没有锁定王国/文化/聚落的中文名称来源；来源状态混用了 `native_exact`、`war_sails_runtime_exact` 等不同语义；未规定 mapping 文件新鲜度、生成器指纹、三件套原子发布、坏记录隔离和 catalog 缺失降级；B1 的 415 计数验收口径不够精确。

## Claude's response

已收窄 B1 为“人物/家族中文目录基础”，并纳入：官方 DLC 与基础游戏的世界来源/运行时可用性分离、明确 ID 规范、mapping 输入文件哈希、生成器版本/脚本哈希、三文件共享 `catalog_build_id`、临时目录原子发布、坏记录隔离、整个 catalog 失效时的空数组降级，以及不把王国/文化/聚落本地化 key 当中文名称。五类实体完整目录延期到补齐权威本地化输入后的独立批次。

## Round 2 — Independent review

`VERDICT: REVISE`

主要问题：打包脚本目前不会复制计划中的实体目录；生成器文件尚未存在且计划没有拆出其先决步骤；“三件套一次性发布”没有具体的 Windows 原子读取协议；指纹校验层次、失败测试、家族去重/关联完整性和 DLC 可用性边界仍不够明确。

## Claude's response

已补充 generation 目录与 `current-pointer.v1.json` 的发布协议；明确先实现生成器和三份 schema，再修改 `scripts/package.ps1`；固定输入/输出原始字节哈希、generator/schema 版本分工；将 415 人物、82 家族、73 基础家族、9 DLC 家族、hero→clan 完整性、混合 build、陈旧指纹、指针断链、发布中断和 profile/referral 降级加入阻断测试。

## Round 3 — Independent review

`VERDICT: APPROVED`

复审确认：B1 已收窄为人物/家族中文目录；战帆按同一世界的官方 DLC 建模；来源与运行时可用性分离；mapping、generator、schema 指纹和 generation pointer 发布协议明确；打包复制、失败降级、混合 build、陈旧输入、指针中断、家族去重与旧 v1/游戏目录边界均已纳入验收。未发现剩余 material blocker。
