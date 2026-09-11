# PWB-AWAKE-016-unsupported-rule-rejected

该夹具包含已启用的 Workbench 规则覆盖。`definition-v1` 当前没有正式 rule 字段，因此适配器必须返回 `persona.rule_unsupported_for_definition_v1`，而不是把规则静默丢弃或拼进 legacy prose。
