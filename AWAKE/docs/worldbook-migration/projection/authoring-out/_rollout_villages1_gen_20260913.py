# -*- coding: utf-8 -*-
"""百科化村庄铺开批 1：阿塞莱 40 村全量（含乌格巴补档特例）→ 40 档。
数据源＝DB bannerlord_settlements（village，描述文 token→CNs 中文全文）。
【乌格巴补档特例】官方 CNs 本地化 `Settlements.Settlement.text.castle_village_A7_1`
错挂为别处「执政官加里俄斯流放名单」叙事文（繁中/英/日等 11 语言均正常）；
补档口径：快照行＋引文用官方 EN 原文（A 级逐字），简中叙述为转写，注记入快照行。
双写：authoring-out ＋ workspace authoring。红线同城镇批：JSON 往返断引用 +
ignore_aliases；slug 不进文档体；六身份显式落点；grant.min_detail==layer。
"""
import io, os, json, re, hashlib, sqlite3, yaml

OUT = os.path.dirname(os.path.abspath(__file__))
WS_AUTH = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
SRC_ID = "source.calradia.game.villages-desc"
SRC_VER = "bannerlord-1.3.15.110062"
SNAP = "game-villages-desc.txt"

# 官方 CNs 错挂的村（描述文与村无关，见文件头挂账说明）：乌格巴补档特例——
# 快照与引文用官方 EN 原文（A 级逐字），简中叙述为转写（标注见快照行与验收报告）。
UQBA_SID = "castle_village_A7_1"
UQBA_NOTE = "注：官方CNs该条描述文错挂为别处叙事（繁中/英/日等11语言正常），引文依官方EN原文，简中叙述为转写"

