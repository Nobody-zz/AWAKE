# -*- coding: utf-8 -*-
"""百科化村庄铺开批 2：帝国 97 村之前 49 村（castle_village_EN*/ES*/EW1-7 + EW8_1）→ 49 档。
数据源＝DB bannerlord_settlements（village，描述文 token→CNs 中文全文）。
独立快照与来源登记（source.calradia.game.villages-desc-emp1），不触碰批 1 阿塞莱快照 hash。
双写：authoring-out ＋ workspace authoring。红线同批 1：JSON 往返断引用 +
ignore_aliases；slug 不进文档体；六身份显式落点；grant.min_detail==layer。
"""
import io, os, json, re, hashlib, sqlite3, yaml

OUT = os.path.dirname(os.path.abspath(__file__))
WS_AUTH = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
SRC_ID = "source.calradia.game.villages-desc-emp1"
SRC_VER = "bannerlord-1.3.15.110062"
SNAP = "game-villages-desc-emp1.txt"

# ---------- L2 观感文案（人工层）：sid -> (assert, rumor, detail) ----------
L2 = {
 "castle_village_EN1_1": ("瓦拉戈斯坐落在阿里斯河畔，高地的细小河流由此汇入深邃的俄佛堤斯湖；当地土壤并不十分肥沃，但足够饲养绵羊。",
   "瓦拉戈斯的地薄，庄稼不上脸，羊倒是吃得肥。村里人说土地撇下他们的时候，顺手给了羊。",
   "瓦拉戈斯的羊毛按春秋两剪出货——贫地养不了大群，量小质稳；收羊先看湖畔草情，雨水好的年头毛色油亮。"),
 "castle_village_EN1_2": ("艾俄里亚位于帝国腹地土壤肥沃的阿里斯谷，当地方言至今掺杂帕拉语词汇——帝国人到来前，大陆中央的居民说的是帕拉语。",
   "艾俄里亚人说话带古腔，老人讲那叫帕拉话，比帝国还老。麦子也是老资格，年年稳产。",
   "艾俄里亚是阿里斯谷的老粮村——冲积土麦产稳、离帝国腹地集市近；外乡粮商收麦要会听口音，本地人认话不认人。"),
 "castle_village_EN2_1": ("罗卡那位于拉科尼斯湖的东岸，湖水缓和了北方冬季的寒冷，为种植桑树、产出蚕丝提供了条件。",
   "罗卡那靠湖吃饭——湖水捂着桑树过冬。村里人说这湖是小火盆，蚕比人过得舒坦。",
   "罗卡那的蚕丝成于湖畔小气候——冬季霜害少、桑叶产量稳；丝价看春季头茬茧，湖上早雾多的年头丝质最好。"),
 "castle_village_EN2_2": ("诺耳塔尼萨是拉科尼斯湖南岸的一座渔村，村民在湖泊的浅滩放置渔网和渔笼，捕获拟鲤、鲷鱼、鲈鱼与小龙虾。",
   "诺耳塔尼萨的网子一年四季不闲。浅滩里拟鲤、鲷鱼、小龙虾什么都往笼里钻——村里人讲，湖底比粮仓还满。",
   "诺耳塔尼萨四季有渔获——浅滩网笼各管一季，鲜鱼走南岸陆路、虾干耐存；收鲜要赶晨网，晌午的鱼就压价。"),
 "castle_village_EN3_1": ("雷索斯位于德律亚山脉的一片高原上，高原牛靠着当地的高地牧草茁壮成长。",
   "雷索斯的牛是高原来的，风大草硬牛反而壮。外乡人说那牛有山劲儿，拉犁顶两个壮汉。",
   "雷索斯的高原牛耐寒抗病——夏牧高原、冬前出栏是惯例；贩牛要赶霜降前，过了山牛掉膘快。"),
 "castle_village_EN3_2": ("底俄帕利斯位于德律亚山脉的一座山谷中，冬季虽寒冷，肥沃的土壤使其成为北帝国重要的粮仓之一。",
   "底俄帕利斯的山谷是北边的大粮仓——冬天冷得能把人冻裂，可土肥得冒油。村里人说冷是麦子的被。",
   "底俄帕利斯的麦产集中冬前运出——谷地路窄、雪期封山是硬窗口；大宗收粮要提前订垛，雪后价钱翻着走。"),
 "castle_village_EN4_1": ("伽俄斯位于帝国东边密泽亚德的宽阔山谷，村庄得名于曾占据此地的游牧民族语言中「牛」的叫法。",
   "伽俄斯这名字就是老游牧话里的「牛」。地是别人的旧地，牛倒养得比谁都好——村里人讲这叫物归原主。",
   "伽俄斯的牛市沾着游牧旧路的光——谷宽草足、东来客商顺路；买牛认烙印，旧部落的印子至今是信誉。"),
 "castle_village_EN4_2": ("忒密斯位于帝国腹地密泽亚德的石灰岩峭壁之下，当地丘陵上生长的草非常适合养羊。",
   "忒密斯的羊就爱啃峭壁底下那坡草。村里人说石头缝里长出的草带矿气，羊吃了不生病。",
   "忒密斯的羊群贴峭壁放牧——狼害少、草场有限扩不了群；收羊看春羔成活，峭壁落石伤畜是年景的一部分。"),
 "castle_village_EN5_1": ("阿特里翁坐落于涅维斯谷中，冰冷的缓流由此汇入拉科尼斯湖，当地土地适合种植桑树、用于生产蚕丝。",
   "阿特里翁的水是雪水，凉得扎手。可桑树偏喜欢这份凉——村里人说蚕喝着冷水吐的丝也带着韧劲。",
   "阿特里翁的丝以韧著称——冷溪谷地霜期晚、桑叶厚；收丝认水的来路，涅维斯谷的丝价常年比湖北岸高半档。"),
 "castle_village_EN5_2": ("马珊加拉坐落于涅维斯河岸，这里曾是沼地树林，人们把水排干，将其变为牧牛地。",
   "马珊加拉的草地是老辈人一寸寸排出来的。村里人说脚底下每块草皮都曾是水底。",
   "马珊加拉的排水渠系是村产命根——渠淤则草场返沼，年年清淤是公摊；牛价里含渠钱，懂行的看渠不看牛。"),
 "castle_village_EN6_1": ("阿塔科尼亚坐落在德律亚山前的一片高原上。山上奔下来的溪水冲开坡面，露出底下成片的盐，本地人祖祖辈辈都在那儿采。",
   "阿塔科尼亚的盐是山上水冲出来的。村里人说这条溪是搬盐的力工，冲了一千年才把盐面冲给人看。",
   "阿塔科尼亚的盐矿靠溪水露头——采面随水道改移，春汛后开新面是惯例；盐价与溪情挂钩，旱年反而好走货。"),
 "castle_village_EN6_2": ("珀塔米斯坐落在德律亚山脉的一处低谷，人们自前卡拉德时代就在此养牛，过往商旅很容易买到牛奶和黄油。",
   "珀塔米斯养牛养到帝国还没影儿的年月。路过的人买奶买黄油不用进村——路口就有人等着。",
   "珀塔米斯的奶黄油品是路口生意——不进村的买卖省工省价；黄油按季腌桶存运，商队整桶订价、散客零打吃亏。"),
 "castle_village_EN7_1": ("厄毗诺萨位于帝国极北之地、伊勒坦运输线冰川湖的南岸，拉科尼斯湖与塔奈西斯湖之间船只川流不息；当地草甸葱郁，放养着大群牛只。",
   "厄毗诺萨的牛听着两个湖的船号子长大。村里人说这里人少牛多，草都让牛占了便宜。",
   "厄毗诺萨走船路出牛——运输线湖网通两湖，牛半水路省膘；订牛要赶解冻后头几班船，晚了排到秋汛。"),
 "castle_village_EN7_2": ("庞斯是帝国最北端的定居点之一，因位于伊勒坦运输线的「交叉口」而得名；当地人从附近山丘开采盐矿。",
   "庞斯的名字就是「路口」的意思——船到这儿得换水道。村里人说守着路口的人，盐都不愁卖。",
   "庞斯的盐借运输线分销——两湖水道交汇处囤货方便；盐场在北丘，解冻季前囤盐是船户惯例。"),
 "castle_village_EN8_1": ("叙拉托斯坐落于塔奈西斯湖北岸的一处峭壁之上，寒冷潮湿的谷底让亚麻长得很好。",
   "叙拉托斯谷底潮得能拧出水，亚麻就爱这口。村里人说麻布的凉气是湖里带的。",
   "叙拉托斯的亚麻纤维长——湿谷沤麻是老法子，色青质韧；北岸布商按沤池订货，混池麻价砍半。"),
 "castle_village_EN8_2": ("特美摩斯坐落于塔奈西斯湖北端树木繁茂的丘陵地，许多村民以林为业，采伐橡树、山毛榉和松树销往湖边城镇。",
   "特美摩斯的人斧头不离手。橡木、山毛榉、松木分着卖——村里人说树跟人一样，各有各的去处。",
   "特美摩斯的木材按树种分档走湖运——橡木贵在船料、松木走量；冬伐春浮是惯例，浮运损耗算在买主头上要事先讲明。"),
 "castle_village_EN9_1": ("墨卡罗维亚俯瞰伽尔喀斯大瀑布，村庄筑于峭壁之上以免受强盗侵扰；飞流直下的瀑布拍过峭壁，将许多矿石暴露出来，村民以此为生。",
   "墨卡罗维亚的矿是瀑布给露出来的。村里人说水声吵归吵，吵走了强盗，露出了铁矿，两头划算。",
   "墨卡罗维亚的铁矿石随瀑季现面——春汛水大露矿多；峭壁地形运矿靠吊索，买矿自备驮力是规矩。"),
 "castle_village_EN9_2": ("阿加尔蒙紧挨伽尔喀斯河与弥戎河的汇流处，多林村庄的居民世代与相邻的斯特吉亚人通婚；战争的爆发令双方关系趋于紧张。",
   "阿加尔蒙家家都有斯特吉亚亲戚。以前两边的孩子满村跑，如今河对岸来人，先得看看带没带兵器。",
   "阿加尔蒙的木料走两河水路——汇流口集散方便；战事让北岸旧渠道断了半边，价钱比战前高出一截。"),
 "castle_village_ES1_1": ("俄德律萨坐落于帝国遥远的东部边境，蒂亚具斯河对岸即是库赛特汗国领土；当地气候干旱，橡树等仍生长于峡谷角落，支撑小规模的木材业。",
   "俄德律萨的树都躲在峡谷里。河对岸就是库赛特人——村里人说砍树得赶早，边界上的事说不准。",
   "俄德律萨的木材产量小而稳——峡谷料、旱地木纹密；边界紧张时河运改旱运，脚钱翻倍是常态。"),
 "castle_village_ES1_2": ("恺拉坐落于被称为卡勒萨的河流湖泊交织之地，卡勒萨河与蒂亚具斯河共同塑造的冲积平原可以种植小麦；再往东则是相当贫瘠的碱性土。",
   "恺拉的麦田是两条河合手堆出来的。村里人说往东一步土就咸了，守住这片冲积地就是守住饭碗。",
   "恺拉的麦产是东境的底粮——冲积平原肥力随汛期更新；粮价稳但有官粮征购，行商只能收余量。"),
 "castle_village_ES2_1": ("科雷尼亚是帝国境内最偏远的村庄之一，村民仍说着卡拉德语；它位于塔奈西斯湖东南方的平原、阿克坎丘陵附近，村民种植谷物，随时提防犯边的掠袭者。",
   "科雷尼亚是帝国最后一块还讲卡拉德话的地方。村里人一边种麦一边竖着耳朵听山口——掠袭者说来就来。",
   "科雷尼亚的谷物自带边地价——掠袭频繁、粮得囤村中地窖；买粮要结队进村，散客被抢不算村里的账。"),
 "castle_village_ES2_2": ("墨塔基亚坐落于塔奈西斯湖南岸越过蒂亚具斯河的边境地带，这座建于和平时期的帝国殖民地是丝织业所需的桑树种植中心。",
   "墨塔基亚是和平年月里插到边境的桑园。老辈人说当年选地时算准了河对岸的眼色——如今和平还在，桑就得接着种。",
   "墨塔基亚的桑苗是边境硬通货——苗与叶两头出货；河边地含军事摊派，丝价里有一成是戍边的钱。"),
 "castle_village_ES3_1": ("墨利翁位于密泽亚德中央附近的一道狭窄山谷，当地丝线既受衣着朝服的帝国贵族珍爱，也被牧民边民穿在铠甲下作为对箭矢的额外防护。",
   "墨利翁的丝有两种穿法——贵人穿在朝服里，边民穿在铠甲底下挡箭。村里人说同一匹绸，两种命。",
   "墨利翁的丝按用途分两路卖——朝服料认匹头齐整，甲内衬认厚密；边地订货自带验针法，厚薄一摸便知。"),
 "castle_village_ES3_2": ("萨戈利那坐落于密泽亚德的一道狭窄山谷底，附近山上开采的银矿解释了帝国为何在过去几个世纪耗费人力物力征服并守卫此地。",
   "萨戈利那的山谷窄得只容一队马走——帝国为了山里的银子在这儿守了几辈子。村里人说银子是甜的，也是沉的。",
   "萨戈利那的银矿有官军戍守——矿课按洞口抽成；私矿的说法常年有，真伪难辨，收银认官戳。"),
 "castle_village_ES4_1": ("拉文尼亚是珀拉斯海东北岸的一座渔村，附近的城堡守卫着通往阿塞莱领土的关口要道；这些湖泊与咸水湖构成的交通网络被称为卡勒萨。",
   "拉文尼亚的渔船进出都要跟城堡打照面。村里人说关卡守的是道，湖里的鱼守的是饭——两样都不敢得罪。",
   "拉文尼亚的渔获过卡勒萨水网分销——关卡抽税计在价内；咸水湖鱼与河鱼分卖，行家一口尝得出水的来路。"),
 "castle_village_ES4_2": ("厄忒弥萨坐落于阿塞莱人口中的「奈赫尔·凯勒斯」（苦河）、帝国人称之为卡勒萨的河流上游；河水刚从海边山上流下，尚未到达东南方的盐碱平原，附近土地小麦收成很好。",
   "厄忒弥萨的河在阿塞莱嘴里叫苦河，到了村里却喂出好麦子。村里人说苦是苦在下头，甜是甜在上头。",
   "厄忒弥萨占苦河上游甜水——盐碱化未至、麦质干净；上下游用水有旧约，扩田动水必惊动两岸。"),
 "castle_village_ES5_1": ("摩雷尼亚俯瞰塞堤斯河，位于通往吕卡里亚谷的低矮入口处，村民在河畔收集黏土，供应帝国南方的窑炉。",
   "摩雷尼亚的土是拿去烧的。村里人说别处的土养庄稼，我们这儿的土进窑炉——南边的窑等着它开饭。",
   "摩雷尼亚的黏土按层采挖——河畔的土一层压着一层，好坏分明；窑户按层订货，混层土烧器必裂，逃不过验火。"),
 "castle_village_ES5_2": ("阿特费尼亚村靠近塞堤斯河口，河水在下游分为三支形成三河河谷；在河流汇入珀拉斯海的河口附近，村民们开出了盐田。",
   "阿特费尼亚人管海水叫「收成」。盐田就铺在河口——村里人说河把地让给了海，海就拿盐来换。",
   "阿特费尼亚的盐田吃潮汐——晒盐看风看日，产量随季浮动；三河口的淡水顶托影响卤度，老盐工尝水便知收成。"),
 "castle_village_ES6_1": ("塞斯塔代姆坐落于奥尼石山——吕卡里亚谷中央出露的巨岩西侧；有人说石山得名自聚集于此饱餐死尸的秃鹫，而那些人死前还在当地的盐矿场工作。",
   "塞斯塔代姆那座山是秃鹫起的名——老话讲矿上死的人都喂了它们。村里人不爱提这名字，只说盐是好盐。",
   "塞斯塔代姆的盐矿名声带阴气——工价因此高过别处；矿盐质纯，识货的照收，只是验货不进矿洞。"),
 "castle_village_ES6_2": ("阿密孔坐落于奥兰石山与阿里斯河谷之间逶迤嶙峋的山脊上，居民是牧羊人，以制作加孜然的咸味干酪而闻名。",
   "阿密孔的干酪隔着山脊就能认出来——孜然味冲。村里人说羊吃着带矿气的草，奶里自带咸头。",
   "阿密孔的咸干酪耐运耐存——山脊路远，靠味重压秤赢远近市面；仿味的多，认村印的才敢长途收。"),
 "castle_village_ES7_1": ("约格律斯位于塔奈西斯湖平坦的西岸边，此地原属游牧部落、直到最近才开垦；帝国殖民者付出巨大努力在此种桑养蚕，获得高利润的产品。",
   "约格律斯的地皮以前是牧马的。帝国人硬是把桑树种活了——村里人说新地出丝是拼命换来的甜头。",
   "约格律斯的丝价高在试种风险——新垦地桑龄短、产量逐年爬坡；订丝按年递增价签长约，赌的是桑园成活。"),
 "castle_village_ES7_2": ("优纳利卡坐落于塔奈西斯湖西岸，这片洼地是种植亚麻的好地方。",
   "优纳利卡的洼地存得住潮气，亚麻一年比一年高。村里人说这地别的不长，专给麻铺路。",
   "优纳利卡洼地单产高但只宜麻——退水线外的地才敢下种，涝年绝收；麻价随湖位走，懂水情的贩子赚差价。"),
 "castle_village_ES8_1": ("卡诺普西斯依着密泽亚德高原上塞堤斯河的源头而建，小麦在这片阳光充足的平原上长势喜人。",
   "卡诺普西斯守着大河的头一口水。村里人说下游喝的都是我们让出来的——麦子先喝饱，才轮得到河。",
   "卡诺普西斯的高原麦日照足、面筋高——源头水权是村中公产；面粉南运下水路，源头价里含水规钱。"),
 "castle_village_ES8_2": ("波普西亚坐落于奥尼石山与俄佛堤斯湖域之间窄窄的峡谷内，山间阴凉处的树木生得高大，村民们砍伐松树并将木材卖出。",
   "波普西亚峡谷里的松树高得看不见头。村里人说树荫底下凉快得早——阴处长的才成材。",
   "波普西亚出整松大料——峡谷阴生纹理直、节疤少；大料按株议价，量径的尺是村规，外人带尺进村不算数。"),
 "castle_village_EW1_1": ("沙拉斯湾与珀拉斯内海之间的海角以加隆托为名，沿海草甸茂盛，较高的草对海水盐质耐受性不错，可用来饲养优良的马匹。",
   "加隆托的马吃的是带咸味的草。村里人说海角的风把马吹得筋骨紧——好马都是海边风里站出来的。",
   "加隆托的盐草马耐粗饲、蹄质硬——海角隔离少疫病；马驹按春草订，海雾重的年头草嫩膘好。"),
 "castle_village_EW1_2": ("珀拉斯内海汇入西海所经的狭长海峡以吕西亚为名，此地位于珀拉斯海一侧，附近宽阔的海滩用于建设盐田。",
   "吕西亚的海峡是内海出海的喉咙。村里人守着滩涂晒盐——潮水一天两趟，盐田就吃这两趟。",
   "吕西亚盐田吃海峡潮差——滩宽卤足、航路近便；盐随船走，内海船户出海口前必在此补盐。"),
 "castle_village_EW2_1": ("托里俄斯坐落于卡拉迪亚腹地的阿里斯河谷，河水灌溉与相对温暖的内陆气候确保了与相邻柏耳贡的小麦田高产，喂养了帝国中部的许多人。",
   "托里俄斯跟柏耳贡是隔壁的麦把式——两村的麦喂着帝国肚子。村里人说河是一条，麦是一家。",
   "托里俄斯与柏耳贡共用水规分水——两村麦价齐涨齐落；收麦认河谷整货，单村抬价没人接。"),
 "castle_village_EW2_2": ("柏耳贡坐落于卡拉迪亚腹地的阿里斯河谷，河水灌溉与温暖的内陆气候确保了与相邻托里俄斯的小麦田高产，喂养了帝国中部的许多人。",
   "柏耳贡的名字总跟托里俄斯连着说。村里人不服气——明明我们村的磨先转的，可麦子嘛，是一家。",
   "柏耳贡的磨坊是河谷里的老字号——面粉与麦谷两头出货；两村分水旧约刻在桥头，旱年按约不减。"),
 "castle_village_EW3_1": ("俄尼卡坐落于珀拉斯海北岸，山丘上遍布梯田葡萄园，出产的干型葡萄酒在帝国各地广受欢迎。",
   "俄尼卡的梯田挂在山坡上，一层压一层。村里人说干酒不甜，可帝国的酒桌上认这个涩劲。",
   "俄尼卡梯田酒走量也走名——北岸干型酒是招牌；年成看坡向，向阳梯田先熟先榨，混坡酒价掉档。"),
 "castle_village_EW3_2": ("塔耳库提斯坐落于珀拉斯海岸，近海岛屿冲来的沙壤与晨雾，让俯瞰沙滩的地方得以建起葡萄园。",
   "塔耳库提斯的葡萄喝着晨雾长大。村里人说沙地存不住水，雾就是老天爷每晚来还的水债。",
   "塔耳库提斯的沙壤酒果香重——雾养葡萄糖分足；产量小、岸线有限，酒贩认滩头序号订货。"),
 "castle_village_EW4_1": ("色雷刻托坐落于汇入沙拉斯湾的一片小海湾旁，受南方热风与山泉滋润，被帝国的地主们用来进行椰枣树的试验性种植。",
   "色雷刻托的枣树是老爷们的试验——南边热风加山泉，愣是在帝国地界种出了椰枣。成不成，年年有人来看。",
   "色雷刻托枣园是产业试验田——地主资金撑着，产量谈不上；买枣是买稀罕，价钱看的是来年头一茬。"),
 "castle_village_EW4_2": ("伽玛耳丹位于沙拉斯湾分出的一片小海湾的头端，金枪鱼在近海聚集产卵，当地渔民先用渔网将鱼群围住，再用钩具拖拽上船。",
   "伽玛耳丹人围金枪鱼跟围羊似的——网一圈、钩一搭，大鱼就上船。村里人说鱼汛来时海都是黑的。",
   "伽玛耳丹的金枪鱼汛短而猛——围网季全村下海，渔获即腌即走；汛期工钱翻倍，外乡雇工要旧相识作保。"),
 "castle_village_EW5_1": ("维戎位于乌卡利翁地块的丘陵地带、靠近伽律斯河的源头处，山丘上的葡萄在排水良好的沙质土壤上茁壮成长。",
   "维戎的葡萄园挨着河源头，沙土沥得住水。村里人说葡萄根怕涝不怕旱——沙地正合它的脾气。",
   "维戎的坡地酒以净爽著称——沙质排水、果病少；源头水权分毫必争，园界碑年年有人查。"),
 "castle_village_EW5_2": ("戈勒林坐落于温暖而橡树繁茂的埃皮尔山丘，靠近瓦兰迪亚与巴旦尼亚的边境交界处；养羊在该地区是一种生计，偷羊亦然。",
   "戈勒林这地方养羊，也养偷羊的——老话就这么讲。村里人夜里圈羊加双闩，谁都别笑话谁。",
   "戈勒林的羊市在三国交界的灰地——价低两成是灰地价；收羊认烙印加保人，夜路不带现货。"),
 "castle_village_EW6_1": ("赫托该亚位于拔地而起的玄武岩巨山厄里特律斯山附近，村民在山坡上种植橄榄树，所产橄榄油以清淡芬芳而闻名。",
   "赫托该亚的橄榄树栽在黑石头山上。村里人说玄武岩存了太阳的热，油才香得清清淡淡不腻人。",
   "赫托该亚的清芳橄榄油是宴席名品——石坡地产量有限；头道油按树认订，混榨充名品的官司常打。"),
 "castle_village_EW6_2": ("尼得翁坐落于深成岩构成的厄里特律斯山脚下、泽俄斯河低洼的蜿蜒处，酿酒葡萄生长在河谷的缓坡上。",
   "尼得翁的葡萄种在河湾的慢坡上。村里人说河在这儿拐得慢，土就留得厚——酿酒的活计全在这份慢里。",
   "尼得翁的河湾酒醇厚——缓坡土层深、老藤多；水患年份坡下园受淹，高低园价差是行内常识。"),
 "castle_village_EW7_1": ("俄里斯托科律斯位于俄耳堤西亚湾旁，西大洋与珀拉斯海间的航路经过此地；清晨自西而来的海雾与正午的烈日令酿酒葡萄得以生长。",
   "俄里斯托科律斯的葡萄早上一头雾水，中午一身日头。村里人说这一凉一热，糖分全憋在果子里。",
   "俄里斯托科律斯的酒借航路远销——湾边码头装船方便；昼夜温差是酒的风骨，外港仿味仿不出这份日雾。"),
 "castle_village_EW7_2": ("厄尔凡尼亚位于俄耳堤西亚湾旁、西大洋与珀拉斯海间的航路经过之地，附近山坡上生长的橄榄树享受着南方的暖阳。",
   "厄尔凡尼亚的坡地朝南，橄榄树晒足了太阳。村里人说官册上写的是葡萄园，可满坡的橄榄骗不了人。",
   "厄尔凡尼亚的橄榄出油率高——南坡暖阳、航路近便；油与酒相邻争地，园界易主时按现状断，不按册。"),
 "castle_village_EW8_1": ("革耳塞戈斯坐落于分割库耳西翁峭壁与德律亚山脉的弥戎河谷中，村民在俯瞰村庄的山嘴上开矿，提炼出银与其他贵金属。",
   "革耳塞戈斯的矿口就在村头山嘴上，抬头看得见。村里人说守矿就是守家——矿在，村就在。",
   "革耳塞戈斯出银并副产贵金属——山嘴矿口易守难攻；矿石就地粗炼，精炼货与矿砂分价，验色认火不认嘴。"),
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
        "SELECT settlementId,name,descriptionText FROM bannerlord_settlements "
        "WHERE settlementType='village' AND culture='Culture.empire' ORDER BY settlementId").fetchall()
    assert len(rows) == 97, len(rows)
    rows = rows[:49]  # 批 2a：EN*/ES*/EW1-7 + EW8_1，尾＝castle_village_EW8_1
    assert rows[0]["settlementId"] == "castle_village_EN1_1"
    assert rows[-1]["settlementId"] == "castle_village_EW8_1"

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
        assert desc_cn, sid
        desc_text = re.sub(r"\s+", "", desc_cn)
        seg = f"{cn} | {desc_text} | 出处 bannerlord_settlements#{sid} | 译名token {tok} | 描述token {dtok}"
        meta[sid] = {"cn": cn, "en": en, "desc": desc_text}
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
    with io.open(os.path.join(WS_AUTH, "sources", "source-game-villages-desc-emp1.yaml"), "w",
                 encoding="utf-8", newline="\n") as f:
        yaml.safe_dump(reg, f, allow_unicode=True, sort_keys=False)

    # ---- 3) 档（双写：authoring-out + workspace authoring） ----
    made = []
    for sid, m in sorted(meta.items()):
        assert sid in L2, sid
        desc_text = m["desc"]
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


if __name__ == "__main__":
    main()
