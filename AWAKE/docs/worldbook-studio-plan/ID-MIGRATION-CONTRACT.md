# Worldbook Studio MVP ID and Migration Contract

## ID namespaces

- `doc.<domain>.<slug>`：档案
- `assertion.<slug>`：断言
- `expr.<slug>`：表达层
- `entity.<type>.<slug>`：人物、国家、文化、聚落等实体
- `profile.<slug>`：知识画像
- `referral.<slug>`：推荐对象类型
- `source.<slug>`：来源登记
- `redirect.<slug>`：稳定 redirect 记录
- `event.<slug>`：审计/迁移事件
- `author.<slug>`：开发者身份

ID 一经发布不可重用；显示名、文件名、数组下标和本地化文本不得参与 ID 生成。

## Redirect

```text
{ from, to, reason, event_id, revision }
```

- `from` 和 `to` 类型必须一致。
- `redirect_id` 是稳定 redirect 身份，且必须登记到 ID ledger；`revision` 是该 redirect 记录自身的单调修订号，从 1 开始，后续修改必须恰为前一 revision + 1。
- redirect 必须单向终止，禁止循环、自引用、过期目标和旧 ID 重用。
- 合并需要人工指定所有旧 ID 到一个新 ID；拆分需要人工为每个旧引用选择目标，不能自动猜测。
- 目标不存在、类型不一致或链不能终止时阻止编译。
- `event.object_id` 必须等于 `redirect_id`，`event.object_revision` 必须等于 redirect 的 `revision`，`event.object_hash` 必须是去除派生 hash 后 redirect 规范对象的 SHA-256。

## Entity lifecycle

使用 `supersedes`、`split_from`、`merged_into` 和有效时间区间记录改名、分裂、合并；`lifecycle.revision` 是该档案 lifecycle 记录自身的单调修订号，从 1 开始，后续修改必须恰为前一 revision + 1。每个生命周期变更必须带 `event_id`，并由 `event_type=migration` 事件证明；`event.object_id` 必须等于宿主 document ID，`event.object_revision` 必须等于 `lifecycle.revision`，hash 必须匹配规范 lifecycle 对象。历史正典不因当前游戏状态改变而删除。

## ID ledger

机器 ledger 契约见 `ID-LEDGER-CONTRACT.md` 和 `id-ledger.v1.schema.json`。ledger `revision` 是该 ID 记录的单调修订号，从 1 开始，后续修改必须恰为前一 revision + 1；新 ID 使用 `event_type=registration`，迁移或 tombstone 使用 `event_type=migration`。`event.object_id` 必须等于 ledger 记录的 ID，`event.object_revision` 必须等于 ledger `revision`。tombstoned ID 只能转为历史状态，不得重新变为 active；没有 ledger tombstone 不得声称 ID 不可重用。