# ---------- L2 观感文案（人工层）：sid -> (assert, rumor, detail) ----------
L2 = {
 "castle_village_A1_1": ("突比力斯是沙拉斯湾的渔村，阿塞莱人亦称此湾为沙瑞兹；村民驾船在岛屿间的浅滩捕捞金枪鱼、沙丁鱼与鲨鱼。",
   "突比力斯人管那片海叫沙瑞兹，老人传下来的叫法。鱼汛一到全家上船，鲨鱼都敢碰。",
   "突比力斯的渔产走沙拉斯湾的岛间水路——金枪鱼与沙丁鱼按汛期定价，鲨鱼油是副业；外乡船进湾要先懂本地浅滩的规矩。"),
 "castle_village_A1_2": ("法纳卜坐落在沙瑞兹湾与耶什姆内海之间的低矮海角上，海风携带的雨水刚好支撑一片橄榄园。",
   "法纳卜那个海角，风里的水汽养橄榄树正正好。村里人说这是海神赏的雨水，不多不少。",
   "法纳卜的橄榄油产量不大但稳定——海角地窄、雨水靠海风，扩不了产；收油要赶榨季，价钱在开榨前谈最划算。"),
 "castle_village_A2_1": ("萨赫勒地处阿塞莱边陲的阿沙卜山脉低坡，山脉留住雨云与季风，贮水农田得以种植栗子与小麦。",
   "萨赫勒是阿塞莱地界的边儿了，山把雨云拦下来才有的麦子。山里人讲，这雨是山神收下的过路钱。",
   "萨赫勒是阿塞莱少见的粮产村——贮水农田的栗子与小麦在边地集市是硬通货；置产要先验水渠的引水权。"),
 "castle_village_A2_2": ("阿斯麦特位于阿沙卜山脉角落、毗邻沙漠深处，村民在罕见暴雨时截住山洪，用以浇灌椰枣园。",
   "阿斯麦特一年下不了几场雨，可一下就得赶紧把山洪截住。老人说水是老天爷的脾气，接得住才有枣吃。",
   "阿斯麦特的椰枣靠截洪灌溉——收成跟着暴雨年景走，年份差异大；买枣要问当年山洪大小，行家尝枣就知道年成。"),
 "castle_village_A3_1": ("艾因·巴力克的村民围绕开阔山谷里的水井定居，这道山谷将杰尔贾赖峭壁与塔玛尔山分开；干旱之地靠春季短草繁育马匹。",
   "艾因·巴力克那口井是全村的命根子。春天草一冒头马就肥了——草原上的老爷们都盯着这批马驹。",
   "艾因·巴力克的马驹按春草年成定品质——水井水量是硬约束；挑马要看春天的雨水，懂行的先摸草情再谈价。"),
 "castle_village_A3_2": ("代尔·哈瓦在耶什姆海当风的南岸，村民以露天银矿为生；富矿脉几世纪间已采空，今靠秘而不宣的手艺从矿石里提银。",
   "代尔·哈瓦山上的银子早挖得差不多了，可村里人还是有办法从石头里抠出银子来。什么法子，外人不许看。",
   "代尔·哈瓦的银产量底细外人摸不清——矿脉已贫、提炼的手艺是村中秘传；收银要认老主顾，生面孔压价也难收到真货。"),
 "castle_village_A4_1": ("迈代尼坐落在耶什姆海与杰尔贾赖峭壁之间的低地，村民以水井和旱谷小堤坝收集雨季地表水，灌溉岸边椰枣园。",
   "迈代尼人会管水——井和旱谷里的小坝都是祖上传下来的。谁坏了水规矩，全村不答应。",
   "迈代尼的水利是村产核心——堤坝与水井的维护分担决定灌溉份额；收椰枣先看当年雨季，再问水规有没有变动。"),
 "castle_village_A4_2": ("吉德纳尔坐落达玛尔河河漫滩，该河穿过杰尔贾赖峭壁，是阿塞莱人口最稠密之地；农民靠河水周期泛滥的淤泥种植椰枣。",
   "吉德纳尔挨着大河人挤人。河水一泛滥淤泥就是肥。村里人说这河是喂人的河，就是脾气得摸清。",
   "吉德纳尔的椰枣靠泛滥淤泥——年产稳但泛滥期即农忙锁期；此地近河人口密，短工与船运都不愁雇。"),
 "castle_village_A5_1": ("贾迈耶位于「拜赫尔·耶什姆」（翡翠之海）南岸，雨季短暂但气候很适合橄榄树生长。",
   "贾迈耶的海风带水汽。雨是不多，可橄榄树就好这一口——村里人管这叫海赏饭。",
   "贾迈耶的橄榄品质好但总量受雨季限制——榨季集中；混收外地果充本地果的把戏在此行不通，村里认树不认秤。"),
 "castle_village_A5_2": ("侯纳卜坐落于杰尔贾赖峭壁风化台地下的一座干谷中，村民利用春天短暂繁茂的山坡青草饲养马匹。",
   "侯纳卜的山谷一年就绿那么一阵。村里人掐着日子放马，说春天的草是攒了一年的劲儿长的。",
   "侯纳卜的马靠春草催膘——出栏集中在春末；错过草季马就掉膘，看马先看季节是本地规矩。"),
 "castle_village_A6_1": ("希巴勒·祖姆尔位于「奈赫尔·凯勒斯」（石灰河）畔，水清土沃可种谷物；往南不远即是只能种耐碱庄稼的盐碱地。",
   "希巴勒·祖姆尔的地是好地，可再往南走一步土就咸了。村里人说祖坟选得好，占住了河边的活土。",
   "希巴勒·祖姆尔是石灰河沿岸的谷产村——好地有限、南界即盐碱，扩产无门；收粮按村界算，越界地里的成色差一档。"),
 "castle_village_A6_2": ("拉迈萨位于耶什姆海东岸的开阔平原，附近有稀疏的金合欢树林，阿塞莱最好的马匹在此放养。",
   "阿塞莱最好的马在拉迈萨吃草——这话草原上没人反驳。那片平原带着海风，草里有咸味，马吃了壮。",
   "拉迈萨是阿塞莱马价的风向标——平原开阔、草场带海风，马驹品质全境头档；苏丹与部族首领的马都从这里出。"),
 "castle_village_A7_2": ("本盖兹坐落于分割杰尔贾赖峭壁与塔玛尔山的大旱谷入口，村民打井取旱谷地下水灌溉橄榄林。",
   "本盖兹的井是村里一代代人凿出来的。旱谷口的风大，可井水没断过——老人说谷底下有暗河。",
   "本盖兹的橄榄靠井灌——井权即产权，均分世代相承；收果要认井份，谁家的果子谁家的价。"),
 "castle_village_A8_1": ("坦姆努坐落在珀拉斯海沙嘴分割出的大潟湖边，村民刮取浅水区的黏土卖给当地陶工。",
   "坦姆努的湖泥是好泥，陶工抢着要。村里孩子都会认泥——哪片的细哪片的杂，一眼看穿。",
   "坦姆努的黏土按湖区分层成色定价——陶坊认泥如认粮；刮泥有季节限制，风浪大的日子不出工。"),
 "castle_village_A8_2": ("库加坐落在耶什姆海以东的小山丘脚下，阿塞莱沙漠在此过渡为荒漠草原；村民打井浇灌椰枣树林。",
   "库加在沙漠的边上，再往东就是荒草滩了。井水浇枣，枣甜水也甜——村里人说这井是先祖验过的。",
   "库加是沙漠草原过渡带的枣产村——井深与出水量定产量；此地亦是进入荒漠草原前最后的补给点，水价有行规。"),
 "castle_village_A9_1": ("巴里哈勒是杰尔贾赖峭壁台地旱谷边的绿洲村，以短命而肥美的春草饲养马匹闻名。",
   "巴里哈勒那点绿洲水，养出来的马倒是好马。人说绿洲的草背着太阳长，马吃了不上火。",
   "巴里哈勒的马产受绿洲水源上限约束——量少质稳；每年春草季即马市季，行商提前半月来占客栈。"),
 "castle_village_A9_2": ("瓦达尔坐落于杰尔贾赖峭壁向陆侧的干河谷，紧邻沙砾平原与沙丘；春冬海云降下最后的水，勉强滋养养马草地。",
   "瓦达尔那地方，天上的云走到那儿就把最后一点水洒了。村里人说这是海舍不得他们，留的救命水。",
   "瓦达尔的草场靠海云余泽——年景看风向与云路，波动比别村大；收马驹要押冬春两季的云情，行情随年成起落。"),
 "village_A1_1": ("塔舍巴坐落于耶什姆海西岸，渔民全年大多可在风平浪静的保护水域捕捞金枪鱼与沙丁鱼。",
   "塔舍巴的湾是老天爷赏的避风港，一年到头能出海。古亚兹城里的鱼市，一半是咱们村养的。",
   "塔舍巴的渔汛稳——保护水域全年可作业，供给古亚兹鱼市；渔权按船入册，外来船挂靠本地船东才能下网。"),
 "village_A1_2": ("巴格位于塔玛尔山巨大的花岗岩露头山脚下，海风带来的高地雨水刚好够种橄榄树。",
   "巴格人靠山吃饭——塔玛尔山把海风拦下来，落到树上的就是雨。石头缝里都能种出橄榄来。",
   "巴格的橄榄园嵌在花岗岩坡地——单产不高但果质浓；山体挡风，榨季比海角村晚几天，收油的可两头跑。"),
 "village_A1_4": ("希卜莱特坐落于塔玛尔山脚下，村民将花岗岩石缝中积蓄的雨水引入村里的椰枣园。",
   "希卜莱特的枣园喝的是石头缝里的水。哪条石缝存水多，村里老辈人心里有一本账。",
   "希卜莱特的引水石渠是村产命脉——渠权随地产走；枣园转让要先过水账，没算石缝水源的地价不算数。"),
 "village_A2_2": ("艾布·希位于凯勒斯河畔、该河流向南方不毛之地；此地原为古河床，土地贫瘠碱化，唯椰枣树仍能生长。",
   "艾布·希的地底下是老河床，土是咸的，别的庄稼不爱长，就椰枣皮实。村里人说枣树是穷人的亲兄弟。",
   "艾布·希是碱地专耕村——作物单一，椰枣几乎全产出；耐碱品种与老井淡水配着用，地价便宜但水权贵。"),
 "village_A2_3": ("胡加位于阿塞莱远东的蒂亚具斯河畔，土地多为贫瘠荒漠草原，唯春季长出适合喂马的花草。",
   "胡加是天边儿上的村子了，再往东就是没人的荒滩。春天河滩上的花一开，马就有得吃了。",
   "胡加地处阿塞莱东缘——马产随春汛走，量不大但地偏价实；此地亦是与东方部落的暗市接头点，生人勿探。"),
 "village_A3_1": ("阿卜巴坐落在沙漠深处边缘、杰尔贾赖峭壁向陆侧山脚下，山体径流在地下积蓄成泉，小麦在绿洲耕地里生长。",
   "阿卜巴的泉是山上喂的，一年到头不断。沙漠里能看见麦浪，外地人来了都不信自己的眼睛。",
   "阿卜巴是沙漠绿洲粮村——泉眼水量定耕地边界；麦价不受年成大波动，是周边驻军与商队的稳粮源。"),
 "village_A3_3": ("比尔·赛义夫坐落在杰尔贾赖峭壁的水井与湖泊之间，村民挖至浅层地下水区收集黏土，卖给纳哈撒沙漠的陶工。",
   "比尔·赛义夫人是伴着水井长大的，井底下掏出来的泥是宝。沙漠那头的陶匠，全认这儿的泥。",
   "比尔·赛义夫的黏土跨沙漠卖给纳哈撒陶坊——路远价高但靠驼队；挖泥深度有讲究，过了淡水层泥就废。"),
 "village_A4_1": ("加卜拉卜坐落于珀拉斯海南岸的开阔海湾边，低洼的海岸线适合建设盐田。",
   "加卜拉卜的海滩是白花花的，晒的全是钱。老人说这湾的水咸得能腌住岁月——盐田就是村里的田。",
   "加卜拉卜的盐田看天吃饭——日照与风定产；盐是官控物资，私贩查得严，正经渠道走拉齐赫的海船。"),
 "village_A4_2": ("穆苏姆靠近耶什姆海东岸，阿塞莱沙漠在此过渡为散布金合欢与灌木的荒漠草原，村民打井浇灌椰枣林。",
   "穆苏姆的井是村里几辈人淘出来的。金合欢树开花的时候枣花也开了——村里人说这是双喜。",
   "穆苏姆的椰枣与海东诸村同档——井灌定产；此地近拉齐赫海路，枣干装船外销是常例，陆运反而少。"),
 "village_A4_4": ("多加是耶什姆海岸边的渔村，村民乘三桅小帆船捕捞可长过人身的鲈鱼，并需提防海湾与沼泽里的咸水鳄。",
   "多加海里的鲈鱼能长到比人还长！可水里也蹲着鳄鱼跟人抢食。村里人出海前都要祭一祭。",
   "多加的大鲈鱼是稀货——三桅小船近海作业，风险是咸水鳄；鱼价高、保险更高，收鱼的行商要认老船队。"),
 "village_A5_1": ("马赫卢勒坐落于杰尔贾赖峭壁与阿沙卜山之间的开阔峡谷，地下水仅够口粮田；村民春雨后在山上丰草坡放牧养马补贴生计。",
   "马赫卢勒的地水就够糊口，真金白银全在马身上。春雨一过全村赶着马上山，跟迁徙似的。",
   "马赫卢勒是粮马两掺的小村——口粮自给、马匹换钱；春牧的坡地按氏族分片，外来的马进不了场。"),
 "village_A5_2": ("利瓦斯坐落于耶什姆海南侧的宽阔山谷内，农田有限，以荒漠春草喂养的马匹最为出名。",
   "利瓦斯的山谷宽，风一吹草浪翻滚。地里打不出几把粮，可马市上一匹好驹换半年的粮。",
   "利瓦斯的马谷名气在外——谷地避风、春草油润；马驹订户多是胡比亚与周边城的老主顾，生客难插队。"),
 "village_A5_3": ("瓦勒塔斯傍着「拜赫尔·耶什姆」（翡翠之海），低地非常适合种植亚麻。",
   "瓦勒塔斯的低地上亚麻连成片，开花时蓝汪汪一片。村里人沤麻的塘，外乡人闻着躲，我们闻着是钱味。",
   "瓦勒塔斯是阿塞莱少见的亚麻村——沤麻要水塘与气候配合；麻纤供给帆布与绳索商，海船订单优先。"),
 "village_A6_1": ("米贾伊特坐落在达玛尔河入海口旁。河水到这儿缓下劲，一路驮来的淤泥撂在岸上，积出阿塞莱数一数二的肥地。",
   "米贾伊特的地肥得冒油——大河把上游的土全送到这儿。撒纳拉港的粮食，一半打我们村过的手。",
   "米贾伊特是达玛尔河口的粮仓村——淤泥沃土产额稳居阿塞莱前茅；粮多船也多，与撒纳拉港的粮栈绑得紧。"),
 "village_A6_2": ("哈穆沙瓦在达玛尔河下游、快入海的地方。河水驮来的淤泥一路堆到这儿，堆出阿塞莱最肥的一片地。",
   "哈穆沙瓦跟米贾伊特隔着不远，同喝一条河的肥。村里人夸口：我们的麦磨出来的面，能照见人影。",
   "哈穆沙瓦与河口诸村共吃达玛尔河的淤泥——产额互为参照；入海口水位定船期能否上溯收粮，行家先看水再订约。"),
 "village_A6_3": ("贾哈西姆坐落于「拜赫尔·耶什姆」（翡翠之海）南岸，温暖水域盛产金枪鱼、鲣鱼与沙丁鱼。",
   "贾哈西姆的海暖，鱼也勤快。汛期一来网都沉手——村里人说暖水养懒人，可养勤快的鱼。",
   "贾哈西姆渔汛期长——暖水鱼种轮番上汛；鲜鱼走撒纳拉港，咸干货走南岸陆路，两头都有行价。"),
 "village_A6_4": ("纳赫兰坐落于杰尔贾赖峭壁被达玛尔河分割形成的冲积平原，土壤每年靠洪流淤泥恢复肥力，小麦产量很高。",
   "纳赫兰人不怕发水，就怕不发——洪水一过，地就跟上了肥似的。老人说这平原是河亲手垒的。",
   "纳赫兰的麦产靠年度洪泛——洪大则丰、洪小则歉，行情随上游水情走；此地的粮契都写明按当年水情议价。"),
 "village_A7_2": ("拜特·哈提夫坐落于达玛尔河冲积平原，石质土壤限制灌溉效果，山上的春草却能让马匹茁壮成长。",
   "拜特·哈提夫的地看着平，底下全是石头，种粮不成。可山上的春草喂马，一头头油光水滑。",
   "拜特·哈提夫是石地牧村——粮不行、马顶上；紧邻阿斯凯尔河市，马驹就地换粮是常例，价钱两头都熟。"),
 "village_A7_3": ("马卜瓦兹俯瞰蜿蜒穿过杰尔贾赖峭壁台地的达玛尔河，村民在河漫滩的淤泥上种植小麦与其他作物。",
   "马卜瓦兹高高地瞅着大河，滩上的地一年肥一年。村里人说：河是我们的老爷，滩是我们的田。",
   "马卜瓦兹的河滩田随水位涨落——种多种少先看头场水；高台地势不怕淹，粮栈与晒场是村里的硬产业。"),
 "village_A7_4": ("扎勒姆坐落于「枯焦之门」——达玛尔河自纳哈撒沙漠发源并穿过杰尔贾赖峭壁之处，河水淤泥使当地小麦收成很好。",
   "扎勒姆守着枯焦之门——沙漠里出来的河，头一段水最金贵。老人说河是从死地里逃出来的，带的全是活气。",
   "扎勒姆卡着达玛尔河出沙漠的头一隘口——上水情先到、下水村后知，粮价资讯比别村快半步。"),
 "village_A8_1": ("艾兹贝特·纳胡勒位于俯瞰「拜赫尔·耶什姆」（翡翠之海）的多岩山角上，村民以水井取石缝间积蓄的地下水灌溉麦田。",
   "艾兹贝特·纳胡勒的麦田全靠石头缝里攒的水。哪条缝养哪块田，村里的水账比婚书还郑重。",
   "艾兹贝特·纳胡勒的水账是产权核心——井与石缝水源绑定地块；傍着加西拉的泉水名号，麦价能多讲一分。"),
 "village_A8_2": ("阿卜甘位于「拜赫尔·耶什姆」（翡翠之海）南岸的一处小潟湖边，村民捕捞金枪鱼、沙丁鱼、鲣鱼与鲨鱼。",
   "阿卜甘的潟湖是鱼窝子，风浪进不来。村里孩子踩着水就能捞着沙丁鱼——鲨鱼进来反倒出不去了。",
   "阿卜甘的潟湖渔场风险低、产量稳——鲨鱼油与鱼干是两宗副产；湖口航道窄，渔权与航道维护费摊到每条船。"),
 "castle_village_A7_1": ("乌格巴坐落于珀拉斯海沿岸，村民收集山丘的雨水与径流，浇灌田地与椰枣园。",
   "乌格巴守着珀拉斯海，却不靠海吃饭——村里人蓄的是山上的雨水。枣园喝的就是这口活水。",
   "乌格巴的椰枣靠蓄雨灌溉——雨水年成定产量，蓄水与引水设施是村产核心；地处阿塞莱内陆与海岸之间，枣干就近换粮换盐是常例。"),
}

