# R3 候选批量生成器（B4 拆分版）
# 5 个源条目拆为 17 个文档；表达规则见 WRITING-RULES.md。
# 结构：中立内核段（fact/relation，零旁白零模糊语）+ 身份表达段（attribution 口吻，layer 标注）。
# span 用唯一标记定位，偏移由脚本计算；产物写入归档区 r3-revision/，不覆盖 r2。
param(
    [string]$OutDir = "D:\AWAKE-Archive\worldbook-migration-content\semantic-rewrite-batch-download-20260903\r3-revision\documents"
)
$ErrorActionPreference = "Stop"
$SNAP = "629a24fc633c035aa83d369b54d35cd3a893eb429d5fcf501e2e2ff0c717f129"

# ---------- claim 定义（id 后 24 位 hex + 全字段） ----------
$C = @{
'lac-loc'    = @{id='0d2520893b3a115035a9a1a0';kind='source_fact';s='拉科尼斯湖';p='位于';o='卡拉迪亚北部的封闭型内陆淡水湖';persp='neutral';orig=@('6792cb2c1224889c505df1ba','9989debf022c34e94bddd43e','28a21f9e5db2488ea6a4bc5e','d35c759a5ef84e3997f96df0');note='r2 保留'}
'lac-riv'    = @{id='80fd2b2477dad8dd01375064';kind='relationship';s='弥戎河与喀拉卡兹河';p='汇入并连接';o='拉科尼斯湖及北部水运网络';persp='neutral';orig=@('bfc66570e6db11cb4b5768ec','9989debf022c34e94bddd43e','28a21f9e5db2488ea6a4bc5e','d35c759a5ef84e3997f96df0');note='r2 保留'}
'lac-nav'    = @{id='c9ff734b8dd6d7968fb1a954';kind='source_fact';s='拉科尼斯湖';p='在冬季仍可通航';o='陆路受冰雪阻断时仍具运输价值';persp='neutral';orig=@('6792cb2c1224889c505df1ba','bfc66570e6db11cb4b5768ec','9989debf022c34e94bddd43e','28a21f9e5db2488ea6a4bc5e','d35c759a5ef84e3997f96df0');note='r2 保留'}
'lac-live'   = @{id='c4672cffe65c5125bb1d00e7';kind='relationship';s='沿岸居民';p='依靠湖区谋生';o='渔业、船运与码头劳作';persp='neutral';orig=@('d5bd6d3382672ae542a1cc22');note='r3 新增 lac-01'}
'lac-trade'  = @{id='d2c7e2dc921eafdf047a31c8';kind='relationship';s='帝国麦子与斯特吉亚毛皮';p='沿湖区水路转运';o='湖区南北双向贸易';persp='neutral';orig=@('7ba74330155cd80ad446d57a');note='r3 新增 lac-02'}
'lac-imp'    = @{id='e1158ae544d37d2f31aeed53';kind='interpretation';s='帝国兵志叙述';p='将冬季航运视为';o='北境防务与运兵之利';persp='imperial';orig=@('ce4011516c2d730bfe559bea');note='r3 新增 lac-04，主体定为帝国（防务）叙述'}
'lac-rumor'  = @{id='7707292541174752ca88df88';kind='rumor';s='湖水变红的德律亚传说';p='被叙述为';o='湖区战争记忆的一部分；无凭据';persp='local_oral_tradition';orig=@('6792cb2c1224889c505df1ba','9989debf022c34e94bddd43e','28a21f9e5db2488ea6a4bc5e','d35c759a5ef84e3997f96df0');note='r2 保留'}
'lac-rust'   = @{id='c829e454ee3264a31fe6c451';kind='interpretation';s='浅滩锈色';p='被学士解释为';o='湖底烂泥枯草积年沤出的锈气上浮（保留原文或然口气）';persp='survey_narrative';orig=@('a5239cb238349326884324ff');note='r3 新增 lac-03，用词已中世纪化'}
'sara-bay'   = @{id='3b9d7b54dab5b10399c69bcf';kind='source_fact';s='沙拉斯湾';p='属于';o='西大洋伸入西岸的半封闭海域';persp='neutral';orig=@('f909774865375efcedda705f','37c93efdede02cb6daf570e2','aa04431ae3638ceeb29a2b77','d891efcae094b0ed440a1646');note='r2 保留'}
'sara-isles' = @{id='59cc0718f038c2b4a0c3e0a6';kind='source_fact';s='南部群岛';p='削弱';o='外海涌浪并使湾内较适合停泊与近岸航行';persp='neutral';orig=@('f909774865375efcedda705f','37c93efdede02cb6daf570e2','aa04431ae3638ceeb29a2b77','d891efcae094b0ed440a1646');note='r2 保留'}
'sara-strait'= @{id='b3f620e228052b0073d624b5';kind='relationship';s='加隆托海峡';p='承担';o='湾内外水体交换并构成航运要道';persp='geographic_or_maritime';orig=@('ff1f9f7cefc1b400e083a2c1');note='r3 新增 sara-01，按原文强度落笔'}
'sara-port'  = @{id='4d959427d53c21b06f4b7db8';kind='relationship';s='沙拉斯湾与沙拉斯港';p='支撑';o='渔业、航运和港口生计';persp='neutral';orig=@('f909774865375efcedda705f','aa04431ae3638ceeb29a2b77','d891efcae094b0ed440a1646');note='r2 保留'}
'sara-cradle'= @{id='2bd862c0fc76a6b093c72d40';kind='interpretation';s='帝国历史传统';p='把沙拉斯湾解释为';o='卡拉德先祖登陆的摇篮';persp='imperial_chronicle';orig=@('4b5c4e92c558a50b89d34516','37c93efdede02cb6daf570e2','aa04431ae3638ceeb29a2b77','d891efcae094b0ed440a1646');note='r2 保留 sara-02'}
'sara-land'  = @{id='cf3972ba2c60a3234be103d3';kind='rumor';s='先祖从群岛登陆的故事';p='被标记为';o='尚未获得考古与移民史佐证的传说';persp='scholar_or_chronicler';orig=@('4b5c4e92c558a50b89d34516','37c93efdede02cb6daf570e2','aa04431ae3638ceeb29a2b77','d891efcae094b0ed440a1646');note='r2 保留 sara-03'}
'sara-tales' = @{id='7889b88429c064b45cd4a68b';kind='rumor';s='群岛的雾、罗盘和石刻故事';p='属于';o='船员传闻，不能直接当作事实';persp='sailor';orig=@('f909774865375efcedda705f','aa04431ae3638ceeb29a2b77','d891efcae094b0ed440a1646');note='r2 保留 sara-04'}
'kach-pos'   = @{id='1ddf7e7efeaa259eff44a5bf';kind='relationship';s='卡恰尔半岛';p='位于';o='斯特吉亚东侧并夹在北方与南方海域之间';persp='neutral';orig=@('23c0a329d5c5c66d4370035a','94ac23017eeb5d2e0fdd84d1','206d487d908cebfc42146574','3d467f21af08984d37a37fd9','a49e9fcc030002b9bc571b28','ff9d4c7f163c52568e176640','508198f6fcc695b3b2913fe3','a84dca4d1f7293a66142cb66');note='r2 保留'}
'kach-terra' = @{id='bb010f8c526d3be41857a8c7';kind='source_fact';s='卡恰尔半岛';p='具有';o='狭长、多石、海崖陡峭且耕地有限的地形';persp='neutral';orig=@('23c0a329d5c5c66d4370035a','94ac23017eeb5d2e0fdd84d1','3d467f21af08984d37a37fd9','a49e9fcc030002b9bc571b28','ff9d4c7f163c52568e176640','508198f6fcc695b3b2913fe3','a84dca4d1f7293a66142cb66');note='r2 保留'}
'kach-harbor'= @{id='a48e49e8bc9c8d3cc77b3bc2';kind='source_fact';s='卡恰尔半岛';p='地形上';o='适合建港口的地方不多';persp='neutral';orig=@('30c093b48c33986cf2773924','3d467f21af08984d37a37fd9');note='r3 新增 kach-02，V0+V6 双 origin（独立审查补绑）'}
'kach-war'   = @{id='42d80bdb7b7cc690dd16e45a';kind='interpretation';s='卡恰尔半岛';p='被部分叙述赋予';o='设哨望海、把扼水路的战略用途';persp='mixed_cultural';orig=@('9fe36f20baf04e7c868405cc','1853f6f685ba1c4723babc88');note='r3 新增 kach-03'}
'kach-succ'  = @{id='a1168db44a5367611e4677d3';kind='state';s='卡恰尔半岛';p='曾被叙述为先后由';o='巴旦尼亚人、诺德人和斯特吉亚人控制';persp='chronicle_summary';orig=@('23c0a329d5c5c66d4370035a','94ac23017eeb5d2e0fdd84d1','206d487d908cebfc42146574','13f9390b840b5d6b5ff80b44','957378bc01ef92aeb8c91732','ff9d4c7f163c52568e176640','508198f6fcc695b3b2913fe3','a84dca4d1f7293a66142cb66');note='r2 保留'}
'kach-bat'   = @{id='2a766fe18cf768014da363d2';kind='interpretation';s='巴旦尼亚叙述';p='将易主归因于';o='借斯特吉亚人清除诺德人后反失其地';persp='battania';orig=@('f750bd2c82e7c1a70246c4a7','b937bd2489cee0f594784d82','da3a671da0f48db2201b6335');note='r3 新增 kach-05'}
'kach-nor'   = @{id='29471fc14b7ee2c3e7b7be74';kind='interpretation';s='诺德叙述';p='将易主归因于';o='巴旦尼亚人设局、雇军反噬';persp='nord';orig=@('bfbcf87ac995494464b77daa','72e83c5fd1d9128300317556','8bcf98613d010ed2fca40daa');note='r3 新增 kach-05'}
'kach-stu'   = @{id='19d09a9134841be233413cb2';kind='interpretation';s='斯特吉亚叙述';p='将易主归因于';o='两方相争而坐收其成';persp='sturgia';orig=@('735bce810b08055af5a4efa6','32af781c62b1f0133bb6ff9b','4dec3d4dc19af168b71bd687');note='r3 新增 kach-05'}
'kach-conf'  = @{id='47fbec18d69a1cc10a9ed2c5';kind='interpretation';s='三方易主叙述';p='彼此存在';o='对援军、背叛和趁乱夺地的不同解释';persp='battania_nord_sturgia';orig=@('94ac23017eeb5d2e0fdd84d1','d759d91af73234caa80779c6','13f9390b840b5d6b5ff80b44','957378bc01ef92aeb8c91732','ff9d4c7f163c52568e176640','508198f6fcc695b3b2913fe3','a84dca4d1f7293a66142cb66');note='r2 保留'}
'kach-khu'   = @{id='9dddd4ae617f78441f817f84';kind='interpretation';s='卡恰尔半岛地形';p='在库赛特叙述中被判断为';o='不利于骑兵展开';persp='khuzaite';orig=@('30bb5679b259bf56550aca2c');note='r3 新增 kach-04'}
'kach-unres' = @{id='9644824420c36a47d41eae1e';kind='unresolved';s='易主次序与当前归属';p='不能仅凭旧世界书确认';o='需要时代与游戏状态证据';persp='runtime_state_required';orig=@('4ec5f5b6085abd35411cf7f1','fafa6ebea323dd7c93b6e5fe');note='r3 新增 kach-01；游戏锚点作交叉引用不入正文'}
'dawn-loc'   = @{id='2da527808a3b5f26b6bc4690';kind='source_fact';s='黎明山脉';p='位于';o='德夫赛格高原东缘并大致南北延展';persp='neutral';orig=@('a9b72bd02a2375457d6e7511','da3d476ae6799cd17f0ce203','35dde1cd5124c08c16e25afb','33c0619278d098c3b62b6cf8');note='r2 保留'}
'dawn-foot'  = @{id='dc592556b2e4c247d50cd40a';kind='source_fact';s='黎明山脉山麓';p='覆盖';o='针叶林与高山草甸';persp='neutral';orig=@('67ffaea0e27373a734c8c043');note='r3 新增 dawn-01，不补山腰分层'}
'dawn-edge'  = @{id='1c70c804f9d583f4e8d33c96';kind='source_fact';s='黎明山脉';p='构成';o='德夫赛格高原东缘的地理边界';persp='neutral';orig=@('dc50ead6102fd6c77daaca97','dcede3c67bc117cb4a3bf032');note='r3 新增 dawn-02'}
'dawn-taboo' = @{id='8e71486bbda76901237134dd';kind='relationship';s='山脉腹地与山民习俗';p='形成';o='未经允许不得进入的社会边界';persp='mountain_community';orig=@('65c009ad533c80a1c5309a10','a3a408f910436a380a600617');note='r3 新增 dawn-02'}
'dawn-legend'= @{id='17ddf9219131f13632e45320';kind='rumor';s='阿赫哈克、黑魔法和古老仪式的故事';p='属于';o='当地传说与信仰叙述';persp='local_tradition';orig=@('83961e21b835b4d258576a57');note='r3 新增 dawn-03'}
'dawn-stew'  = @{id='8b0d5629862f3c72dad1c7f1';kind='interpretation';s='合儿必特部进入山地的过程';p='被来源解释为';o='通过通婚、共俗和守护者身份融入当地';persp='chronicle_interpretation';orig=@('0c9c5aeed1beb4316607df0d','ca7ee9265d94e60a4b17b7ac');note='r3 新增 dawn-04'}
'dawn-ctrl'  = @{id='fd78df11d0b49237fa8d34dd';kind='unresolved';s='黎明山脉的当前实际控制';p='不能仅凭旧世界书确认';o='需要当前状态证据';persp='runtime_state_required';orig=@('87d93dfafccf8f4996c5919f','3b556703bcb05325e3e3c509');note='r3 新增 dawn-05'}
'der-vill'   = @{id='aaffcbbc1deebd4394117f51';kind='source_fact';s='德里亚特';p='位于';o='卡琉斯堡附近、瓦尔切格湾与埃博半岛山脊之间';persp='neutral';orig=@('2d10c4485858a1066a02edb1','b914a5e8bf2c60e3674b9ee7','0ec35eb8e5de203fd8419088','de11a5d1ec7bbe86add3b764','b844fb9c8953f7eac55696ed','45668da2677d15a911ba99d0');note='r2 保留'}
'der-furs'   = @{id='575e2811c363e513120d23f7';kind='source_fact';s='德里亚特';p='产出';o='河狸、水貂和海豹相关毛皮';persp='neutral';orig=@('2d10c4485858a1066a02edb1','b914a5e8bf2c60e3674b9ee7','0ec35eb8e5de203fd8419088','de11a5d1ec7bbe86add3b764','b844fb9c8953f7eac55696ed','45668da2677d15a911ba99d0');note='r2 保留'}
'der-oil'    = @{id='6fde897b4e66fee72843b855';kind='source_fact';s='德里亚特';p='产出';o='可由海豹加工而来的油脂';persp='neutral';orig=@('2d10c4485858a1066a02edb1','de11a5d1ec7bbe86add3b764','b844fb9c8953f7eac55696ed','45668da2677d15a911ba99d0');note='r2 保留'}
'der-trade'  = @{id='57e81a172cc6d39dbebffae4';kind='relationship';s='德里亚特的海豹皮';p='进入';o='瓦尔切格湾一带的交易';persp='merchant_or_wanderer';orig=@('72aae9c7742aa2b714cde743');note='r3 新增'}
'der-cloak'  = @{id='8578161db710e16e0c4ec922';kind='relationship';s='德里亚特的海豹皮';p='被用于';o='防水斗篷以及卡琉斯堡守军冬季皮袄';persp='merchant_or_garrison';orig=@('cfc037e4579446c6b278d70d','ff93757629b3a361d0b5fcbc');note='r3 新增 der-01，收窄口径'}
'der-lamp'   = @{id='d4853fc7e987012ee50ace18';kind='source_fact';s='海豹油';p='可以用于';o='点灯（烟少火亮）';persp='neutral';orig=@('de017ecd107cc7777b6aefb8');note='r3 新增 der-03'}
'der-road'   = @{id='a2958bbb9490ce195a755e68';kind='interpretation';s='德里亚特道路';p='被商旅叙述描述为';o='进入村庄需要费功夫；运出成本未经直接确证';persp='merchant';orig=@('dca60350f8c418ad9167f6d5');note='r3 新增 der-02'}
'der-reg'    = @{id='ddfb30ad3ffe6a7f19b28766';kind='unresolved';s='德里亚特与卡琉斯堡';p='存在旧式定居点绑定标记，但';o='正式 entity ID 尚未确认';persp='registry_required';orig=@('b844fb9c8953f7eac55696ed','45668da2677d15a911ba99d0');note='r2 保留 der-04；纯元数据，不入正文'}
}

