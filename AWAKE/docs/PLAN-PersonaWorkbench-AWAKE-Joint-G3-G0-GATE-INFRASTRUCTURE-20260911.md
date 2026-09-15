# G3-G0：Persona 门禁基础设施

> 状态：`PREPARED / PENDING_INDEPENDENT_REVIEW`
>
> 本批次只建立后续 G3-A0/G3-A 使用的外部、可验证门禁；不实施 Persona runtime，也不修改 G3-A0/A 的业务工具或源码。

## 目标

消除 G3-A0 的自举循环：后续批次不得使用自己写出的 verifier 来证明自己的 scope hash、approval、lease 或 write-set zero-overlap。

## 精确写集

- `_houkai_merge/AWAKE/tools/persona-awake-joint/verify-g3-scope-authority.ps1`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/verify-contract.ps1`
- `_houkai_merge/AWAKE/docs/persona-awake-joint-g3-authority-record.v1.schema.json`
- `_houkai_merge/AWAKE/docs/persona-awake-joint-g3-g0-scope.v1.json`
- `_houkai_merge/AWAKE/docs/persona-awake-joint-g3-a0-check-matrix.v1.json`
- `_houkai_merge/AWAKE/docs/fixtures/persona-awake-joint/PWB-AWAKE-017-g3-scope-authority/input.json`
- `_houkai_merge/AWAKE/docs/fixtures/persona-awake-joint/PWB-AWAKE-017-g3-scope-authority/expected.json`

不在写集：`AWAKE/src/*`、`AWAKE.Tests/*`、`verify-runtime-bridge-static.ps1`、`verify-g3-a0-scope.ps1`、所有 Persona Storage 文件、`ModuleData/*`、`dist/*` 与游戏目录。

## 必须实现的不可变规则

1. verifier 位于 G3-G0 已关闭 lease 的写集，且不属于被验证的 A0/A write set；
2. raw UTF-8 SHA-256 绑定 scope、approval、lease 三者；approval 与 lease record 必须经同一 schema 解析；
3. A 的结构化 predecessor 必须绑定 A0 scope path/hash、approval verdict/path、lease ID/path/status=`released`；
4. 规范化路径、重复路径和 A0/A write-set 交集必须 fail-closed；
5. `persona-awake-joint-g3-a0-check-matrix.v1.json` 固定每个 check ID 的层级（S0/A/B）、expected disposition 与 expected failed-check IDs；G3-B-only 失败不可计入 A0 blocking；
6. G3-G0 不定义 G3-A runtime evidence schema 或 trace schema。`PWB-AWAKE-010/011/019/020` 的 case-level evidence contract 是 G3-A 的独立预实施门禁，必须在 G3-A 获得 execution approval 前重新建立、独立审查并 hash-pin；本批不以未实现的 runtime evidence 契约阻塞 authority infrastructure。

## 验收与边界

- `G3-G0-001`：伪造 hash、缺 approval、active/released 状态错误、重复或交集 write set 均 reject；
- `G3-G0-002`：合法 A0 predecessor 才允许 A scope preflight pass；
- `G3-G0-003`：A0 exact check matrix 将 Storage wiring 标记为 G3-B，不计入 A0 blocking；
- 最高 E2；不构建 runtime、不启动/同步游戏、不创建 A0/A approval 或 lease。

## 后续顺序

`G3-G0 approved + signed-off + lease released -> G3-A0 independent review -> G3-A0 approval/lease -> G3-A`。

G3-G0 的独立审查只审查本计划、scope 和现有 G3-S0 verifier 的可复用契约；不得把 A0/A implementation 提前纳入本批。
