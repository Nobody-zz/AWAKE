# G3-G0C：不可变完成快照

> 状态：`PREPARED / PENDING_INDEPENDENT_REVIEW`

## 目的

为已实施且已释放的 G3-G0 建立单一不可变 completion record，供 G3-A0 作为 predecessor 引用。它替代可变 lease 文件的“最新记录”语义，不重写 G3-G0 历史记录。

## 最小写集

- `_houkai_merge/AWAKE/docs/persona-awake-joint-g3-g0-completion.v1.json`
- `_houkai_merge/AWAKE/docs/persona-awake-joint-g3-g0-completion.v1.schema.json`
- `_houkai_merge/AWAKE/tools/persona-awake-joint/verify-g3-scope-authority.ps1`
- `_houkai_merge/AWAKE/docs/persona-awake-joint-g3-g0c-scope.v1.json`
- `_houkai_merge/AWAKE/docs/fixtures/persona-awake-joint/PWB-AWAKE-021-g3-g0-completion/input.json`
- `_houkai_merge/AWAKE/docs/fixtures/persona-awake-joint/PWB-AWAKE-021-g3-g0-completion/expected.json`

## 不变量

- G3-G0C 由已完成 G3-G0 的 detached authority 预授权；G0C approval/lease 不属于 G0C 自身 write set，必须绑定 G0C scope raw hash、G0 predecessor identity 和用户签收；
- record 固定 G3-G0 scope hash `657C2AFD40C0C1C86467AC6F058B7D20E6CE3160C22E470677197A35E21B7DA5`、released lease `g3-g0-20260911-102613`、approval 的 user exception/signoff 与 verifier SHA-256；
- record 的 digest domain 固定为 raw UTF-8 record 内容去除 `recordSha256` 字段后的 canonical JSON；schema、字段集合、路径和未知字段均由 verifier fail-closed 校验；
- A0 verifier 必须验证 completion record 本身的 raw SHA-256、所有固定字段与 release 状态；A0 只绑定 completion record，不再绑定可变 G0 lease；后续 G0 lease 的变动不得使该完成快照失效；
- completion record 缺失、字段 hash 不符、release 不符或输入路径不等于 A0 scope 声明时 fail-closed；
- G3-A0 scope 在 G0C 成功后更新为 completionPath + completionSha256；更新前不得创建 A0 approval/lease，也不得宣称 A0 predecessor ready；
- 不修改 `AWAKE/src`、Storage、static runtime verifier、游戏目录、同步或 BuildId。

## 验收

- 正向：A0 scope 绑定的 completion record 全字段匹配时 predecessor check pass；
- 负向：篡改 completion hash、scope hash、verifier hash、lease ID/status 或 CLI path 任一项均 reject；
- E2，仅离线工具证据。