# ---------- 12 个拆分文档（B4：按调取场景拆，瘦文档并回主题） ----------
$DOCS = @(
@{key='lac-lake';place='拉科尼斯湖';id='7c1e0a4f2b9d46e3a85f01b7';from='57455a7ba76bcab6ce45bca1';domain='geography';title='拉科尼斯湖·水陆与生计';claims=@('lac-loc','lac-riv','lac-nav','lac-live','lac-trade','lac-imp');text=@(
'拉科尼斯湖在卡拉迪亚北部，四面为山地与森林环抱，是一大片封闭的内陆淡水湖。弥戎河与喀拉卡兹河从不同方向注入，把它接进北方的内河航运。',
'湖水终年不冻。入冬后四邻的河道封冻、道路被大雪隔断，湖上的船却照常往来，是北方冬季少有的活水路。',
'湖岸的营生三样：打鱼、行船、码头扛活。帝国出产的麦子由水路北运，斯特吉亚的毛皮顺同一条水路南回，两头商货都在湖上转运。',
'帝国兵志对这条冬季水路另有盘算：北境若起烽烟，大雪封路之时，谁握着湖上的船队，谁就把兵员粮秣的路攥在手里。此乃帝国兵家的一家之言。'
);spans=@(
@{b='拉科尼斯湖在卡拉迪亚北部';e='接进北方的内河航运。';claims=@('lac-loc','lac-riv')},
@{b='湖水终年不冻。';e='少有的活水路。';claims=@('lac-nav')},
@{b='湖岸的营生三样';e='码头扛活。';claims=@('lac-live')},
@{b='帝国出产的麦子';e='都在湖上转运。';claims=@('lac-trade')},
@{b='帝国兵志对这条冬季水路';e='一家之言。';claims=@('lac-imp');layer='detail'}
)},
@{key='lac-tales';place='拉科尼斯湖';id='d8a63f91c2e57b04f17a93d2';from='57455a7ba76bcab6ce45bca1';domain='culture';title='拉科尼斯湖·湖色与传说';claims=@('lac-rumor','lac-rust');text=@(
'湖边流传着德律亚人的老故事：早年间湖水曾被鲜血染红。老辈人提起这段就沉默；故事没有凭据，只当是湖记着的旧事来讲。',
'走过北境的学士另有说法：浅滩有时在黄昏泛出锈色，多半是水底的烂泥枯草积年沤出的锈气浮了上来，跟血染湖水的老故事凑不到一处。学士自己也说，这只是一己之见。'
);spans=@(
@{b='湖边流传着德律亚人';e='旧事来讲。';claims=@('lac-rumor');layer='rumor'},
@{b='走过北境的学士';e='一己之见。';claims=@('lac-rust');layer='detail'}
)},
@{key='sara-bay';place='沙拉斯湾';id='e2b94f76a0d13c58f96b27c4';from='19254d0e00505c440749b338';domain='geography';title='沙拉斯湾·湾澳与港口';claims=@('sara-bay','sara-isles','sara-strait','sara-port');text=@(
'沙拉斯湾是西大洋伸进大陆西岸的一片湾水，东头收口在加隆托海峡。南侧群岛罗列，替湾子挡住外海涌浪，湾里水面平缓，泊船与近岸行船都安稳。海峡内通湾水、外连大洋，是这片水进出的要道。沙拉斯港就靠这湾水面过活：打鱼的、跑船的、码头上讨生活的，都吃这湾水。'
);spans=@(
@{b='沙拉斯湾是西大洋';e='收口在加隆托海峡。';claims=@('sara-bay')},
@{b='南侧群岛罗列';e='泊船与近岸行船都安稳。';claims=@('sara-isles')},
@{b='海峡内通湾水';e='进出的要道。';claims=@('sara-strait')},
@{b='沙拉斯港就靠这湾水面';e='都吃这湾水。';claims=@('sara-port')}
)},
@{key='sara-tales';place='沙拉斯湾';id='3f9ab6d0e57c2841a9d6f0be';from='19254d0e00505c440749b338';domain='culture';title='沙拉斯湾·起源之说与船人怪谈';claims=@('sara-cradle','sara-land','sara-tales');text=@(
'帝国的史书里，这片湾被唤作帝国的摇篮：按史书的说法，卡拉德人的先祖正是从南部群岛渡水而来，头一回在北岸的沙拉斯踏上这片大陆。史书、颂词、老贵族的家训都这么讲。',
'登陆的故事本身无从查考：没有掘出的旧物作证，也没有可靠的迁徙记闻，帝国学士自己也只当传说记着。',
'跑船人另有闲谈：起雾的天，群岛的轮廓会变样；罗盘挨着某些礁石转个不停；石头上刻着没人认得的符号，说不清是帝国人凿的还是更早年间的手笔。港里人只当怪谈讲讲，航道平安要紧。'
);spans=@(
@{b='帝国的史书里';e='都这么讲。';claims=@('sara-cradle');layer='detail'},
@{b='登陆的故事本身无从查考';e='只当传说记着。';claims=@('sara-land');layer='rumor'},
@{b='跑船人另有闲谈';e='航道平安要紧。';claims=@('sara-tales');layer='rumor'}
)},
@{key='kach-land';place='卡恰尔半岛';id='5d82c0a9f3e17b46d2a9c85e';from='6f56c867548c16af9182acbd';domain='geography';title='卡恰尔半岛·地要与用兵';claims=@('kach-pos','kach-terra','kach-harbor','kach-war','kach-khu');text=@(
'卡恰尔半岛从伊卡拉荒原探进海里，是一条狭长的石地，恰好横在南北两片海域中间。地面多是石头和陡崖，种不出庄稼，能泊大船的港位没有几处。',
'用兵的人看的是它的位置：占了合适的岬角，南北两海的动静都收在眼里，因此旧书把它说成设哨望海、把扼水路的要地。库赛特人对它兴致有限——按他们的说法，满地石头陡崖，马蹄站不住，骑兵使不开。'
);spans=@(
@{b='卡恰尔半岛从伊卡拉荒原';e='横在南北两片海域中间。';claims=@('kach-pos')},
@{b='地面多是石头和陡崖';e='港位没有几处。';claims=@('kach-terra','kach-harbor')},
@{b='用兵的人看的是它的位置';e='把扼水路的要地。';claims=@('kach-war');layer='detail'},
@{b='库赛特人对它兴致有限';e='骑兵使不开。';claims=@('kach-khu');layer='summary'}
)},
@{key='kach-own';place='卡恰尔半岛';id='8e3f57b1c9a04d26f7e5b31a';from='6f56c867548c16af9182acbd';domain='politics';title='卡恰尔半岛·归属沿革';claims=@('kach-succ','kach-unres');text=@(
'半岛的归属几经变换：旧世界书的说法，它先后在巴旦尼亚人、诺德人和斯特吉亚人手里。至于每一回易主的先后次序与眼下的归属，仅凭这部旧书说不清，须对照当世的状态证据才好定论。'
);spans=@(
@{b='半岛的归属几经变换';e='斯特吉亚人手里。';claims=@('kach-succ')},
@{b='至于每一回易主';e='才好定论。';claims=@('kach-unres')}
)},
@{key='kach-tales';place='卡恰尔半岛';id='b9d74e28f5c03a61e8d294fc';from='6f56c867548c16af9182acbd';domain='culture';title='卡恰尔半岛·三族讲法';claims=@('kach-bat','kach-nor','kach-stu','kach-conf');text=@(
'三族人讲半岛的旧事，各讲各的道理。巴旦尼亚人的讲法：他们借斯特吉亚人的斧头赶走诺德人，末了引狼入室，地也没保住。诺德人的讲法：巴旦尼亚人设了局，雇来的人打完仗反客为主。斯特吉亚人的讲法：两家相争两败俱伤，他们坐收其成。三套讲法对不上，谁也做不得准。'
);spans=@(
@{b='巴旦尼亚人的讲法';e='地也没保住。';claims=@('kach-bat');layer='detail'},
@{b='诺德人的讲法';e='反客为主。';claims=@('kach-nor');layer='detail'},
@{b='斯特吉亚人的讲法';e='坐收其成。';claims=@('kach-stu');layer='detail'},
@{b='三套讲法对不上';e='做不得准。';claims=@('kach-conf');layer='detail'}
)},
@{key='dawn-mtn';place='黎明山脉';id='46c9e1b8a3f70d25c8e4a917';from='d766c02ab0c98cb7236341d8';domain='geography';title='黎明山脉·山岳';claims=@('dawn-loc','dawn-edge','dawn-foot');text=@(
'黎明山脉全称柯希·罗希尼，坐落在德夫赛格高原东缘，山势大体南北走向，是高原东边的天然界限。山麓生着针叶林与高山草甸。'
);spans=@(
@{b='黎明山脉全称柯希·罗希尼';e='天然界限。';claims=@('dawn-loc','dawn-edge')},
@{b='山麓生着针叶林';e='高山草甸。';claims=@('dawn-foot')}
)},
@{key='dawn-taboo';place='黎明山脉';id='6f3b92ad0e84c571d9b26e4a';from='d766c02ab0c98cb7236341d8';domain='culture';title='黎明山脉·禁忌与传说';claims=@('dawn-legend','dawn-taboo');text=@(
'山里相传神话时代的暴君阿赫哈克双肩生蛇，御所藏在山脉深处；黑魔法与古老仪轨的故事，至今在周边部族口中流传。凭着这些相传的忌讳，山腹被当作禁地：不经允许踏进深处，即是冒犯山灵与先祖。当地部族守着这条规矩，把外人挡在山外。'
);spans=@(
@{b='山里相传神话时代';e='部族口中流传。';claims=@('dawn-legend');layer='rumor'},
@{b='凭着这些相传的忌讳';e='挡在山外。';claims=@('dawn-taboo');layer='detail'}
)},
@{key='dawn-stew';place='黎明山脉';id='94e7d2c5b8a36f10e5c7d948';from='d766c02ab0c98cb7236341d8';domain='politics';title='黎明山脉·归属与守护';claims=@('dawn-stew','dawn-ctrl');text=@(
'库赛特西征之后，山地便归了合儿必特部治下。合儿必特人不强改当地风俗，靠通婚、随俗一代代融进山民之中，并以山地的看守人自居——这是编年史的讲法。至于如今山里实际由谁做主，旧书没有可依据的记述，须待当世证据。'
);spans=@(
@{b='库赛特西征之后';e='编年史的讲法。';claims=@('dawn-stew');layer='detail'},
@{b='至于如今山里实际由谁做主';e='须待当世证据。';claims=@('dawn-ctrl')}
)},
@{key='der-vill';place='德里亚特';id='a8c5f09d3e62b741d0f95e63';from='7b7de79b571f0addb4d37eaf';domain='geography';title='德里亚特·村庄';claims=@('der-vill');meta=@('der-reg');text=@(
'德里亚特是卡琉斯堡左近的一座村庄，夹在瓦尔切格湾和埃博半岛之间的山脊上。'
);spans=@(
@{b='德里亚特是卡琉斯堡左近';e='山脊上。';claims=@('der-vill')}
)},
@{key='der-furs';place='德里亚特';id='c1d947ae8b06f35e92a47d0f';from='7b7de79b571f0addb4d37eaf';domain='economy';title='德里亚特·毛皮生计';claims=@('der-furs','der-oil','der-trade','der-cloak','der-lamp','der-road');text=@(
'村民上山下海讨生活：山里捕河狸、水貂，海豹则要到湾里捉；毛皮和油脂是村里的主要出产。海豹还能炼油。',
'海豹的皮货在瓦尔切格湾那片水面上有名，进的是湾内各处的交易；做成斗篷顶得住水汽，卡琉斯堡守军过冬的皮袄，有些料子就出自德里亚特。海豹油点灯，烟少火亮。',
'只是路难走：进村要翻山脊、绕海湾，行商的说法是进去一趟很费功夫。至于把货运出去要多花多少脚钱，旧书没有明说，只能算作猜测。'
);spans=@(
@{b='村民上山下海讨生活';e='毛皮和油脂是村里的主要出产。';claims=@('der-furs')},
@{b='海豹还能炼油。';claims=@('der-oil')},
@{b='海豹的皮货在瓦尔切格湾';e='湾内各处的交易；';claims=@('der-trade')},
@{b='做成斗篷顶得住水汽';e='出自德里亚特。';claims=@('der-cloak')},
@{b='海豹油点灯';e='烟少火亮。';claims=@('der-lamp')},
@{b='只是路难走';e='只能算作猜测。';claims=@('der-road');layer='detail'}
)}
)

