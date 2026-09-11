# GRILLME: Worldbook Authoring v1 投影批计划拷问 — 20260912

> 对 PLAN-WORLDBOOK-AUTHORING-PROJECTION-20260912.md 的拷问轮；每问给结论，导致计划变更的标 ⚙ 并已写回计划书"拷问修订"节。

**Q1 建档"走官方保存链"具体走哪条？**
已有实证：作者闭环验收用 `POST /api/authoring/save-authoring`（CAS：sourceHash+revision）、`create-document` 建档（real-worker 链路）。批量建档走同一组端点，逐档 读→建→存。⚙ 计划补 CAS 与端点名。

**Q2 unresolved 类 claim 怎么投影？** ⚙
B5 断言只有五种 kind，无 unresolved。裁定：知识限制类（96448244、fd78）**不落断言**，落档案"确定程度=存疑"+摘要句；纯注册元数据（ddfb30ad）**不投影**，留在迁移文档层。

**Q3 表达身份不在登记表怎么办？** ⚙
不扩登记表。表达身份一律映射手册九类通用身份（学士→贵族学识口径、跑船人/渔民/山民→普通平民、行商→公证商人），具体来源身份保留在表达文本内（"学士说/跑船人说"已自然在文中）。消解 W4 的登记表扩充风险。

**Q4 subdomain 必填，ID 从哪来？** ⚙
从 `knowledge-taxonomy.v1.json` 取稳定 ID，禁止手填；W2 增加逐档归类表。

**Q5 档案 ID 冲突与查重？** ⚙
正式编号由 Studio 生成（A3.1），候选临时 ID 不带入；建档前对 editor-catalog 查重（同名/同主题）。

**Q6 place_cluster/split_from 塞得进 authoring schema 吗？** ⚙
authoring.v1 schema 可能白名单无此字段。裁定：不硬塞；批次维护 `PROJECTION-MANIFEST.json`（候选 ID → 新档案编号 + place_cluster + split_from），档案层能放则放、放不下归 manifest。

**Q7 r3-revision 冻结了吗？**
是。W3 细化引文的产物落在 authoring 档案证据 + `QUOTE-REFINEMENT-20260912` 记录，**不回写**已批准的 r3 documents。⚙ 补进计划。

**Q8 W3 与 W4 的实施顺序？**
逐档串行做完 W2→W3→W4 再下一档（避免半档状态），W5/W6 收尾。

**Q9 登记 schema 扩 lore 分区的版本处理？**
W1 先读 registry schema，确定扩展方式与版本号/哈希更新路径；schema 文件是数据层变更，仍属"不改 C#"边界内。

**Q10 三向闭合怎么在 authoring 产物上复验？**
validator 思路移植：assertion↔claim 一一对应、expression↔表达 span、layer/grants 与候选一致。⚙ W6 补此工具项。

**Q11 谁审批？**
用户在 Studio UI 人工审批 12 档（Authority Gate 主权），本批产物停在 needs_review。

**Q12 失败回滚？**
建档失败逐档重试；已建档案不删（Studio 无删除语义则标废弃说明），manifest 记录状态。
