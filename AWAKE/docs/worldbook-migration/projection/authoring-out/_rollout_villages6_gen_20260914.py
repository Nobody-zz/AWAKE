# -*- coding: utf-8 -*-
"""百科化村庄铺开批 6：巴旦尼亚 33 村 → 33 档。
独立来源登记 source.calradia.game.villages-desc-battania；红线同前批。
巴旦尼亚＝部落酋邦：用 部众/族人/氏族、议事场/氏族长老、族长、政令/律令；禁城邦宫廷词。
"""
import io, os, json, re, hashlib, sqlite3, yaml

OUT = os.path.dirname(os.path.abspath(__file__))
WS_AUTH = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
SRC_ID = "source.calradia.game.villages-desc-battania"
SRC_VER = "bannerlord-1.3.15.110062"
SNAP = "game-villages-desc-battania.txt"

L2 = {
 "castle_village_B1_1": ("阿布·科梅尔坐落于穿过乌卡利翁高原的特朗河上游的陡峭处；当地各部落在山坡上种葡萄，酿出受帝国与瓦兰迪亚贵族喜爱的白葡萄酒。",
   "阿布·科梅尔的坡地是留给酒的。村里人说别处种粮，这里种葡萄——一瓶好酒换得回一车麦。",
   "阿布·科梅尔以河坡葡萄为业——坡向朝阳、排水好；白葡萄按粒与甜分等，酒顺特朗河外运，贵族订单是常年大宗，霜期一近就得抢收。"),
 "castle_village_B1_2": ("因韦斯位于特朗河与埃蒂尔河从乌卡利翁地块翻滚而出、汇入瓦兰迪亚中央平原之处；周围的丘陵宜种小麦。",
   "因韦斯的人不愁水。村里人说两河先在门口碰头，才往外走——肥土是水带来的。",
   "因韦斯以丘陵麦作为业——两河冲积、土厚墒足；麦按饱满与干湿分等，粮走河谷商路，河水一涨即防淹，丰年仓满。"),
 "castle_village_B2_1": ("拉诺克·亨位于乌卡利翁地块的高山山脊处；村民善养猪，下山的路一条向西通瓦兰迪亚，一条向东通帝国。",
   "拉诺克·亨的人站在两家的门槛上。村里人说谁来收猪都得先把路走对——路对了，价才公道。",
   "拉诺克·亨以山脊养猪为业——林下放养、橡实育膘；猪按膘情与岁口分等，西卖瓦兰迪亚、东卖帝国，贩子抢路则价随之一变。"),
 "castle_village_B2_2": ("坎特雷克坐落于帝国领土通往乌卡利翁高原处多岩的巴拉索格峡谷之中；土壤并不肥沃，但高处的灌木林地宜养猪。",
   "坎特雷克的人不跟石头较劲。村里人说地薄就薄，猪吃的是坡上的林子——林子够厚就行。",
   "坎特雷克以峡谷养猪为业——坡林放牧、石多地少；猪按膘情分等，赶出峡谷卖给两边市镇，路窄则好价要走早集。"),
 "castle_village_B3_1": ("德鲁伊莫尔坐落于摩康谷——一道把乌卡利翁高原与埃博半岛分开的裂谷——之中；矿工在山边裂缝处发现了银矿。",
   "德鲁伊莫尔的石头里藏着银。村里人说这道谷两边的山不肯合成一块，就是留了缝给矿工。",
   "德鲁伊莫尔以裂谷银矿为业——缝间矿脉、坑道随石走；银料就地粗炼，由氏族派工头验看成色，产出大半上缴，私卖者罚没。"),
 "castle_village_B3_2": ("托·梅利纳坐落于乌卡利翁地块西北部阴暗林丘内的一块沼泽盆地中；村民从潮湿的泥土里取出黏土，卖给附近城镇的陶工。",
   "托·梅利纳的泥是湿出来的。村里人说林子遮着太阳，盆底就存着好泥——陶工等着我们的泥下窑。",
   "托·梅利纳以沼泽陶土为业——林荫湿地、土细且黏；泥按细粗分等，供附近窑口烧器，秋后取泥、春耕前封坑。"),
 "castle_village_B4_1": ("潘德拉克坐落在乌卡利翁北部、面向瓦尔切格湾的陡坡上；此地风大路险，瓦兰迪亚、巴旦尼亚与斯特吉亚的劫掠队都来过，养猪的村民一听号角，就把畜群赶进山里藏起来。",
   "潘德拉克的人睡觉都竖着耳朵。村里人说号角一响，先赶猪再拿矛——猪没了，明年就没指望。",
   "潘德拉克以山坡养猪为业——林坡放养、橡实育膘；因处三国交界，猪群随警号入山躲避，猪按膘情分等，劫后余下的猪价反而更高。"),
 "castle_village_B4_2": ("林杜恩坐落于“林·泰瓦尔”——即暗湖——的西北面绿色山丘上；周围的农田里种着小麦和大麦。",
   "林杜恩的人守着湖却不看湖。村里人说水在那边，地在脚下——把坡上的麦收好，比盯着湖实在。",
   "林杜恩以坡地麦作为业——近湖多雨、土色深；小麦与大麦分仓，粮货顺山路外运，雨多则麦好、久涝则减收。"),
 "castle_village_B5_1": ("雷姆托伊尔位于乌卡利翁高原东部悬崖的裂隙下，靠近巴旦尼亚人称作“米尔”的弥戎河的源头；这里是巴旦尼亚土壤最肥的地方，小麦收成很好。",
   "雷姆托伊尔的人说自己的地是别人挑剩下的。村里人说悬崖挡风、源头给水——好地就这么攒出来了。",
   "雷姆托伊尔以肥地麦作为业——崖下沃土、水源近便；麦按饱满分等，是这一带出粮最稳的村，丰年余粮走山路外销。"),
 "castle_village_B5_2": ("克莱格·班坐落于乌卡利翁地块的一堵悬崖下；村民从旁边的米尔河里收集黏土，卖给巴旦尼亚各城镇的陶工。",
   "克莱格·班的人顺着河捡泥。村里人说水帮了忙——泥不用挖，它自己漂到崖根下等着。",
   "克莱格·班以河泥为业——崖根淤积、随水而聚；泥按细粗分等，供各城窑口烧器，上游暴雨则泥浊难用，须等水清再取。"),
 "castle_village_B6_1": ("弗林托格坐落于乌卡利翁地块东部的高山山顶处；当地土壤贫瘠，树木却生得粗壮，是巴旦尼亚的木材主要产地。",
   "弗林托格的人跟石头争树。村里人说这块土养不活麦，倒养得活树——树比人认命。",
   "弗林托格以山顶林材为业——土薄木壮、成材慢；木材按树种与材分等，供梁柱与船料，伐后须留苗，老树不许滥砍。"),
 "castle_village_B6_2": ("格林托尔俯瞰着湍急的费尔河——泰瓦尔湖的水由此汇入瓦尔切格湾；当地草甸养出的马匹，很受北方斯特吉亚强盗与商贩的青睐。",
   "格林托尔的马出了名。村里人说北边的人来一趟不容易，可为了马，强盗和贩子都肯走这一趟。",
   "格林托尔以河畔牧马为业——草甸水足、马腿壮；马按齿口与体态分等，北上的买家好坏都收，因是水口要道，买马也常顺带探路。"),
 "castle_village_B7_1": ("阿斯特坐落于乌卡利翁东北部山丘与格兰尼斯山之间的浅谷中；当地人比起打仗更重贸易，氏族长老鼓励大家种葡萄，酿出特别醇厚的葡萄酒。",
   "阿斯特的人先算账再拿矛。村里人说老族长早说了——酒卖得动，比多杀几个人管用。",
   "阿斯特以谷地酿酒为业——浅谷聚温、坡地宜葡；酒按年份与醇厚分等，走商路外销，长老议定酒价，谁私压价就罚。"),
 "castle_village_B7_2": ("伊姆拉赫位于乌卡利翁地块中部起伏的丘陵上；这里的特产是酿巴旦尼亚甜葡萄酒的白葡萄。",
   "伊姆拉赫的人只种一种葡萄。村里人说别的葡萄管饱，这葡萄管甜——甜味是别处学不去的。",
   "伊姆拉赫专出甜酒白葡萄——坡地朝阳、日暖夜凉，果里存得住甜；葡萄按粒与甜分等，专供甜酒坊，熟期短，须赶在雨前摘完。"),
 "castle_village_B8_1": ("乌瑟莱姆位于巴旦尼亚东部边境的一座山脊上，俯瞰着米尔河的一条支流；当地人是林农，采伐橡树、紫杉和松树，把木材卖给建筑工和造船工。",
   "乌瑟莱姆的人认树不认路。村里人说紫杉给弓、橡树给梁、松树给船——砍哪棵，心里都有数。",
   "乌瑟莱姆以林农为业——山脊林好、材种齐；木料按树种与材分等，橡木走造船、紫杉留弓匠，边地买卖多做现钱。"),
 "castle_village_B8_2": ("肖尔达斯坐落于巴旦尼亚东部边境的低地，在乌卡利翁山崖脚下；附近的乌瑟莱姆要塞护着这片没有遮拦的农场，让它们免受东边斯特吉亚强盗的袭击。",
   "肖尔达斯的人靠要塞过日子。村里人说地是好地，就是没墙——要塞一在，强盗就绕道走。",
   "肖尔达斯以边境农作为业——崖下平地、宜种麦豆；因无险可守，粮仓多设在山崖侧洞，戍军常来收粮，边情一紧即抢收。"),
 "village_B1_1": ("达尔门格斯位于泰瓦尔湖——也就是乌卡利翁地块中央的暗湖——之畔；这里土地平坦肥沃、雨水丰沛，宜种小麦与大麦。",
   "达尔门格斯的人不记得旱过。村里人说湖边就是这样，要水有水，要地有地——剩下的就看人勤不勤。",
   "达尔门格斯以湖畔麦作为业——平土肥、雨水匀；小麦与大麦分仓，粮走湖岸与山路外销，湖雾重则麦易倒，须勤看田。"),
 "village_B1_2": ("埃贝雷斯坐落于一处鞍状山脊上，一边是泰瓦尔湖，另一边通往卡瓦尔谷的隘口；当地村民种葡萄，酿出巴旦尼亚地方甘甜的白葡萄酒。",
   "埃贝雷斯的人守着一个山口。村里人说湖在这边，路在那边——酒卖得出去，隘口就得有人盯着。",
   "埃贝雷斯以隘口葡萄为业——山脊日照足、排水好；白葡萄按粒与甜分等，酒经隘口外销，隘口一乱酒价先涨。"),
 "village_B1_3": ("贝格洛米艾位于乌卡利翁地块南边峭壁的一处角落里；除山上的矿床和沼泽里的沼铁矿外，此地并无别的长处。",
   "贝格洛米艾的人不嫌地方偏。村里人说石头有铁、泥里有铁——能刨出一口饭的地方，就不算差。",
   "贝格洛米艾以沼铁为业——浅层沼矿、随处可挖；铁料就炉粗炼，按成色分等，供农具与矛头，雨季沼深则歇工。"),
 "village_B1_4": ("阿斯·卡瓦尔靠近卡瓦尔谷——通往帝国领土的陡峭山谷的起始端；山上奔流而下的小溪里，可以收集到上好的黏土。",
   "阿斯·卡瓦尔的人顺着溪水找泥。村里人说水从山里带了土下来，沉在弯处——那儿一挖就是好泥。",
   "阿斯·卡瓦尔以溪泥为业——谷口水缓、泥沉成层；泥按细粗分等，供陶工烧器，山口通帝国，外运得便、价也卖得高。"),
 "village_B2_1": ("迪安托格麦尔位于“林·泰瓦尔”也就是“暗湖”之畔；此地名字的解释有好几种传说，意思是“白鼬之翔”，却没有两个版本对得上；村民从湖边收集黏土，供附近的工匠使用。",
   "迪安托格麦尔的人自己也说不清村名。村里人说祖宗留下的名字，意思记不全了——反正泥是真的，拿来有用。",
   "迪安托格麦尔以湖泥为业——岸滩淤土、细而耐烧；泥按细粗分等，供附近匠人制器，村名传说争执不休，倒成了外人打听的由头。"),
 "village_B2_2": ("格伦利斯里格——一条湿滑的溪谷——位于乌卡利翁北边的高地上；当地村民多是林农，为附近的要塞城镇邓格拉尼斯和卡·班塞斯供应木材。",
   "格伦利斯里格的人不信干路。村里人说谷里哪块石头滑，闭着眼都知道——木头靠人扛出去，脚底得稳。",
   "格伦利斯里格以溪谷林材为业——湿谷树密、材种杂；木料按材与龄分等，专供两座要塞城镇，山路难走，冬季伐木、雪化后外运。"),
 "village_B2_3": ("莫里希格坐落于乌卡利翁地块中部的一块盆地中；这一带以盛产小麦和大麦闻名。",
   "莫里希格的谷子堆得下不去脚。村里人说盆地里风小，麦子站得住——一年忙两季，够吃也够换。",
   "莫里希格是盆地粮村——土厚墒足、宜连片耕作；麦按饱满与干湿分等，粮走山路与湖运外销，秋后集中打谷、外村雇工最多。"),
 "village_B3_1": ("博格·贝斯坐落于乌卡利翁地块东部一座地势低矮的林谷中；村民们在阴暗的沼泽林地里下套子，捕捉河狸、狐狸和其他毛皮兽。",
   "博格·贝斯的人看泥看脚印。村里人说天黑了才不用看——兽走的路，泥上写得清清楚楚。",
   "博格·贝斯以林间毛皮为业——沼林兽多、套猎成习；皮张按毛色与完整分等，河狸皮最贵，冬季毛厚时外运、皮贩登门。"),
 "village_B3_2": ("格纳特·纳尔是巴旦尼亚传说中决斗之地附近的一座村庄，据传纳尔在此留下自己的头颅；村民以出众的护林本领闻名，帮着附近悬崖上的卡·班塞斯要塞抵挡斯特吉亚人和其他掠夺者，守着巴旦尼亚的腹地。",
   "格纳特·纳尔的人走在林子里没声。村里人说外人进谷先看见树，我们进谷先看见人——守的是那道崖。",
   "格纳特·纳尔以护林据险为业——熟林道、善设伏；族人按村落编伍轮流守崖，报警用号角与烟，林货与山货是常产，战时先顾要塞。"),
 "village_B3_3": ("托·莱阿德——满月之丘——坐落于巴旦尼亚北部一座树木繁茂的小丘下；村民们采伐橡树、紫杉和山毛榉林，几百年来巴旦尼亚人细心照料这些树木，好让木材一直够砍够卖。",
   "托·莱阿德的人砍树先看年岁。村里人说祖宗定的规矩——幼树留着，老树才动，砍了要补。",
   "托·莱阿德以丘林轮伐为业——林龄分区、砍补相续；木材按树种与材分等，橡紫杉供器用，族人共守伐期，逾规者罚。"),
 "village_B4_1": ("布林·格拉斯坐落于乌卡利翁地块东边的悬崖上；村民在山坡上种葡萄，酿出浓郁的深红色葡萄酒，深受瓦兰迪亚人和帝国人喜爱。",
   "布林·格拉斯的人看天色下剪子。村里人说崖上日头毒，葡萄才够浓——等的人多，可急不得。",
   "布林·格拉斯以崖坡酿酒为业——日照烈、排水快，酒色深；酒按年份与浓淡分等，外销瓦兰迪亚与帝国，买家常年预定大半。"),
 "village_B4_2": ("安杜恩坐落于俯瞰“林·泰瓦尔”也就是暗湖的山上裂口处；这一带的村民种树，采伐山毛榉、橡树和紫杉，再卖到各城镇去。",
   "安杜恩的人站在裂口上看湖。村里人说树是自家种的，砍也得自家拿主意——外人别想乱动一根。",
   "安杜恩以山裂林材为业——裂口挡风、树直材厚；木料按树种与材分等，山毛榉供器具、橡木供梁，走山路销往各镇。"),
 "village_B4_3": ("马格·阿尔巴坐落于俯瞰“林·泰瓦尔”也就是暗湖的草场上；村民们在泥泞的山谷里种植亚麻。",
   "马格·阿尔巴的人跟湿泥打交道。村里人说草场给牲口，泥谷给亚麻——湿是湿了点，麻线倒是韧。",
   "马格·阿尔巴以谷地种麻为业——湿土宜麻、纤维韧长；麻按纤维与色泽分等，沤麻靠谷水，供织坊纺线与结网，多与草场牲畜搭卖。"),
 "village_B4_4": ("斯温林俯瞰着一道岩石大瀑布——它拦下泰瓦尔湖的暗水，在乌卡利翁中央汇成湖；马匹在激流与瀑布之上的草地上吃草。",
   "斯温林的人听得见瀑布。村里人说水声大不大，就知道湖里涨没涨——马只要那片没淹的草，就够了。",
   "斯温林以瀑布旁牧马为业——水气润草、马壮耐走；马按齿口与脚力分等，因近水源要道，马与皮货借水运外销。"),
 "village_B5_1": ("杜恩位于特朗河上游峡谷的下方；水势平缓时，村民从河边的水洼泥塘里刮取淤泥，再沿着蜿蜒小路把泥背到头顶悬崖上的彭·坎诺克城。",
   "杜恩的人一辈子在爬坡。村里人说河边取泥不难，难的是那截路——可上头认这泥的账。",
   "杜恩以河塘淤泥为业——水缓积泥、质细可用；泥背运上崖供彭·坎诺克窑口，按细粗分等，汛期水浊则停采、等泥沉。"),
 "village_B5_2": ("盖恩塞斯位于乌卡利翁地块的东部边界、莱恩诺丘陵之下一座阴暗的林谷中；村民在湿地里取泥，卖给彭·坎诺克的陶工。",
   "盖恩塞斯的人摸黑也能取泥。村里人说林子挡了日头，泥才养得好——陶工就等着这一趟。",
   "盖恩塞斯以湿地陶土为业——林谷积水、土细而黏；泥按细粗分等，专供彭·坎诺克窑口，秋后开坑取泥、春耕前封坑。"),
 "village_B5_3": ("韦农·埃蒂尔坐落于俯瞰特朗河支流——埃蒂尔河源头的一座露头山上；村庄下方，河水在成堆的矿渣间蜿蜒流过，这些矿渣是几百年来在附近山丘上开采富铁矿留下的。",
   "韦农·埃蒂尔的人从小就认得那种黑水。村里人说水里的黑不是脏，是几百年前挖矿的人留下的印子。",
   "韦农·埃蒂尔以富铁矿为业——山顶露头、矿脉浅；铁料就地粗炼、按成色分等，渣堆沿河淤积，供矛头与农具，雨天大水则冲渣淤田。"),
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
        "WHERE settlementType='village' AND culture='Culture.battania' ORDER BY settlementId").fetchall()
    assert len(rows) == 33, len(rows)

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
    with io.open(os.path.join(WS_AUTH, "sources", "source-game-villages-desc-battania.yaml"), "w",
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
    print("generated:", len(made), "battania villages; snapshot hash:", file_hash[:16])


if __name__ == "__main__":
    main()
