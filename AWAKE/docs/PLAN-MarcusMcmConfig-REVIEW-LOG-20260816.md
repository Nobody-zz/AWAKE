# AWAKE · Marcus MCM 配置审查

## Round 1

- 使用公开 SDK API，不反射内部。
- 状态读取失败时降级为 Offline，不抛异常。
- 一键同步仅在 host 为 `FrameworkHost` 时执行，否则引导 AI 设置台。

`VERDICT: APPROVED`
