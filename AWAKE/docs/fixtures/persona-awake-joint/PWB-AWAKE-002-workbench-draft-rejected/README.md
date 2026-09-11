# PWB-AWAKE-002-workbench-draft-rejected

`source.document.status` 现在确实是 `draft`，并且只声明一个被选中的 draft 输入；未把未选中的 disabled 变体伪装成同一次 operation 的第二个错误。

期望错误采用当前 adapter 的稳定代码 `persona.workbench_not_approved`。该负例通过 `source.document.status=draft` 触发 promotion 拒绝，不依赖顶层 legacy `workbench` 别名。