RUMOR_GRANTS = [
    ("profile.commoner", "local"), ("profile.villager", "local"),
    ("profile.tavernkeeper", "faction"), ("profile.ransom_broker", "faction"),
    ("profile.townsfolk", "regional"), ("profile.notable", "regional"),
    ("profile.merchant", "faction"), ("profile.headman", "national"),
    ("profile.soldier", "national"), ("profile.noble", "elite"),
]
DETAIL_GRANTS = [
    ("profile.headman", "national"), ("profile.merchant", "faction"),
    ("profile.soldier", "national"), ("profile.noble", "elite"),
]
REG_BIND = {
    "profile_registry_version": "1.0.0",
    "profile_registry_hash": "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5",
    "referral_registry_version": "1.0.0",
    "referral_registry_hash": "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD",
}


def sha(b):
    return hashlib.sha256(b).hexdigest().upper()


def main():
    con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
    con.row_factory = sqlite3.Row

    def loc(string_id, lang="CNs"):
        row = con.execute(
            "SELECT text FROM localization_entries WHERE stringId=? AND language=? LIMIT 1",
            (string_id, lang)).fetchone()
        return row["text"] if row else None

    rows = con.execute(
        "SELECT settlementId,name,descriptionText,culture FROM bannerlord_settlements "
        "WHERE settlementType='village' AND culture='Culture.aserai' ORDER BY settlementId").fetchall()

    # ---- 1) 快照：每村一行描述文中立转录（首句作 A 级引文锚） ----
    lines, meta = [], {}
    for r in rows:
        sid = r["settlementId"]
        nm = r["name"] or ""
        tok = nm.split("{=")[1].split("}")[0] if "{=" in nm else ""
        en = nm.split("}", 1)[1].strip() if "}" in nm else sid
        cn = loc(tok)
        assert cn, sid
        desc = r["descriptionText"] or ""
        dtok = desc.split("{=")[1].split("}")[0] if "{=" in desc else ""
        desc_cn = loc(dtok)
        if sid == UQBA_SID:
            # 官方 CNs 错挂特例：快照行录官方 EN 原文＋注记；引文取 EN 首句
            desc_en = loc(dtok, "std_settlements_xml.xml")
            assert desc_en and desc_en.startswith("Uqba"), sid
            desc_text = re.sub(r"\s+", " ", desc_en).strip()
            seg = (f"{cn} | {desc_text} | 出处 bannerlord_settlements#{sid} "
                   f"| 译名token {tok} | 描述token {dtok} | {UQBA_NOTE}")
            meta[sid] = {"cn": cn, "en": en, "lang": "en", "desc": desc_text}
        else:
            assert desc_cn, sid
            desc_text = re.sub(r"\s+", "", desc_cn)
            seg = f"{cn} | {desc_text} | 出处 bannerlord_settlements#{sid} | 译名token {tok} | 描述token {dtok}"
            meta[sid] = {"cn": cn, "en": en, "lang": "zh-CN", "desc": desc_text}
        lines.append(f"village.{sid} => {seg}")
    snap_text = "\n".join(lines) + "\n"
    file_hash = sha(snap_text.encode("utf-8"))
    io.open(os.path.join(WS_AUTH, "sources", SNAP), "w", encoding="utf-8", newline="\n").write(snap_text)

    # ---- 2) 来源登记 ----
    reg = {
        "source_id": SRC_ID, "source_version": SRC_VER, "source_nature": "game_snapshot",
        "universe": "awake_current", "era": "current", "locator_root": SNAP,
        "source_content_hash": file_hash, "content_tier": "base",
        "license_status": "permitted", "use_status": "active", "valid_until": None,
        "imported_at": "2026-09-13T00:00:00Z", "normalization_version": "utf8-lf-no-bom-v1",
    }
    with io.open(os.path.join(WS_AUTH, "sources", "source-game-villages-desc.yaml"), "w",
                 encoding="utf-8", newline="\n") as f:
        yaml.safe_dump(reg, f, allow_unicode=True, sort_keys=False)

    # ---- 3) 档（双写：authoring-out + workspace authoring） ----
    made = []
    for sid, m in sorted(meta.items()):
        assert sid in L2, sid
        desc_text = m["desc"]
        if m["lang"] == "en":
            first_sent = desc_text.split(". ", 1)[0] + "."
        else:
            first_sent = desc_text.split("。", 1)[0] + "。"
        assert first_sent in snap_text, sid
        qhash = sha(first_sent.encode("utf-8"))
        src = {"source_id": SRC_ID, "source_version": SRC_VER,
               "source_content_hash": file_hash,
               "locator": f"bannerlord.villages#{sid}",
               "quote_hash": qhash, "quote": first_sent}
        a_text, rumor, detail = L2[sid]
        slug = m["en"].lower().replace(" ", "-").replace(".", "")
        doc = {
            "schema_version": "awake.worldbook.authoring.v1",
            "revision": 1,
            "id": f"doc.geography.village-{slug}",
            "title": {"zh-CN": m["cn"], "en": m["en"]},
            "status": "needs_review",
            "domain": "geography",
            "subdomain": "settlements",
            "universe": "awake_current",
            "era": {"key": "current", "certainty": "bounded"},
            "content_tier": "base",
            # 2026-09-17：类别词「村庄」不再进 aliases。
            # 它挂在几百条上 ⇒ 覆盖 > 40 ⇒ 被索引卫生 R1 剔掉 ⇒ 指不到任何东西，
            # 还留了「谁都能沾」的坑（德里亚特的「德里亚特·村庄」就是这么劫走 272 条泛问的）。
            # 类别词已升格为概念词条 doc.geography.settlement-types-village。
            "aliases": {"zh-CN": [m["cn"]], "en": [m["en"], sid]},
            "summary": {"zh-CN": a_text},
            "registry_bindings": dict(REG_BIND),
            "sources": [src],
            "authority": {"owner": "awake_canon", "conflict_policy": "canon_wins"},
            "assertions": [{
                "id": f"assertion.village-{slug}-1", "revision": 1, "kind": "fact",
                "text": {"zh-CN": a_text},
                "sources": [src],
                "expressions": [
                    {"id": f"expr.village-{slug}-rumor", "revision": 1, "layer": "rumor",
                     "text": {"zh-CN": rumor}, "sources": [src],
                     "grants": [{"profile_id": p, "scope": s, "min_detail": "rumor"}
                                for p, s in RUMOR_GRANTS],
                     "denies": []},
                    {"id": f"expr.village-{slug}-detail", "revision": 1, "layer": "detail",
                     "text": {"zh-CN": detail}, "sources": [src],
                     "grants": [{"profile_id": p, "scope": s, "min_detail": "detail"}
                                for p, s in DETAIL_GRANTS],
                     "denies": []},
                ],
            }],
        }
        doc = json.loads(json.dumps(doc))

        class NoAlias(yaml.dumper.Dumper):
            def ignore_aliases(self, data):
                return True

        text = yaml.dump(doc, Dumper=NoAlias, allow_unicode=True, sort_keys=False,
                         default_flow_style=False, width=100)
        assert "&id" not in text and "*id" not in text
        fn = f"village-{slug}.yaml"
        io.open(os.path.join(OUT, fn), "w", encoding="utf-8", newline="\n").write(text)
        io.open(os.path.join(WS_AUTH, fn), "w", encoding="utf-8", newline="\n").write(text)
        made.append(fn)
    print("generated:", len(made), "villages; snapshot hash:", file_hash[:16])
    for x in made:
        print("  ", x)


if __name__ == "__main__":
    main()
