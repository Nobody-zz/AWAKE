# 人物—家族映射

- Schema: awake.persona-family-mapping.v1
- 总人物：415
- 当前游戏 XML 精确绑定：362
- 战帆对象 XML 精确绑定：53
- 战帆人物与家族文本参考：0
- 只有战帆人物姓名键参考：0
- 尚未解析：0
- 战帆对象 XML 中但 AF 没有对应人物文件：3

战帆说明：战帆是卡拉迪亚世界的官方 DLC。当前游戏未安装战帆时，战帆对象仍属于同一世界的 DLC 数据，只是本机没有可直接运行的对象；这不构成基础游戏映射冲突。

主表字段：

- person_name_zh：人物中文名
- person_code：人物代码，例如 lord_1_1、lord_7_1
- family_name_zh：家族中文名
- family_code：优先填写当前游戏真实运行时 ClanId；当前游戏没有时，填写战帆 heroes.xml 的 Hero.faction 运行时 ClanId
- family_code_type：标记运行时家族代码来自当前游戏对象 XML 还是战帆对象 XML
- family_name_string_id：战帆家族本地化键，不等于运行时 ClanId
- family_group_code：从 lord_7_* 文件名层级提取的家族组根代码，仅作参考，不等于 ClanId
- war_sails_name_string_id：战帆具名人物姓名键
- war_sails_family_code：战帆 heroes.xml 直接提供的 Hero.faction，可用于战帆人物视角过滤
- mapping_status：证据等级和映射状态

边界：

1. 当前游戏 heroes.xml -> spclans.xml 是当前游戏人物—家族代码的权威。
2. 新增战帆 heroes.xml 是对象数据；其中 Hero.id 与 Hero.faction 可直接提供战帆人物和运行时家族代码。
3. std_clans_xml-zho-CN.xml 只是中文本地化表。它能提供家族中文名和 StringId，但没有说明哪个 StringId 对应哪个 clan_nord_*，因此不按文件顺序猜配。
4. family_name_zh 为空不代表人物没有家族，而是代表目前没有足够证据把中文家族名绑定到该运行时家族代码。
5. war-sails-heroes-not-in-af.v1.csv 列出战帆对象 XML 有、AF 人物目录没有的额外人物。

输出：

- persona-family-mapping.v1.csv：内容编辑者可查看的人物—家族表。
- persona-family-simple.v1.csv：只保留人物、人物代码、家族、家族代码和状态的简表。
- war-sails-person-family-simple.v1.csv：只列出 lord_7_* / dead_lord_7_* 战帆参考人物。
- persona-family-mapping.v1.json：机器读取的完整映射和输入哈希。
- war-sails-family-reference.v1.csv：战帆家族名称键参考表。
- war-sails-heroes-not-in-af.v1.csv：战帆对象 XML 中未进入 AF 人物目录的额外人物。
