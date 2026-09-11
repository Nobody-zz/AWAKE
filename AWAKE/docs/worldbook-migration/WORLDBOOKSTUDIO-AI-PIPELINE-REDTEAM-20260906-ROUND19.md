# Worldbook Studio AI 生成管线持续红队——第十九轮

> 日期：2026-09-06  
> 范围：Draft HTTP 会话、CSRF、旧路由、Provider endpoint policy、错误投影和 UI 旧入口。  
> 状态：`NO_NEW_P0_P1`  
> 本轮性质：只读静态复核 + EditorContent 基线；未修改实现。

## 1. 执行边界与基线

本轮未：

- 启动 Bannerlord；
- 同步游戏目录；
- 访问真实 Cloud Provider、API Key、Token、Worker 或网络；
- 读取/修改 Downloads 源目录；
- 修改五个世界书迁移候选；
- 修改 Worldbook Studio 实现。

本轮基线：

- Editor content harness：`7/7 PASS`

## 2. 攻击范围

复核：

```text
Draft prepare/generate/review/create-document 会话绑定
CSRF 和 Origin 边界
旧 /api/ai/draft 与新 /api/ai/authoring/draft 路由
Cloud/Local Provider endpoint policy
safe error projection
legacy AI apply/reject 路由
```

## 3. 结果

现有调用链要求 session；写入型 Draft 路由继续经过 CSRF/Origin 约束。旧 AI apply/reject 路由保持 retired/legacy 边界，没有发现可绕过 Draft submission validator、CandidateSet 或 needs_review 的稳定路径。

Provider 端点策略仍区分 cloud 与 loopback local worker；错误投影不把 API key 作为正常响应内容返回。

本轮没有发现新的 P0/P1。

```text
P0 = 0
本轮新增 P0/P1 = 0
连续无新增 P0/P1 = 3 轮
```

## 4. 收敛限制

虽然本轮与第十八轮均无新增 P0/P1，但历史 P1 尚未有实现处置证据。因此只能记录为：

```text
攻击面红队暂时无新增
整体红队目标尚未完成
```

仍待处置的历史 P1 包括：

- Quick Authoring 输入契约和用户意图；
- content tier 默认与 authority；
- CandidateSet lifecycle/stale/superseded；
- warnings/unresolved 生命周期与落盘；
- Draft state version/migration；
- 本地草稿隔离与服务端 Draft 续接；
- evidence exact/normalized/duplicate locator；
- candidate ID 冲突与 merge 去重；
- candidate 分类和 certainty/perspective 落盘保真；
- review decision 幂等和 expected generation；
- Batch 空结果与 source hash 绑定。

## 5. 当前状态

```text
P0 = 0
累计待处置 P1 = 36
本轮新增 P1 = 0
连续无新增 P0/P1 = 3 轮
实现修改 = 0
下一步 = 需要用户授权进入修复批次；修复后重新开启红队
```

