# Plan: Worldbook Studio AI reference-to-draft workflow
_Locked via prior user direction — 2026-08-24_

## Goal

在不改动 AWAKE 冻结候选和编译端的前提下，为 Worldbook Studio 增加面向零基础内容编辑者的 AI 参考资料创作向导：参考资料输入后生成客观事实、简介候选和多身份表达草稿；人工逐条审核后回填普通编辑表单；AI 始终不直接写入正典。

## Approach

1. 修复发布包 UTF-8 清单与校验
2. 增加独立草稿请求与结果契约
3. 复用双 Provider 传输并接入安全会话
4. 增加资料导入、事实审核和视角审核 UI
5. 回填普通表单并保留来源登记入口
6. 运行 Core、Web、包校验和负向回归

## Key decisions & tradeoffs

- 首版支持粘贴文本和本地文本类文件；不做网页抓取。
- 草稿结果不复用受限的 `knowledge-patch.v1`，避免把新增事实伪装成修改已有字段。
- 资料默认只进入临时草稿；用户明确保存来源后才写入 `authoring/sources`。
- 事实、简介、NPC 表达分阶段生成，但允许在同一草稿会话内继续推进。
- 采纳操作只改变浏览器/服务端草稿缓冲区；最终保存仍经过现有作者模式 CAS 和 schema 校验。

## Risks / open questions

- 不同云端 Provider 对结构化 JSON 的遵循程度不同，需要保留严格解析失败提示和重试入口。
- 视角预设需要使用现有 registry 的真实中文标签；缺失映射时必须显示“自定义视角”，不能发送裸英文 ID。
- 来源引用自动绑定需要防止用户修改原文后 hash 失配；首版只允许由 Studio 生成登记文件。

## Out of scope

- Bannerlord 运行时读取器、周报、时间线编译和游戏目录同步。
- 自动联网抓取、自动正典化、自动发布、自动替换现有档案。
- 删除或重写旧高级 YAML 编辑模式。