# ---------- 生成 ----------
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$count = 0
foreach ($d in $DOCS) {
    $fullText = ($d.text -join "`n`n")
    $spans = @()
    foreach ($sd in $d.spans) {
        $i = $fullText.IndexOf($sd.b)
        if ($i -lt 0 -or $fullText.IndexOf($sd.b) -ne $fullText.LastIndexOf($sd.b)) { throw "doc $($d.key): begin marker 不唯一或未找到: $($sd.b)" }
        if ($sd.ContainsKey('e')) {
            $j = $fullText.IndexOf($sd.e, $i)
            if ($j -lt 0) { throw "doc $($d.key): end marker 未找到: $($sd.e)" }
            $s = $i; $e = $j + $sd.e.Length
        } else { $s = $i; $e = $i + $sd.b.Length }
        $span = [ordered]@{
            target_locator = '#/target_text/offset/' + $s
            claim_ids = @($sd.claims | ForEach-Object { 'authoring_provisional.claim.' + $C[$_].id })
            source_origin_ids = @(($sd.claims | ForEach-Object { $C[$_].orig } | ForEach-Object { $_ }) | Sort-Object -Unique | ForEach-Object { 'authoring_provisional.origin.' + $_ })
            operation = 'rephrase'
            text_start = $s
            text_end = $e
            sentence_id = $d.key + '.' + $sd.b.Substring(0, [Math]::Min(8, $sd.b.Length))
            unsupported_additions = @()
            inference_type = 'none'
        }
        if ($sd.ContainsKey('layer')) { $span.layer = $sd.layer; $span.layer_note = 'grants/profile 映射推迟到 Authoring 投影批次对照实体登记表落位' }
        $spans += $span
    }
    $claimsOut = @()
    foreach ($k in ((@($d.claims) + @($d.meta)) | Where-Object { $_ })) {
        $cd = $C[$k]
        $claimsOut += [ordered]@{
            claim_id = 'authoring_provisional.claim.' + $cd.id
            epistemic_kind = $cd.kind
            subject = $cd.s
            predicate = $cd.p
            object = $cd.o
            perspective = $cd.persp
            time_scope = 'historical_or_current_unknown'
            confidence = 'medium'
            source_origin_ids = @($cd.orig | ForEach-Object { 'authoring_provisional.origin.' + $_ })
            polarity = 'affirmed'
            revision_note = $cd.note
        }
    }
    $meta = @()
    if ($d.ContainsKey('meta')) { $meta = @($d.meta | ForEach-Object { 'authoring_provisional.claim.' + $C[$_].id }) }
    $out = [ordered]@{
        schema_version = 'awake.worldbook.migration.semantic-rewrite-candidate.v1'
        batch_id = 'semantic-rewrite.download-20260903'
        revision_line = 'r3'
        base_revision = 2
        target_revision = 3
        source_snapshot_id = 'semantic-pilot30.download-20260903'
        candidate_id = 'authoring_provisional.document.' + $d.id
        split_from = 'authoring_provisional.document.' + $d.from
        b4_split_note = '按 B4 条目边界拆分：本档为一个调取主题，源条目的其余主题见同批其它拆分档'
        place_cluster = $d.place
        place_cluster_note = '关联层元数据：同地文档聚合与联想顺序提示，不作调取容器、不作权限条件（参照二级主题批次先例）'
        status = 'needs_review'
        title = $d.title
        domain = $d.domain
        content_tier = 'pending'
        decision_record = 'SEMANTIC-R3-DECISION-RECORD-20260911'
        writing_rules = 'docs/worldbook-migration/tools-r3/WRITING-RULES.md'
        claims = $claimsOut
        metadata_claims = $meta
        quote_refinement_pending = 'origin 仍绑定整 variant 引文，子引文细化推迟到 Authoring 投影批次'
        target_text = $fullText
        target_spans = $spans
        review = @{ understood_rewrite = 'pending'; reviewer = 'author.pending'; review_state = 'needs_review'; rejection_reason = $null }
        source_snapshot_sha256 = $SNAP
    }
    $path = Join-Path $OutDir ($d.key + '.r3.json')
    [IO.File]::WriteAllText($path, ($out | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding $true))
    $count++
    Write-Host ("WROTE {0}  claims={1} spans={2} textlen={3}" -f $path, $claimsOut.Count, $spans.Count, $fullText.Length)
}
Write-Host "DONE: $count documents"
