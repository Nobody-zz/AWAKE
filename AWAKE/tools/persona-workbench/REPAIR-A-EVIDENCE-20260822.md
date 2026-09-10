# PersonaWorkbench 批次 A 候选证据

## 范围

- 目标：保存/批准原子性、失败保留、角色切换状态隔离、元数据保留、异常输入结构化和文档竞态。
- 计划：`PLAN-PWB-REPAIR-20260822.md`。
- 审查：`PLAN-PWB-REPAIR-REVIEW-LOG.md`，Round 2 verdict 为 `APPROVED`。
- Provider：仅使用本地 Ollama；未调用云端 Provider。

## 回归门

- `PersonaWorkbench.Core.Tests`：PASS。
- `PersonaWorkbench.Web.Tests`：PASS。
- `PersonaWorkbench.BrowserSmoke`：PASS。
- 覆盖：失败批准不改状态、无效文档不落盘、元数据往返、结构化 HTTP 错误、旧预览响应丢弃、加载新角色清理临时 Provider 状态、保存保留元数据、假 Provider 截断/异常路径。

## 固定样本

| 样本 | UTF-8 bytes | SHA-256 |
|---|---:|---|
| short | 270 | `3b82f664f26f11862c7da899668aaa0635461f5cc3fda711a8d7622cba5eb968` |
| long | 3888 | `8234670f470a8f663d5436111b39cf10d97ae9aa1d123e6ff7c090561f93b752` |

## Worker-low 诊断

模型：`gpt-oss:20b`；digest：`17052f91a42e97930aa6e28a6c6c06a983e6a58dbb00434885a0cf5313e376f7`；thinking：`low`。

| 阶段 | 结果 | 耗时 | 输出 bytes |
|---|---|---:|---:|
| `expand-short` | PASS | 26020 ms | 252 |
| `dsl-short` | PASS | 29625 ms | 370 |
| `expand-long` | PASS | 28734 ms | 1053 |
| `dsl-long` | PASS | 16568 ms | 357 |

Worker 结果只作为模型诊断证据，不等同于 Workbench 路由验收。

## Workbench 本地真实路由

候选包：`artifacts/PersonaWorkbench-FreePreview-r72-20260822`。端点：`http://127.0.0.1:11434/v1/chat/completions`；模型：`gpt-oss:20b`；转换输入优先使用同一轮扩充结果；未自动重试。

| 阶段 | 输入来源 | HTTP | 结果 | 耗时 | 输出 bytes | Usage total |
|---|---|---:|---|---:|---:|---:|
| `expand-short` | fixture | 200 | PASS | 25563 ms | 405 | 617 |
| `convert-short` | Workbench expansion | 200 | PASS | 25980 ms | 1507 | 1100 |
| `expand-long` | fixture | 200 | PASS | 37193 ms | 783 | 1878 |
| `convert-long` | Workbench expansion | 200 | PASS | 72641 ms | 1963 | 1897 |

两次 DSL 转换均返回非空 `draft`，且 `usedLocalFallback=false`。本轮固定样本未复现 `provider.intermediate_candidate_truncated`。

## 候选包完整性

- Candidate label：`PersonaWorkbench-FreePreview-r72-20260822`。
- 生成时间记录：`2026-08-22T18:33:41.1994010+08:00`。
- `PersonaWorkbench.Web.exe` SHA-256：`17531AEDF2C6B293E386DB1482BF1954EF176E860AAF7FC8F518FC7F4F934388`。
- `PersonaWorkbench.Launcher.exe` SHA-256：`F0F8A1F21527B7393B8933A0301E1C9814C77596F6B6825C2165FBAD94FB9ED4`。
- `PACKAGE-MANIFEST.sha256.txt` SHA-256：`C11026E06B156BF20A2D03CE28C23701014CC34D90A158DBD0BBA5B89BA6772D`。
- Manifest entries：21；hash errors：0；forbidden files：0。
- r71 原包未覆盖；候选服务测试后 `51337` 端口已释放。

## 结论

批次 A 的完整性修复、回归门和本地 Ollama 短/长文本候选验证均已完成。该包仍是候选包，不代表正式发行或游戏内验证。
