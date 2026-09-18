# -*- coding: utf-8 -*-
"""百科化村庄铺开批 4：瓦兰迪亚 36 村（已入档 castle_village_V6_2 德里亚特除外）→ 35 档。
数据源＝DB bannerlord_settlements（village, Culture.vlandia，描述文 token→CNs）。
独立快照与来源登记（source.calradia.game.villages-desc-vlandia），不触碰前批快照 hash。
双写 authoring-out ＋ workspace authoring。红线同前批：JSON 往返断引用 + ignore_aliases；
slug 不进文档体；六身份显式落点；grant.min_detail==layer。
"""
import io, os, json, re, hashlib, sqlite3, yaml

OUT = os.path.dirname(os.path.abspath(__file__))
WS_AUTH = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
SRC_ID = "source.calradia.game.villages-desc-vlandia"
SRC_VER = "bannerlord-1.3.15.110062"
SNAP = "game-villages-desc-vlandia.txt"
DONE_SID = "castle_village_V6_2"   # 德里亚特：已入档（B 级编年史通道）

# ---------- L2 观感文案（人工层）：sid -> (assert, rumor, detail) ----------
L2 = {
 "castle_village_V1_1": ("于桑克坐落于汇入沙拉斯湾的一处小海湾旁；这片温暖的南方土地很宜种植橄榄树。",
   "于桑克的坡朝海，冬天不咬人。村里人说橄榄认地气，暖和的海湾才养得出好油。",
   "于桑克以橄榄油为大宗——海风暖、坡地缓，果熟匀整；油按头榨分等，头榨走南部商路，尾榨留作灯油与皂料。"),
 "castle_village_V2_1": ("翁加尔位于蜿蜒的特朗河附近的温暖林地；这片盛产小麦的土地曾是帝国腹地，如今是瓦兰迪亚王国的中心地带。",
   "翁加尔的麦子喂过帝国，如今喂瓦兰迪亚。村里人说换谁当家都一样，地还是那块地，只认水和太阳。",
   "翁加尔是王畿粮村——林地间夹大田、河运直通腹地；粮货供应附近市镇与军需，收粮看干湿与杂质，丰年官仓先购。"),
 "castle_village_V2_2": ("费顿位于特朗河的一处拐角上，离通往比斯坎丘陵的山口不远；村民在温暖干燥的一侧山坡上种植橄榄。",
   "费顿的坡一半晒太阳一半背风。村里人说橄榄就爱这半张脸——太湿的坡结不出好果。",
   "费顿的橄榄靠干坡——日照足、果小油浓；村近山口商队过路多，榨季油价随山口商情起落，腌果与干果同走外路。"),
 "castle_village_V3_1": ("德拉庞位于特朗河口正南方的法尔海角角尖；村民冒险远渡重洋，追捕随北方寒流而来的鳕鱼群。",
   "德拉庞的男人一出门就是大半年。村里人说海那边有鱼群，也有寡妇——船回不回，看运气也看祖宗。",
   "德拉庞以远海鳕鱼为业——船队循寒流鱼场北上，渔期长、风浪险；鱼获就地剖晒成干，干鳕耐存价稳，行情看船数与盐价。"),
 "castle_village_V3_2": ("瓦朗比坐瞰特朗河河口；当地地主在河畔沼泽里开垦出的围场中饲养马匹。",
   "瓦朗比的马棚建在烂泥上。村里人说别人嫌沼地没用，老爷偏说那是现成的马圈——围一围就成了。",
   "瓦朗比地主牧马——沼地围场水草足、栏养省工；马只依用途分驮马与骑乘，草情与河口商路一同决定马价。"),
 "castle_village_V4_1": ("奥曼法德位于一条湍急小河旁——那河自莱恩诺山脉奔流而下，汇入奥克斯湖；山坡开成梯田种葡萄，酿出的酒醇厚浓郁。富人会加蜂蜜与丁香调饮，平民则兑水喝。",
   "奥曼法德的酒分两路。村里人说老爷那壶加蜜加丁香，咱们这壶兑水——都是同一个坛子里出来的，喝法不一样。",
   "奥曼法德的酒以醇厚见长——山泉急、坡地瘦，葡萄味重；酒分陈坛与新坛，坛装走陆路，富户订香料调酒，平民买散酒兑水。"),
 "castle_village_V5_1": ("蒂尔比位于埃博半岛附近寒冷多雾的水域旁；埃博以北的低洼地带适宜种植亚麻。",
   "蒂尔比的天总是灰的。村里人说雾多不怕，麻就爱这口凉——雾越厚，纤维抽得越长。",
   "蒂尔比的亚麻靠冷雾养——低洼地湿凉、纤维长韧；麻料按束分等，织布与缆绳各取所需，封冻前是集中出货的窗口。"),
 "castle_village_V5_2": ("西岚达克坐落于埃博半岛的高山脚下，山势略挡西北刮来的刺骨寒风；村民从山间溪流里挖出一种细腻的浅色黏土。",
   "西岚达克的泥是从溪底抠出来的。村里人说那泥里掺着亮渣子，一晒就闪——陶匠拿它做细白碗盏。",
   "西岚达克出细白泥——溪底取土、随层分质；泥性看手感与烧后成色，细泥专供器皿，粗泥做砖瓦，入冬封溪即停挖。"),
 "castle_village_V6_1": ("卡琉斯位于瓦尔切格湾以南多雾的沿海平原上；当地村民讲一种混合了瓦兰迪亚、斯特吉亚与巴旦尼亚成分的话，在低地种植亚麻。",
   "卡琉斯人说话三国都听得懂一半。村里人说祖上是杂着来的，话也杂着长——卖麻的时候倒谁都不吃亏。",
   "卡琉斯以低地亚麻为主产——雾重地湿、纤维柔长；因通三方话语，本村麻商能直接对接北境与山地买家，麻价随边境松紧浮动。"),
 "castle_village_V7_1": ("塔利维尔坐落在卡拉迪亚腹地的特朗河谷中段，村民在山坡上种植橄榄。",
   "塔利维尔在谷中间，风从两头过。村里人说腹地的橄榄不娇气，坡上随便站站就结果。",
   "塔利维尔的橄榄走量——河谷中段坡地连片、易集中收果；榨坊随收季开张，油价看年成与河运，大宗货发往腹地市镇。"),
 "castle_village_V7_2": ("罗德唐之名取自莱恩诺丘陵富含铁的红土；当地矿山为瓦兰迪亚的许多锻造作坊供应铁矿石。",
   "罗德唐的土是红的，人的手也是红的。村里人说这地方的名字就写在土里——红土挖出来，铁就在里头。",
   "罗德唐以铁矿立村——红土浅、矿口散，就地分选后驮运出山；矿石按成色与断面论价，锻造作坊常年包收，铁价牵动兵器行情。"),
 "castle_village_V8_1": ("韦雷克桑位于小而深的内陆湖奥克斯湖的水域旁；村民在湖边平坦的草原上种植小麦。",
   "韦雷克桑的田挨着湖，平得一眼望到头。村里人说湖挡风、地又平，麦子在这地方长得最省心。",
   "韦雷克桑是湖滨粮村——草原平阔、田块成方；粮货靠湖岸集运，收粮看麦粒饱满与干湿，湖运一开粮价就动。"),
 "castle_village_V8_2": ("马林位于奥克斯湖以西的缓坡上；南来的洋流让薄雾清晨格外凉爽，温暖的午后则让当地的葡萄长势很好。",
   "马林的葡萄一天过两季。村里人说早上冷得缩手，午后晒得发困——葡萄偏偏就爱这么折腾。",
   "马林的葡萄赖早晚两季的落差——雾露润、午后暖，果皮厚味足；酒按坡向分等，西坡先熟，装坛后走湖路发往内地。"),
 "village_V1_1": ("卡利奥克坐落于瓦兰迪亚中部埃皮尔山丘与比斯坎山之间的平原上，当地人在沼泽地上种植亚麻。",
   "卡利奥克的田一半是水。村里人说沼地难走，可麻就喜欢脚泡在水里——越烂的地，麻越壮。",
   "卡利奥克在沼地种麻——排水靠沟渠、熟地逐年扩；麻料按束与色分等，沤麻是村中大活，麻期一到全村下沟。"),
 "village_V1_2": ("埃蒂尔菲德位于巴旦尼亚边界的一处低矮山嘴旁，埃蒂尔河自乌卡利翁山崖的泉眼里涌出；村民种小麦，卖给驻扎附近的军队。",
   "埃蒂尔菲德的麦子直接进了军营。村里人说边上有兵，粮就不愁卖——只要不打仗，年年都有人来收。",
   "埃蒂尔菲德以军粮为大宗——崖泉灌田、产量稳；粮货按军单先行认购，边衅一起粮价先涨，余粮才轮到民市。"),
 "village_V2_1": ("马雷汶坐落于莱恩诺山脉密林覆盖的陡坡上，俯瞰着整个奥克斯湖；村民采伐高大的树木，卖到沿海城市。",
   "马雷汶的人从上往下看湖。村里人说林子归我们，船归海边的人——木料顺坡滚下去，换回来的才是活钱。",
   "马雷汶以原木为业——陡坡取材、滑道下山，水运发往沿海船坞；木料按材与龄论价，造船的直材最贵，梁柱料次之。"),
 "village_V2_2": ("奥里唐位于一条小河旁——那河发源于埃博半岛的群山，最终汇入奥克斯湖；水流到附近一处拐角便缓下来，村民得以从河岸掘取陶工所需的黏土。",
   "奥里唐的泥是水给攒下的。村里人说河一快就带泥走，一慢就把泥撂下——拐角那儿，全是好料。",
   "奥里唐供陶土——缓流河湾淤泥细净、易于成形；取土按湾段分层，货发沿湖窑口，泥质看手感与烧色，细泥价高。"),
 "village_V2_3": ("弗雷吉昂坐落于帕拉汶德以北的平原上——瓦兰迪亚的南国暖流自此让位于北境的寒气；村民在沼泽与草地里种植亚麻。",
   "弗雷吉昂是暖气的尽头。村里人说再往北就冷了，可麻不怕冷，怕的是太舒服——这儿刚合适。",
   "弗雷吉昂靠冷暖交界种麻——地湿、生长期长，纤维韧；麻料多走帕拉汶德织坊，秋后集中出货，寒年麻质反佳。"),
 "village_V3_2": ("鲁兰德位于特朗河中部的一处林谷中；村民以橡树林地的橡子作饲料养猪。",
   "鲁兰德的猪是自己找饭吃的。村里人说谷里橡子掉一地，猪拱一拱就饱了——省下的粮都是赚的。",
   "鲁兰德出橡子猪——林谷放养、省料肉紧；腌肉与火腿风味浓，出货多在秋冬，收猪看橡子年成与膘厚。"),
 "village_V3_3": ("拉尔纳克坐落于特朗河谷下游的平缓山丘上；村民种植黑麦、大麦与小麦，出售给帕拉汶德附近缺粮的城镇居民。",
   "拉尔纳克的粮进了城。村里人说城里人嘴多，地里的东西不愁没去处——就是价钱得看城里人的脸色。",
   "拉尔纳克是城郊粮村——三麦兼种、地力错开；粮货直供帕拉汶德，磨坊与酒坊是常客，城价一动本村先觉。"),
 "village_V3_4": ("特朗河以北草绿色的丘陵被一道峻峭峡谷分割，帕利松就坐落于峡谷口；这一带地薄，但上游吹来的凉海风与温暖的午后，正宜于酿酒用葡萄。",
   "帕利松的葡萄长在瘦地上。村里人说地肥的地方长粮，地瘦的地方长味儿——酒好不好，先看土薄不薄。",
   "帕利松以酿酒葡萄见长——峡谷风道、昼夜温差大，果味集中；酒分坡块封坛，谷口园子先熟，货走陆路发往内陆酒商。"),
 "village_V5_1": ("菲尔贝克坐落于一块巨岩上，俯瞰瓦兰迪亚中部嶙峋的比斯坎海岸；当地终年温暖，比斯坎山坡上的葡萄长势喜人。",
   "菲尔贝克的人站在石头顶上看海。村里人说海风到这儿就软了，葡萄晒得舒服——这地界，光比水多。",
   "菲尔贝克凭暖坡种葡萄——临海日照长、病害少；酒质偏甜，装坛后沿海岸商路外销，风调的年头酒价压秤。"),
 "village_V5_2": ("梅罗克是一座小渔村，俯瞰比斯坎海岸上的小海湾；村民向西出海捕近岸的鲻鱼与鲈鱼，并在南边岛屿附近寻获金枪鱼与沙丁鱼。",
   "梅罗克的人看风下网。村里人说西边捞小的，南边追大的——船小不敢走远，能捞多少是多少。",
   "梅罗克渔获分两路——近岸鲻鱼鲈鱼走鲜市，南方岛屿金枪鱼沙丁鱼量大价低；鲜货离水即贱，多盐渍风干外运。"),
 "village_V5_3": ("诺格伦坐落于瓦兰迪亚中部比斯坎丘陵的一处斜坡上；村民在沿海树林与高山峭壁之间的牧场上放羊。",
   "诺格伦的羊在树和石头中间找草。村里人说坡上的草东一块西一块，羊得会走路才吃得饱。",
   "诺格伦以绵羊为业——林峭之间草场零碎、放牧范围大；毛肉双收，剪毛按季集中，毛质看细度与净度，织坊常年包收。"),
 "village_V6_1": ("阿罗曼克坐落于穿过比斯坎山脉与埃皮尔山脉的纳尔谷中；这片瓦兰迪亚南部的温暖土地很宜种植橄榄树。",
   "阿罗曼克在两道山梁中间，风到这儿就停。村里人说谷里比外面暖一截，橄榄树冬天冻不着。",
   "阿罗曼克靠谷地种橄榄——两面山挡风、霜期短；果熟早，榨季比北边村子先开，油货随山谷商路走南部市镇。"),
 "village_V6_2": ("莫特同样坐落于穿过比斯坎山脉与埃皮尔山脉的纳尔谷；这片瓦兰迪亚南部的温暖土地很宜种植橄榄树。",
   "莫特跟谷里邻村一样种橄榄，可莫特人爱往高处栽。村里人说坡上比谷底干，果子小，出的油却香。",
   "莫特的橄榄种在谷中偏高的坡段——土薄果小、油味浓；本村少有榨坊，多把果子卖到谷底大坊换油，按果计价。"),
 "village_V6_3": ("阿洛斯唐坐落于比斯坎山脚下一块可以俯瞰海洋的巨岩上；这片瓦兰迪亚南部的温暖土地很宜种植橄榄树。",
   "阿洛斯唐的橄榄树长在石头缝里。村里人说地是硬的，风是咸的，可果子偏偏长得油亮。",
   "阿洛斯唐的橄榄地临海背崖——树龄老、产量低；因近海口，腌果比榨油更值，收货看果形与盐渍成色。"),
 "village_V6_4": ("绍尔纳坐瞰着穿过比斯坎山的通路；当地村民与瓦兰迪亚中部的许多居民一样，专事养猪。",
   "绍尔纳守着山口。村里人说猪爱吃路上掉的橡子，商队一走，猪就跟着捡——山路养人，也养膘。",
   "绍尔纳以养猪为业——山口林坡放养、商路余粮省料；腌肉熏腿耐存价高，收猪看膘情与奔走，冬前是集中出货季。"),
 "village_V7_1": ("萨万特坐落于沙拉斯湾以北温暖的草原上；这片土地是瓦兰迪亚公认的优质马场。",
   "萨万特的马是草原上跑大的。村里人说别处的马圈着养，我们的马是放开的——跑出来的腿，骗不了人。",
   "萨万特出良马——海湾暖流润草、草场开阔；马按齿口体态分等，军需与贵族争购，暖冬草足的年头马价更高。"),
 "village_V7_2": ("韦桑位于沙拉斯湾的一处小溪旁——溪水自埃皮尔山丘流下；这片温暖的土地很宜橄榄树生长。",
   "韦桑的溪是从山丘上淌下来的。村里人说有溪就有菜园，坡上再栽几行橄榄——过日子够了。",
   "韦桑的橄榄靠溪灌——田块沿溪铺开、家户零散；产量不大但油质清，多在本村与邻镇市集散卖，罕有远销。"),
 "village_V8_1": ("传说战士奥尔萨即在奥尔斯热登陆，并把长矛插在海滩上；如今该地以马闻名，马群在点缀着报春花与水仙的岩石海岸斜坡上吃草。",
   "奥尔斯热的马圈立在一根老矛的位置上。村里人说祖宗插过矛的地方，如今养着马——打打杀杀的事，交给马也不错。",
   "奥尔斯热以海崖马场闻名——岩坡短草、海风结实；马群耐湿少病，因传说招徕买家，好马多被贵族点名要走。"),
 "village_V8_2": ("卡南克位于莱恩诺北部潮湿的林丘中；该村以饲养优质猪闻名，那些猪能在密林里穿梭，寻食橡子、栗子与蛴螬。",
   "卡南克的猪比人认路。村里人说林子密得人钻不进，猪倒熟得跟自家院子似的——拱回来的膘，比喂出来的香。",
   "卡南克以林猪著称——湿林橡栗丰足、放养省料；肉味浓、腿货耐存，收猪看齿龄与膘层，林深处的猪更被认。"),
 "village_V8_3": ("勒芒塔尔位于埃伯半岛的群山之中，陡坡上长满直耸的云杉与冷杉；村民用陷阱捕猎貂、兔、狐狸等毛皮兽。这片荒僻之地近来吸引了许多新定居者，他们为林中的自在而来，瓦兰迪亚贵族在此地的管束也比南方宽松不少。",
   "勒芒塔尔的林子谁也管不严。村里人说这儿税轻话也少，来的人图的就是没人盯着——下套子的手艺，比种地传得远。",
   "勒芒塔尔以毛皮为业——高林密、兽道多，设陷阱取皮；貂皮与狐皮按毛底与色差分等，村僻地远，皮货多由行商上门收。"),
 "village_V9_1": ("阿兰塔斯坐落于多山的伊博半岛中央；这里土薄天寒，但山里的铁矿养活了此地。",
   "阿兰塔斯的麦长不好，铁倒是管够。村里人说地不给饭，山给——挖出来的黑石头，比麦子值钱。",
   "阿兰塔斯以铁矿立村——山场矿口分散、靠人背畜驮；矿石按成色论价，就近卖给锻造作坊，铁货多走山路出半岛。"),
 "village_V9_2": ("阿利斯维斯特原是一片荒沼，瓦兰迪亚定居者将水排干，在山谷里种起了小麦与黑麦。",
   "阿利斯维斯特的地是人从水里抢来的。村里人说祖辈一锹一锹把水赶走，才有了这几片田——所以这儿的人惜地。",
   "阿利斯维斯特是垦沼新村——排水沟渠成网、熟地逐年扩；麦产偏晚但地力足，粮货随山谷道路外销，沟渠岁修是村中大役。"),
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
        "WHERE settlementType='village' AND culture='Culture.vlandia' ORDER BY settlementId").fetchall()
    assert len(rows) == 36, len(rows)
    rows = [r for r in rows if r["settlementId"] != DONE_SID]
    assert len(rows) == 35, len(rows)

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

    reg = {
        "source_id": SRC_ID, "source_version": SRC_VER, "source_nature": "game_snapshot",
        "universe": "awake_current", "era": "current", "locator_root": SNAP,
        "source_content_hash": file_hash, "content_tier": "base",
        "license_status": "permitted", "use_status": "active", "valid_until": None,
        "imported_at": "2026-09-14T00:00:00Z", "normalization_version": "utf8-lf-no-bom-v1",
    }
    with io.open(os.path.join(WS_AUTH, "sources", "source-game-villages-desc-vlandia.yaml"), "w",
                 encoding="utf-8", newline="\n") as f:
        yaml.safe_dump(reg, f, allow_unicode=True, sort_keys=False)

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
        assert " " not in slug and slug, sid
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
        assert not os.path.exists(os.path.join(OUT, fn)), "目标档已存在: " + fn
        io.open(os.path.join(OUT, fn), "w", encoding="utf-8", newline="\n").write(text)
        io.open(os.path.join(WS_AUTH, fn), "w", encoding="utf-8", newline="\n").write(text)
        made.append(fn)
    print("generated:", len(made), "vlandia villages; snapshot hash:", file_hash[:16])


if __name__ == "__main__":
    main()
