# -*- coding: utf-8 -*-
"""21 个空子域 ↔ 30 条百科素材的映射表（供生成器消费）。

口径（照百科化宪章）：
  - 每个空子域 1 档；素材够则一档引一条**原文**（A 级），
    同域其他素材写进正文但不作引文；
  - 素材确实没有的子域 ⇒ 走 D 级 `author_created` 登记，**不许编 source_id**；
  - 引文一律由生成器从快照机械切取，禁止手抄。

空清单来源：`tools/_wb_taxcov_20260925.py` 实测（22 个空子域，其中
`politics/kingdoms` 已由本轮 8 位君主档填上，故实际待补 21 个）。
"""

# (subdomain, doc_slug, 中文标题, 英文标题, [素材 stringId], 主题句, [别名])
# 素材列表为空 ⇒ 该档走 author_created（D 级登记）。
#
# ★ 别名是**唯一的检索入口**：`RuntimePackageCompiler.cs:112` K1 明确
#   「keywords 来源＝标题＋别名＋实体锚点名」，**正文不进索引**。
#   ⇒ 凡正文里出现、而玩家/NPC 会用来发问的词，必须登进别名，否则捞不到
#   （09-25 实测：问「步兵」0 命中，因只在正文里出现）。
PLAN = [
    # ── politics（kingdoms 已由君主档填，剩 2）──
    ("politics/offices", "politics-offices", "总督与官职", "Governors and Offices",
     ["dehXpX8A", "T5yrOCaG"], "封地要有人替领主看着。",
     ["总督", "官职", "任命", "封地管理", "军需官", "斥候", "工程师", "外科医生", "特长"]),
    ("politics/succession", "politics-succession", "王位与继承", "Throne and Succession",
     ["IudGLWQZ", "ZjaaAoxd"], "王位怎么传下去，是每场内战的第一颗火星。",
     ["继承", "继位", "王位", "世袭", "加冕", "合法国王", "王权"]),

    # ── economy（7 个空）──
    ("economy/currency", "economy-currency", "第纳尔与通货", "Denars and Currency",
     ["aaPVmfMo"], "钱在这片土地上换了名字，但人人都认。",
     ["第纳尔", "货币", "钱", "银币", "金币", "假币", "价钱", "影响力"]),
    ("economy/debt", "economy-debt", "债务与赎金", "Debt and Ransom",
     ["iM2J0vav"], "欠下的东西，迟早要以某种形式还。",
     ["债", "欠钱", "赎金", "借贷", "抵押", "人情", "赖账"]),
    ("economy/food", "economy-food", "食物与给养", "Food and Provisions",
     ["ammp5agv"], "一支队伍最先垮掉的地方，往往不是阵线而是锅灶。",
     ["食物", "粮食", "口粮", "吃喝", "挨饿", "酒", "面包", "牲口"]),
    ("economy/land_production", "economy-land", "土地与出产", "Land and Production",
     ["aEfsyVH4"], "把沼泽排干、把林子砍掉，土地才吐出粮食。",
     ["土地", "田", "耕地", "佃户", "收成", "农活", "牧场", "小麦", "庄园"]),
    ("economy/taxation", "economy-taxation", "税赋", "Taxation",
     ["GoF2sYNt"], "谁收、收多少、怎么收，写在每一条王国律法里。",
     ["税", "征税", "收税", "贡", "赋税", "通行费", "关税", "税金"]),
    ("economy/trade_routes", "economy-trade-routes", "商路与商队", "Trade Routes and Caravans",
     ["qggtvf8Y"], "路通到哪里，钱就流到哪里；路断在哪里，哪里就穷。",
     ["商路", "商队", "贸易", "通商", "买卖", "货运", "走私", "市集"]),
    ("economy/workshops", "economy-workshops", "作坊与匠人", "Workshops and Craftsmen",
     ["w94Fl25I"], "从羊毛到毛毡，中间隔着一整条手艺人链子。",
     ["作坊", "工坊", "工匠", "铁匠", "羊毛", "毛毡", "手艺", "打造", "铺子"]),

    # ── culture（4 个空）──
    ("culture/language", "culture-language", "语言与文字", "Language and Letters",
     ["1Ww5lg6I", "X0kKBzsW"], "瓦兰迪亚人初来时语言混杂——他们的名字就是从这种混杂里长出来的。",
     ["语言", "文字", "识字", "口音", "方言", "写信", "念书", "名字"]),
    ("culture/marriage", "culture-marriage", "婚姻与联姻", "Marriage and Alliances",
     ["mvCgqAyT"], "家族以血缘为名义，婚姻是它真正的那根线。",
     ["婚姻", "结婚", "娶", "嫁", "联姻", "妻子", "丈夫", "成亲", "再婚"]),
    ("culture/clothing", "culture-clothing", "衣着与装饰", "Clothing and Adornment",
     ["a2AjI8nc", "Ud1AMybr"], "从沙漠的遮罩到森林的皮毛，穿什么由脚下的地决定。",
     ["衣服", "衣着", "穿", "斗篷", "长袍", "皮草", "头巾", "打扮"]),
    ("culture/festivals", "culture-festivals", "节庆与祭祀", "Festivals and Rites",
     ["ZjaaAoxd"], "在圣山上加冕，在村口摆酒——同一个仪式，两种排场。",
     ["节日", "庆典", "祭祀", "宴会", "摆酒", "婚礼", "仪式", "圣山", "加冕"]),

    # ── war（4 个空）──
    ("war/tactics", "war-tactics", "兵法与编队", "Tactics and Formations",
     ["2OnPxeu0", "cXbEPF8R", "7H5RG3fS", "toAD1Vxz", "CxvVbLnK"],
     "步兵守住线，骑兵找缺口，弓手决定谁先撑不住。",
     ["兵法", "战术", "编队", "步兵", "骑兵", "弓手", "射手", "骑射手", "阵型",
      "冲锋", "侧翼", "远程", "标枪", "盾牌"]),
    ("war/fortifications", "war-fortifications", "筑城与攻城", "Fortifications and Sieges",
     ["93mX8aKp"], "石头堆起来的东西，最后还是要靠人守、靠人饿。",
     ["攻城", "围城", "城墙", "要塞", "城堡", "守城", "塔楼", "云梯"]),
    ("war/logistics", "war-logistics", "补给与行军", "Logistics and Supply",
     ["T5yrOCaG"], "凝聚力掉到零，军团自己就散了。",
     ["补给", "行军", "粮草", "辎重", "军团", "凝聚力", "后勤", "军需"]),
    ("war/prisoners", "war-prisoners", "俘虏与赎金", "Prisoners and Ransom",
     ["uce9Opgg"], "活着的敌人比死了的值钱。",
     ["俘虏", "战俘", "赎金", "囚犯", "关押", "放人", "交赎"]),

    # ── geography（5 个空）──
    ("geography/climate", "geography-climate", "气候与季节", "Climate and Seasons",
     ["Ud1AMybr"], "北方是致命的寒冬，南方是曝晒的砾石。",
     ["气候", "天气", "冬天", "夏天", "严寒", "酷热", "雨季", "降雪", "风沙"]),
    ("geography/directions", "geography-directions", "四方与边境", "Directions and Frontiers",
     ["fGNdo1BK", "Xtyb9Dyr"], "东边的草海、南边的沙漠、北边的森林、西边的海——帝国夹在中间。",
     ["东方", "西方", "南方", "北方", "边境", "边疆", "草海", "沙漠", "森林"]),
    ("geography/natural_boundaries", "geography-natural-boundaries", "山川与天然疆界",
     "Natural Boundaries", ["Ax9OkgL5", "mOZsbV8j"],
     "云雾缭绕的山区、出露的火成岩、青铜沙漠的砾石平原——疆界先由地形画好。",
     ["山脉", "山口", "大河", "海岸", "草原", "沙漠", "山地", "森林", "海峡"]),
    # ⚠️ 以下两档在 30 条百科素材里确实找不到干净原文 ⇒ 走 D 级 author_created
    ("geography/roads", "geography-roads", "道路与渡口", "Roads and Crossings",
     [], "路网决定了谁能把货送到、谁能把兵开到。",
     ["道路", "大路", "小路", "渡口", "桥", "驿道", "路口", "行路"]),
    ("geography/sea_routes", "geography-sea-routes", "海路与港口", "Sea Routes and Ports",
     [], "第一批瓦兰迪亚人就是横渡汪洋来的；海从来没有挡住过人。",
     ["海路", "港口", "码头", "船", "航海", "渡海", "商船", "沿岸"]),
]
