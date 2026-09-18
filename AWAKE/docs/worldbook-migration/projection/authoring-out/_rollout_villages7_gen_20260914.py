# -*- coding: utf-8 -*-
"""百科化村庄铺开批 7：斯特吉亚 32 村 → 32 档。
独立来源登记 source.calradia.game.villages-desc-sturgia；红线同前批。
斯特吉亚＝波耶贵族制：用 波耶/领主、领主会议、政令/律令/规矩；禁朝廷/敕令一类帝制词。
"""
import io, os, json, re, hashlib, sqlite3, yaml

OUT = os.path.dirname(os.path.abspath(__file__))
WS_AUTH = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
SRC_ID = "source.calradia.game.villages-desc-sturgia"
SRC_VER = "bannerlord-1.3.15.110062"
SNAP = "game-villages-desc-sturgia.txt"

L2 = {
 "castle_village_S1_1": ("乌斯托科坐落于瓦尔切格湾与比亚里海之间当风的卡恰尔半岛上；当地人养的耐寒北方牛在石缝间找小草吃，艰难时则添些海藻。",
   "乌斯托科的人看牛也看风。村里人说这地方的草不够厚，牛得自己想办法——海边的东西，能吃的都算收成。",
   "乌斯托科以耐寒牛群为业——风大草短、牛只耐役；牛按膘情与耐力分等，海藻当荒年添料，肉奶自用、余牛与皮张走湾内船运。"),
 "castle_village_S1_2": ("哲米扬位于卡恰尔半岛北岸；这里草木葱茏，是当地地主养马的好地方。",
   "哲米扬的马有福。村里人说北岸雨多，草长得比别处软——马吃得顺口，人就省心。",
   "哲米扬以海滨牧马为业——临岸草茂、马体舒展；马按齿口与脚力分等，地主牧多、农户牧少，好马先应领主征用。"),
 "castle_village_S2_1": ("马扎丹靠近伽尔喀斯河的源头，在拉科尼斯湖的正西边；这片湿冷之地的村民，在泥泞的山谷里种植亚麻。",
   "马扎丹的人不怕湿。村里人说这地方干不了，麻却要湿——地跟活儿正好对上。",
   "马扎丹以湿谷种麻为业——水源近、土湿宜麻；麻按纤维与色泽分等，沤麻靠谷水，供织坊纺线结网，秋后集中收麻。"),
 "castle_village_S2_2": ("福林坐落于伊卡拉荒原边上、加尔察河附近的黑麦和大麦田旁；这里的地主把林地改成了牧马的草场。",
   "福林的人眼看着林子变小。村里人说地主的马一年比一年多，树就一年比一年少——粮够吃，柴却得往外买。",
   "福林以粮田与马场并营——河畔宜麦、林地改牧；麦按干湿分等、马按齿口分等，军马应征在先，余粮余马走河路外销。"),
 "castle_village_S3_1": ("涅夫扬斯克坐落于俯瞰瓦尔切格湾的一处平坦山角上；当地气候寒冷，但从山上淌下来的水带来了肥土，村民便种黑麦、大麦一类耐寒的作物。",
   "涅夫扬斯克的人认命也认地。村里人说冷是天定的，肥是水带来的——种得对路，就不怕天冷。",
   "涅夫扬斯克以耐寒粮作为业——山角临湾、土肥墒足；黑麦与大麦分仓，粮走湾内水运，霜早则抢收、粮价随年成起落。"),
 "castle_village_S3_2": ("德宁位于俯瞰瓦尔切格湾狭窄处的牧草地上；马儿在高地上吃草，偶尔瞧一眼下面经过的长船和小渔船。",
   "德宁的人见惯船。村里人说湾里来来往往，跟我们没多大利害——倒是马往哪儿卖，才要琢磨。",
   "德宁以高地牧马为业——临湾草茂、马善走；马按齿口与脚力分等，借湾内水道外销，长船过境时买卖与消息都灵。"),
 "castle_village_S4_1": ("克拉尼罗格位于俯瞰加尔察河的开阔林谷内；此地的村民是捕猎的好手，猎取貂、狐狸和兔子的毛皮，供应南方的大量需求。",
   "克拉尼罗格的人靠林子吃饭。村里人说南边的贵人穿什么我们不管——反正皮得从这儿出。",
   "克拉尼罗格以林猎毛皮为业——谷林兽多、猎户成群；皮张按毛色与完整分等，貂皮最贵，冬季毛厚时南运、皮商上门坐庄。"),
 "castle_village_S4_2": ("伊斯米尔科格坐落于弥戎河的两条小支流——加尔察河与瓦斯特拉河——之间长满草的低地上；当地人大多既养牛又偷牛，还常去劫掠东边的帝国边境村落和西边的巴旦尼亚人。",
   "伊斯米尔科格的人分不清哪头牛是自己的。村里人说草谷养牛，也养胆——牛被牵走，我们就去牵回来。",
   "伊斯米尔科格以河边养牛为业——草低水足、牛群成群；牛按膘情与角记分等，劫掠所得与自养混栏，边地动荡时牛价乱、销路多半见不得光。"),
 "castle_village_S5_1": ("奥夫是伊卡拉荒原——拉科尼斯湖西北方的森林——里一处较新的斯特吉亚垦殖地；村民起初靠打猎取皮为生，但猎物大多被赶到了森林深处，如今他们改在沼泽林地里种亚麻。",
   "奥夫的人换过一次生计。村里人说林子里的兽走光了，就改种麻——地还是那块地，活儿换了。",
   "奥夫由猎转麻——原以毛皮立村，兽少后改种亚麻；麻按纤维与色泽分等，沤麻靠沼水，新垦地薄，收成尚不及老麻乡。"),
 "castle_village_S5_2": ("费赫位于伊卡拉荒原——拉科尼斯湖西北方森林——的北部边缘处；它是冒险深入森林的商贩落脚之处，那些人要面对严寒与狼群，还要应付寻毛皮的、脾性难料的林中居民，以及北地的赏金猎人。",
   "费赫的人见过太多来路不明的客。村里人说城里人想进林子发财，先进我们村——能不能出来，就难说了。",
   "费赫是林边商栈——据荒原入口、管进山补给；货按来路与成色分等，毛皮、腌肉与盐是常货，赊账多、赖账也多，冬季最险。"),
 "castle_village_S6_1": ("塔科尔是一座养牛的村子，坐落在巴旦尼亚人称作“米纳德·里韦尔”也就是“战争之山”的山口处——他们的帝国与斯特吉亚邻居则管它叫“弥那多耳”；这是连通荒凉北方与较安稳的帝国疆土的主要通道之一，不论对买卖还是对劫掠都一样。",
   "塔科尔的人守着山口过日子。村里人说谁想从北边下来、从南边上去，都得打这儿过——牛养得好不好，倒在其次。",
   "塔科尔以山口养牛兼收路税为业——据通北要道；牛按膘情与角记分等，商队过口抽成，兵事一起，牛与道路都先紧着守军。"),
 "castle_village_S6_2": ("德沃鲁斯塔是一座渔村，俯瞰着连接瓦尔切格湾和拉科尼斯湖的激流；村民在天亮时沿悬崖一路下去，捕捉跃过岩石的鲑鱼，以及海里的鲱鱼和鳕鱼。",
   "德沃鲁斯塔的人算着潮水下崖。村里人说鱼认路，人也得认路——脚下一滑，什么都没了。",
   "德沃鲁斯塔以激流捕鱼为业——临崖下钩、就水收网；鲑鱼最俏，鲱鳕多腌藏，鲜鱼走湾内快到集，汛期一过即修网补崖道。"),
 "castle_village_S7_1": ("乌里克斯卡拉位于伊勒坦运输线上——那是一张由冰川湖连成的网络，往来的长船可以在拉科尼斯湖与塔奈西斯湖之间拖过去、划过去；村民在浅水里捕带刺的鲈鱼和螯虾，风干后卖给过路的人。",
   "乌里克斯卡拉的人靠水路的客人过日子。村里人说船上的人总要吃东西——我们把鱼晒干，等他们来。",
   "乌里克斯卡拉以湖道渔获兼供应为业——临拖船线、客旅不断；鲈鱼与螯虾风干成货，按干湿与成色分等，湖面封冻时转卖存粮与腌货。"),
 "castle_village_S7_2": ("阿洛夫位于名叫伊勒坦运输线的冰湖网络的边缘处；村民在俯瞰湖泊的高地上种黑麦和大麦，肥沃的黑土补偿了当地漫长的冬季和短暂的生长期。",
   "阿洛夫的人跟天抢日子。村里人说这地方能种的就那几个月——土好，才顶得住天短。",
   "阿洛夫以高地粮作为业——黑土厚、生长期短；黑麦为主、大麦次之，粮走湖道外销，雪早则抢收、粮价随年成涨落。"),
 "castle_village_S8_1": ("弗拉基夫坐落于伊勒坦山脚下寒冷阴暗的林谷中；这一带的耐寒牛在冬天会刨开厚厚的积雪找草吃，就这样熬到春天。",
   "弗拉基夫的人只留刨得动雪的牛。村里人说养不活自己的牛，我们也养不活——能熬过冬天的才配吃粮。",
   "弗拉基夫以耐寒牛群为业——雪谷放养、牛性耐馁；牛按耐寒与役力分等，林谷伐木所得与牛一起运出山，冬长则出栏推迟。"),
 "castle_village_S8_2": ("格拉夫斯特伦坐落于东卡拉迪亚的峻峭险峰——伊勒坦山——脚下，这座山得名自草原民族的一位神灵；这片荒野上有许多毛皮兽，如貂、狐狸、兔子等。",
   "格拉夫斯特伦的人受山神照看。村里人说山有名有姓，兽也就多——猎户来这儿，图的是毛皮，也图个心安。",
   "格拉夫斯特伦以荒野猎皮为业——山脚兽多、猎期长；皮张按毛色与完整分等，貂狐为上，山名带神，猎户入山先祭、忌滥杀幼兽。"),
 "village_S1_1": ("罗多巴斯——“奥巴斯之屋”——得名自山上一位亡故已久的斯特吉亚酋长；村子坐落在俯瞰伊卡拉荒原的山脊上，去山下黑暗森林里猎取毛皮的猎人把这里当作落脚处。",
   "罗多巴斯的人先认屋再认人。村里人说老酋长的名头还在——猎户进山前，都愿意在村里住一晚。",
   "罗多巴斯以猎户据点兼收皮为业——据荒原脊线、进山要冲；皮张按来路与成色分等，猎户在此补给，村中以皮易粮盐，冬猎最盛。"),
 "village_S1_3": ("卡格雷夫从前叫卡尔·格莱夫——巴旦尼亚酋长格莱夫的环形堡垒，两代人之前被斯特吉亚人夺了去；它位于瓦尔切格湾沿岸，湾里海水较平，不少鱼会成群游到湾里产子，当地渔民很会借这份便利。",
   "卡格雷夫的人记得旧名。村里人说墙是别人修的，鱼是水里来的——名字改了，鱼汛没改。",
   "卡格雷夫以湾内渔获为业——临静湾、鱼汛密；鱼按大小与鲜度分等，鲜鱼走湾内市集、余货腌藏，旧日堡垒的墙石成了渔家的地基。"),
 "village_S2_1": ("萨夫纳坐落于俯瞰激流的高耸山脊上——那激流把拉科尼斯湖与狭窄的马佐波尔湾连在一处；村民在靠近海崖的内陆种小麦，就为那里的地能躲开带盐的风。",
   "萨夫纳的人不往海边下地。村里人说海风咸，麦苗受不了——往里挪几步，收成就两样。",
   "萨夫纳以内陆麦作为业——背海避咸、土宜麦；麦按饱满与干湿分等，粮走脊上小路外运，海风大则麦矮，隔崖的地才保收。"),
 "village_S2_2": ("马拉布罗特坐落于马佐波尔湾与比亚里海之间高耸的多岩山脊上；这片苦寒之地要不是山上有丰富的铁矿，怕是没人会来定居。",
   "马拉布罗特的人宁肯住石头也不挪窝。村里人说这地方风硬土薄，可石头里有铁——铁能换粮，就够了。",
   "马拉布罗特以山脊铁矿为业——岩脊露矿、采掘不易；铁料就地粗炼、按成色分等，矿石靠人力背下山换粮，风季停工、矿价随兵事起落。"),
 "village_S3_1": ("乔诺巴斯——“黑树林”——坐落在一片由森林开辟出来的土地上，俯瞰着弥戎河倾入拉科尼斯湖的大瀑布；村民在瀑布旁水雾弥漫的田里种黑麦和大麦。",
   "乔诺巴斯的人说土是砍出来的。村里人说林子让地，瀑布给水——田里雾气重，麦子反倒长得欢。",
   "乔诺巴斯以辟林粮作为业——雾田润、地力新；黑麦与大麦分仓，粮走湖道外销，瀑布水声大、近田易涝，须起垄排水。"),
 "village_S3_2": ("斯科林坐落于把佳托姆湾与拉科尼斯湖分开的一个海角上；村民在狭窄的海湾水里捕淡水比目鱼，还在主湖里捉梭鱼一类更大的鱼。",
   "斯科林的人会看两片水。村里人说湾里水浅、湖里水深——不同的鱼，就得用不同的网。",
   "斯科林以海角渔获为业——湾湖相交、鱼种两样；比目鱼走鲜货、梭鱼宜腌藏，按大小鲜度分等，风季封湖时以腌鱼与补网度日。"),
 "village_S4_1": ("博乔瓦戈卡坐落于拉科尼斯湖陡峭北岸的一处小高处；这座村子以捕鱼为生，从湖里捕鲈鱼、鲷鱼、鲑鱼，偶尔也捕大鲟鱼和梭鱼。",
   "博乔瓦戈卡的人怕风不怕水。村里人说湖大，什么鱼都有——就是北岸的浪，得看准了再出船。",
   "博乔瓦戈卡以湖渔为业——北岸水深、鱼种全；鲜鱼走湖岸市集，鲟鱼子腌成的货最贵，按鱼种与鲜度分等，浪大即歇船。"),
 "village_S4_3": ("奥姆卡尼坐落于拉科尼斯湖畔与积雪的车尔特格山脉之间；这一带的村民养猪，用盐和蒜腌好猪肥肉，卖到北地各处去。",
   "奥姆卡尼的人靠腌肉过冬。村里人说湖边养猪省粮，盐和蒜一拌——往北一送，比活猪还值钱。",
   "奥姆卡尼以腌猪为业——湖牧养猪、盐蒜入味；肥膘按厚薄与咸淡分等，腌货耐运走北地，冬前集中宰腌、开春外销。"),
 "village_S4_4": ("扬古图姆位于拉科尼斯湖北岸、车尔特格山脉的一处角落里；林务员在山腰上砍伐橡树、榆树和其他硬木。",
   "扬古图姆的人认硬木。村里人说这一带树长得慢，可木头结实——做梁做轴，都不用挑。",
   "扬古图姆以山腰硬木为业——林密材坚、成材慢；硬木按树种与材分等，橡榆供梁轴与器具，伐后留苗，雪季封山则停工。"),
 "village_S5_1": ("维西布罗特是伊勒坦山缓坡上的一座养羊村，正好在人称伊勒坦运输线的冰湖网络下方；这一带虽然寒冷偏僻，但因它处在拉科尼斯湖与塔奈西斯湖之间、帝国与草原之间，是重要的买卖节点，人口反而比较稠密。",
   "维西布罗特的人见惯过路人。村里人说地方偏，路却不偏——东西两头的人，都得打这儿换脚力。",
   "维西布罗特以牧羊兼驮运为业——据湖线要冲、客货常过；羊毛与羊肉按季出货，兼营向导与驮兽，往来越密、村中越富。"),
 "village_S5_2": ("布基茨坐落于库赛特边境上伊勒坦山的东坡；这里的村民养一种高原牛，这品种扛得住从山上刮下来的刺骨寒风。",
   "布基茨的人在边境上放牛。村里人说两边的旗子换来换去，牛认的是坡——谁的税轻，就卖给谁。",
   "布基茨以边境牧牛为业——坡高风硬、牛性耐寒；牛按膘情与役力分等，边市双边买卖、税随属地而变，边情紧则赶牛内迁。"),
 "village_S6_1": ("克沃尔的村民在车尔特格山脚下放养一些好养活的小牛；这些人里，有不少是卡拉迪亚各地难民和失意人的后代，他们就在这寒气凛冽的北方森林中过着避世的隐居日子。",
   "克沃尔的人不问来路。村里人说各家都有各家的事，躲到这儿就图个清静——牛小点没事，够过日子就行。",
   "克沃尔以隐居牧牛为业——山麓草薄、牛小易养；牛按膘情分等，村人多是外来落户的，买卖不张扬、以物易物为常，避税避役者多。"),
 "village_S6_2": ("拉达克梅德坐落于车尔特格山脉中的一片盆地；当地崎岖的荒野盛产狐狸、貂和兔子一类的毛皮动物。",
   "拉达克梅德的人专挑冬天进山。村里人说夏天毛不厚，不值钱——冻一冻，皮才好卖。",
   "拉达克梅德以盆地猎皮为业——荒野兽多、冬皮为上；皮张按毛色与完整分等，狐貂价高、兔皮做衬里，猎获多由行商过山来收。"),
 "village_S6_3": ("阿莱巴特坐落于斯特吉亚东北部的森林深处；这片偏远之地长久以来都是北方各地躲避压迫、债务与世仇之人的藏身处，当地口音受森林民族瓦尼人的影响很大。",
   "阿莱巴特的人说话跟别处不太一样。村里人说祖上从各处逃来，口音就杂了——可谁也不问谁从前的事。",
   "阿莱巴特以林中定居为业——地远人杂、多种生计；猎、采与薄田并作，货以毛皮山货换盐铁，因避役者多，官差与税吏少至，口音自成一格。"),
 "village_S7_1": ("科尔夏斯位于瓦尔切格湾最窄处的旁边；村民出海向西，捕捞鳕鱼、鲑鱼和其他冒险游进湾里的冷水鱼。",
   "科尔夏斯的人看天出海。村里人说湾口一窄，风就急——鱼是好鱼，可命比鱼要紧。",
   "科尔夏斯以湾口海渔为业——临窄口、通外海；鳕鱼多腌藏、鲑鱼走鲜货，按鱼种与鲜度分等，风急浪高即停船，冬汛最险也最丰。"),
 "village_S7_2": ("卡布尔坐落于卡恰尔半岛的高原上，多少得到北边高耸山脊的一些庇护；当地村民种大麦和谷物。",
   "卡布尔的人念北边那道山的援。村里人说风不小，可比坡下轻——多种点大麦，日子就稳。",
   "卡布尔以高原粮作为业——背山避风、土宜大麦；大麦与杂谷分仓，粮走半岛山路外销，山脊挡风一弱，收成就跟着差。"),
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
        "WHERE settlementType='village' AND culture='Culture.sturgia' ORDER BY settlementId").fetchall()
    assert len(rows) == 32, len(rows)

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
    with io.open(os.path.join(WS_AUTH, "sources", "source-game-villages-desc-sturgia.yaml"), "w",
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
    print("generated:", len(made), "sturgia villages; snapshot hash:", file_hash[:16])


if __name__ == "__main__":
    main()
