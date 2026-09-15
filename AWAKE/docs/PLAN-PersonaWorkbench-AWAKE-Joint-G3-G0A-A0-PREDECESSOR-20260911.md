# G3-G0A：A0 已关闭前序的独立可验证门禁

状态：`PREPARED / PENDING_INDEPENDENT_REVIEW`

## 目标

为 G3-A 提供一个独立、fail-closed 的只读验证器，验证已完成的 G3-A0 scope、approval 和 released lease。它不改既有 `verify-g3-scope-authority.ps1`，从而不改变 G0C completion record 固定的 verifier hash。

## 精确写集

- `tools/persona-awake-joint/verify-g3-a0-predecessor.ps1`
- `docs/persona-awake-joint-g3-g0a-scope.v1.json`

approval、lease 与报告是 detached records 或临时 artifacts，不属于 runtime write set。

## 不变量与验收

- 读取 G3-A scope 的 `a0Predecessor`；缺字段、逃逸路径、hash 不匹配、A0 approval 未 approved/未签收、A0 lease 非 released、lease id 不匹配、A0 write set 不等于 scope 时均 reject；
- 正向输入为当前 A0 records，输出 `pass/0`；每个关键前置错误均能以临时副本输入得到 `reject/10`；
- 不读取或写入 Persona Storage、不改 `src`、不构建、不同步、不启动游戏；最高证据 E2。

## 后续

G0A 通过并关闭 lease 后，G3-A scope 才可绑定其 report digest；这会形成一个新的 G3-A revision，必须独立复审并重新由用户签收后才可发 G3-A runtime lease。
