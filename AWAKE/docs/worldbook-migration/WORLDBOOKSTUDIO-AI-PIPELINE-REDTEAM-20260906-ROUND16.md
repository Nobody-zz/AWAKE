# Worldbook Studio AI 生成管线持续红队——第十六轮

> 日期：2026-09-06  
> 范围：生成期间的异步响应、用户编辑/候选切换、请求取消和旧结果覆盖。  
> 状态：`NO_NEW_P0_P1`  
> 本轮性质：只读静态复核 + Draft race/DOM 基线；未修改实现。

## 1. 执行边界与基线

本轮未：

- 启动 Bannerlord；
- 同步游戏目录；
- 访问真实 Cloud Provider、API Key、Token、Worker 或网络；
- 读取/修改 Downloads 源目录；
- 修改五个世界书迁移候选；
- 修改 Worldbook Studio 实现。

本轮基线：

- Draft race harness：`2/2 PASS`
- Draft DOM/state harness：`2/2 PASS`

## 2. 攻击范围

复核以下攻击：

1. 生成请求进行中编辑事实文本；
2. 生成请求进行中修改 source；
3. 生成请求进行中切换 stage；
4. 生成请求进行中切换候选；
5. 生成响应返回后用户已开始新一轮请求；
6. 用户关闭 Draft dialog 后旧响应返回；
7. create-document 返回后文档列表已发生变化；
8. AbortController 取消后旧响应继续写入状态。

## 3. 现有防线结果

当前 `studio-draft.js` 使用：

```text
generation
activeRequest
AbortController
draftSnapshot
draftRequestIsCurrent
```

`draftRequestIsCurrent` 比较：

```text
generation
stage
sourceName
sourceText
providerId
perspectives
```

文本编辑、source 修改、stage 切换和候选选择均会触发 `draftMarkChanged()`，因此旧响应不会覆盖新的 Draft 状态。

`draft-race.test.js` 已验证：

- late create response 不会切换错误文档；
- 文档刷新后会再次检查当前状态。

`draft-dom-state.test.js` 已验证：

- 恢复状态、筛选、详情和多选不会因重渲染丢失。

## 4. 本轮结论

本轮没有发现新的 P0 或 P1。

| 严重度 | 新增 | 结论 |
|---|---:|---|
| P0 | 0 | 没有新的无权限发布/runtime 路径 |
| P1 | 0 | 没有新的未覆盖异步状态绕过 |
| P2 | 0 | 本轮没有新增独立缺口 |

前面已发现但尚未处理的本地草稿防抖 timer、服务端 Draft 续接和 CandidateSet 权威回读问题仍然有效，本轮不重复计数。

## 5. 收敛记录

```text
P0 = 0
本轮新增 P0/P1 = 0
累计待处置 P1 = 34
连续无新增 P0/P1 = 1 轮
实现修改 = 0
下一步 = 再进行一轮独立范围红队；若仍无新增，则满足“连续两轮无新增”部分，但不代表 P1 已处置
```

