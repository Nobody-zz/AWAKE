# AF 缺漏人物与战帆参考审计

- AF 当前游戏未匹配人物：53
- 战帆人物表直接命中候选：5
- 只有海军领主/描述文本命中：46
- 完全没有命中：2

判定规则：

- war_sails_reference_candidate：战帆 heroes 字符串表出现同名或相关中文名，可作为人工核对候选。
- war_sails_name_or_description_hit_only：只在战帆海军领主或描述文本中出现，不足以证明稳定人物对象关系。
- needs_manual_review：战帆索引未找到同名参考。

重要限制：

1. 本表只帮助定位参考资料，不补写当前游戏的 HeroId、ClanId、KingdomId。
2. 战帆是《骑马与砍杀 II：霸主》卡拉迪亚世界的官方 DLC；本机未安装战帆，只表示当前没有对应的游戏运行数据，不表示它是另一个世界。
3. 战帆对象即使属于官方 DLC 数据，也不能未经开发者审核自动成为 AWAKE 正典或默认启用内容；稳定的 DLC XML 对象关系可以作为同一世界下的参考来源。
4. lord_7_* 与 dead_lord_7_* 的共同编号模式不能单独证明家族关系。

机器表：persona-game-war-sails-candidate-audit.v1.csv
