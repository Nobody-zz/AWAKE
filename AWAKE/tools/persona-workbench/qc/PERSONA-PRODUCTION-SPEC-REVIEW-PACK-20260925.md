# 角色卡制作规范独立审查包清单

状态：`READY_FOR_HUMAN_REVIEW`。本包用于候选规范 A–F 对抗性验收，不是角色卡内容审核，也不代表规范已通过。

## 每位评审者只收到这五份文件

| 文件 | SHA-256 |
|---|---|
| `PERSONA-CARD-PRODUCTION-SPEC.zh-CN.md` | `6c0ee1b0fbfe7400b18e112071aa954a26d555beec6e5ab893bbb80709f64098` |
| `PERSONA-PRODUCTION-SPEC-REVIEW-FORM-20260925.md` | `c841f243ddb526c8a972b8d5619733b9c510c14cd4dcf030461a76d909610452` |
| `PERSONA-PRODUCTION-SPEC-REVIEW-FIXTURES-20260925.md` | `2c6d1d6b19bdb4e3d467e3fa9f8c1d71a9649ca76ba7f593b846a5295cea2199` |
| `PERSONA-AUTHORING-RECORD-TEMPLATE.zh-CN.md` | `852a69ba02cc7005a59acda69345bec355ad02e5253bef5ff4cf9aa907b339fc` |
| `PERSONA-BLIND-REVIEW-TEMPLATE.zh-CN.md` | `dce85e854aad631cba6f712f3ab84f875aee495d2b980e569448bd303817d032` |

上表哈希按 UTF-8 文件字节计算，先将 CRLF 换行统一为 LF，其余字节保持不变。复制前逐个校验哈希；不一致则停止，不得使用混合版本。每位评审者各自收到相同的五份文件并独立填写一份审查表。评审期间不得发送作者口头解释、红测日志、模型诊断或另一位评审的判断。

## 保管与签收

- 两名评审者必须是未参与候选规范编写、也未参加这些夹具所涉及角色卡创作的**人类**。
- `PERSONA-PRODUCTION-SPEC-REVIEW-ANSWER-KEY-20260925.md` 由作者单独保管；两位评审者均签署并锁定各自审查表后，才可逐项核对。
- 本地 Qwen/Ministral 模型诊断报告和 `PERSONA-PRODUCTION-SPEC-REDTEAM-LOG-20260925.md` 不属于盲审输入。
- 任一已知缺陷漏报、简单有据卡不能在声明范围内通过资格、无据超范围主张被放行、负对照仅因句式相似被判缺陷、或结论需要作者解释才能得出，均不通过；保留两份原表并修订后重审。

当前正式制作入口和 `run-card-gates.ps1` 尚未切换；只有两份人类审查表均符合答案钥匙、所有差异解决、且用户签收后，才进入入口与门禁迁移。
