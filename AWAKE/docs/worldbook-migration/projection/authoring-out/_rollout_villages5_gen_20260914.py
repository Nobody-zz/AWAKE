# -*- coding: utf-8 -*-
"""百科化村庄铺开批 5：库赛特 35 村 → 35 档。
独立来源登记 source.calradia.game.villages-desc-khuzait；红线同前批。
"""
import io, os, json, re, hashlib, sqlite3, yaml

OUT = os.path.dirname(os.path.abspath(__file__))
WS_AUTH = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
SRC_ID = "source.calradia.game.villages-desc-khuzait"
SRC_VER = "bannerlord-1.3.15.110062"
SNAP = "game-villages-desc-khuzait.txt"

L2 = {
 "castle_village_K1_1": ("乌赛克坐落于库赛特人称作达那孜海的塔奈西斯湖东岸、阿克坎丘陵之下；村民不久前还过着游牧生活，养的绵羊羊毛又长又好。",
   "乌赛克人放下帐篷没几年。村里人说羊还是那群羊，只是圈起来了——好毛是跟着人走的，不是跟着帐篷。",
   "乌赛克出长毛绵羊——湖东草场接丘陵、冬春两季牧；毛按长度与净度分等，长毛织毡与粗呢，收毛人认手感与色泽。"),
 "castle_village_K1_2": ("埃斯梅及周围村落位于阿克坎丘陵向陆一侧的德夫赛格高原上；这里是库赛特及其盟友的主要放牧地。",
   "埃斯梅的草场一眼望不到边。村里人说这儿是汗国的后院——牛羊走到哪儿，哪儿就是库赛特。",
   "埃斯梅是高原牧业要地——草场连片、多部族共用；牲畜按部族记号分群，牛市与羊市随季节迁移，草情定全年行情。"),
 "castle_village_K2_1": ("阿契赛尔位于阿克坎丘陵的南端；这一带土层破碎，铁石在沟壑之间显露出来。",
   "阿契赛尔的沟里能捡到黑石头。村里人说地是自己裂开的——不挖都知道底下有什么。",
   "阿契赛尔以浅层铁矿为业——沟壑露矿、采法省工；矿石按成色分堆，驮运出丘陵卖给锻造作坊，铁价随军需起落。"),
 "castle_village_K2_2": ("喀木沙位于库赛特人称作达那孜海的塔奈西斯湖畔；当地渔业不盛，村民在湖边收集黏土，卖给柴坎及其他城镇的陶工。",
   "喀木沙人不靠鱼吃饭。村里人说湖里没给什么，岸上倒给——那泥一捏就是碗。",
   "喀木沙供陶土——湖岸取泥、随滩分质；货走柴坎等城镇窑口，泥性看手感与烧色，近岸细泥为上，粗泥做砖坯。"),
 "castle_village_K3_1": ("哈坤坐落于阿克坎丘陵的悬崖之上；人们在这里采银已久，库赛特征服此地后，历代可汗对这行当也乐于支持。",
   "哈坤的坑道比村子的年头还长。村里人说谁当家都得用银子——可汗也一样，所以矿从不歇。",
   "哈坤以银矿为业——崖间坑道世代开挖、矿口分散；银料就地粗炼，税由汗帐派官征收，成色官验，私市不敢明着收。"),
 "castle_village_K3_2": ("基拉兹位于德夫赛格高原南端的一处盆地内；不久前这片土地还由游牧者支配，如今当地人仍靠饲养绵羊谋生。",
   "基拉兹的人把马鞍换成羊鞭没几年。村里人说盆地里风小，羊好过冬——比骑马省心。",
   "基拉兹以牧羊为业——盆地草场避风、冬牧稳定；毛与肉同出，羊毛按季集中剪收，肉畜赶赴高原集市交易。"),
 "castle_village_K4_1": ("泰佩斯俯瞰着喀拉卡孜河的一条支流——那河自北方流入帝国人称作塔奈西斯的达那孜海；该地盛产木材，人们顺流放木，销往湖边城镇。",
   "泰佩斯的人靠水吃饭。村里人说树在坡上长，钱在水里漂——木头一放进河，就自己走到城里去了。",
   "泰佩斯以木材为业——坡林取材、扎筏顺流；木料按材与龄论价，湖边城镇的船坞与屋梁是常客，汛期是放筏窗口。"),
 "castle_village_K4_2": ("库鲁卢克位于喀拉卡孜河的支流札罕河畔；这片谷地几乎终年积雪，草原小马靠吃春草、冬天刨雪找食，厚毛皮帮它们扛住砭骨寒风。",
   "库鲁卢克的马自己会刨雪。村里人说这儿的马不用喂得太细——雪底下有草，冷风里头有毛。",
   "库鲁卢克出耐寒小马——雪谷放养、料省体健；马匹耐寒耐远，适合驿递与轻骑，厚毛品种在秋后最俏。"),
 "castle_village_K5_1": ("希木利位于德夫赛格高原的一处盆地内；这里几乎终年干旱，却仍有冬雪与泥泞的春天；雪化雨停留下的水坑边，能挖到上好的黏土。",
   "希木利的水坑是老天给的窑口。村里人说一年就盼那两场水——水一退，泥就熟了。",
   "希木利靠水坑取泥——时令短、产额随雨雪；泥料按坑分等，货驮往高原市镇窑口，逢大旱即断料，泥价反涨。"),
 "castle_village_K5_2": ("鄂木罗托克坐落于德夫赛格高原东边的速仑山脚下；村民往附近的树林采伐这一带为数不多的木材。",
   "鄂木罗托克的林子金贵。村里人说高原上树比人少，砍一棵得记着补——要不往后连车辕都没处找。",
   "鄂木罗托克供高原木材——林小量少、择木再伐；木料按材分等，车辕、帐杆与屋梁各取所需，因稀缺旱年价高。"),
 "castle_village_K6_1": ("迪纳尔位于库赛特部落联盟的极北之地，沿喀拉卡孜河而建；这里几乎没有树木与山丘，挡不住自东北草原沿冰冻河面刮来的寒风，出产的羊毛因此异常浓密。",
   "迪纳尔的风是顺着河走的。村里人说没山没树，就只剩羊顶着——风越硬，毛越长。",
   "迪纳尔以厚毛羊著称——河畔无遮、羊自蓄厚绒；毛按绒厚与净度分等，极北的厚毛价比南边高一截，冬前被订空。"),
 "castle_village_K6_2": ("喀拉哈力位于库赛特部落联盟的极北之地，离冰冷的比亚里海不远，是与斯特吉亚人常年争夺的边境；这里几乎终年积雪，春草仍养活了相当多的绵羊，厚羊毛帮人挡下海上刮来的砭骨寒风。",
   "喀拉哈力两边都说是自己的。村里人说换谁的旗都一样，羊照放——就是收毛的人来得少了，价倒涨。",
   "喀拉哈力出边境厚毛羊——争地不定、牧户结伴放牧；毛货多由行商冒险上门收，边情一紧毛价先涨，货也最容易被劫。"),
 "castle_village_K7_1": ("西米拉位于德夫赛格高原的东侧，再往东便进入大草海；当地村民饲养能耐受寒冬酷暑的牛。",
   "西米拉的牛什么天都见过。村里人说高原的冷、草海的热，两头都熬过来——这样的牛才顶用。",
   "西米拉以牧牛为业——接草海、气候两极，牛只耐役；役牛与肉牛分栏，皮张与腌肉同销，牛市随草海迁徙季起落。"),
 "castle_village_K7_2": ("柯希·阿吉克位于人称恋人山脚下的一条狭窄干旱的峡谷中，村名也源自此山；当地人从东边的游牧民手里买马，再转手卖给城镇里的库赛特贵族。",
   "柯希·阿吉克人靠牵马过日子。村里人说马从草海那头来，往贵人院子里去——我们只管中间的这段路。",
   "柯希·阿吉克是马匹转口村——峡谷当道、便于控价；马按血统齿口分等，转手加价视买家身份，贵族点名要的马先留。"),
 "castle_village_K8_1": ("埃泽努尔位于当地方言称作帖牙合的蒂亚具斯河畔；村民在河岸的裸岩上采铁，河水也因此泛着微红。",
   "埃泽努尔的水是红的。村里人说那不是血，是铁——河底下有东西，洗也洗不掉。",
   "埃泽努尔临河采铁——岸岩浅、易取；矿石就近转运，河水泛红成了收货人的记号，铁价看军需与外运的通路。"),
 "castle_village_K8_2": ("格烈登及其附属村落一直延伸到德夫赛格高原的南端，靠近斡祖河与帖牙合河的交汇处；这里地形崎岖，却以山涧里出的上等黏土闻名。",
   "格烈登的泥藏在涧里。村里人说路难走，可泥好——别处烧出来的碗会裂，这儿的不会。",
   "格烈登供山涧细泥——取土靠人背出涧、量少质优；泥料专供细器，价高出邻村一倍，两河交汇处的渡口是外运要道。"),
 "castle_village_K9_1": ("开撒尔位于库赛特领土的东北方，在喀拉卡孜河谷里；这里几乎终年积雪，村民仍饲养着扛得住顺流自北方刮来的寒风的牛群。",
   "开撒尔的人跟雪过日子。村里人说雪早来晚走，牛就挑最扛冻的养——人冷点没事，牛冻坏了就没明年。",
   "开撒尔以耐寒牛群为业——河谷雪长、牧期短；牛只依役用与肉用分栏，皮张与腌肉是秋冬大宗，雪厚则出栏推迟。"),
 "castle_village_K9_2": ("帕亚木位于库赛特领土的寒冷北方，沿喀拉卡孜河而建；这里几乎终年积雪，但村民在地刚解冻时就播下小麦与大麦，秋收因此还不错。",
   "帕亚木的人种地像抢东西。村里人说雪一化就得下种，慢一天就少一斗——好在河边的地肯长。",
   "帕亚木种早熟麦——解冻即播、生长期短；粮产供应河谷与戍所，收粮看雪化早晚，春迟则减产、粮价早涨。"),
 "village_K1_1": ("菲斯纳尔位于喀拉卡孜河的支流札罕河畔；这里几乎终年积雪，春草仍养活了相当多的绵羊，厚厚的羊毛帮它们扛住严酷寒冬。",
   "菲斯纳尔的羊熬得住雪。村里人说别处的羊得进棚，我们的羊自己在雪里站着——毛厚就是底气。",
   "菲斯纳尔出雪原绵羊——河畔春草短、羊蓄厚绒；毛按绒长分等，厚绒供毡帐与冬衣，剪毛季在春末，价随寒年走高。"),
 "village_K1_2": ("乌兰坐落于喀拉卡孜河的河谷中；村民是被可汗下令定居不久的游牧人，已学会在这片低洼泥泞的地上种植亚麻。",
   "乌兰的人刚学会扶犁。村里人说住帐篷的时候看天，住进村子就看地——这泥地还真给饭吃。",
   "乌兰新习耕作——低洼湿地产麻、纤维韧；麻料多与河谷粮货搭运，沤麻靠渠水，收麻看色泽与纤维长度。"),
 "village_K1_4": ("阿萨利格坐落于德夫赛格高原之巅的巴尔思山脚下；村民在相对低矮的山坡上养马，还得时时提防雪豹——山名也正是因此而来。",
   "阿萨利格的马得有人守。村里人说雪豹比贼还精，夜里进圈叼的总是最好的那匹——所以守夜的人不敢打盹。",
   "阿萨利格以高山育马为业——坡缓草短、马腿硬；因雪豹常来，圈栏筑得高、守夜成例，马按齿口体态定价。"),
 "village_K2_1": ("喀拉卡拉特位于德夫赛格高原东部平缓边缘的一处盆地内；不久前这片土地还由游牧者支配，当地如今仍以繁育良马闻名。",
   "喀拉卡拉特的马是盆地里养出来的。村里人说风被山挡住了，马就不用顶风跑——养得舒展，跑起来也舒展。",
   "喀拉卡拉特出良马——盆地避风、草场平缓；马场按血统分群，军马与商队坐骑各走一路，好马须先应汗帐征调。"),
 "village_K2_2": ("帖斯密勒位于发源于德夫赛格高原南端的斡祖河畔；居民是牧羊人，多亏历代可汗清剿马匪，他们才得在库赛特的强权之下安稳过活。",
   "帖斯密勒的羊从前常被抢。村里人说如今夜里能睡整觉了——汗国的税虽重，可比马匪讲道理。",
   "帖斯密勒以牧羊为业——河畔草场安稳、牧期长；毛肉双收，商队过河即成交，治安好则外运顺、羊价稳。"),
 "village_K3_1": ("沙佩什特位于札罕河与喀拉卡孜河的交汇处；村民靠开采附近的盐矿谋生。",
   "沙佩什特的盐比粮金贵。村里人说河是两条，盐只有一处——谁占着矿，谁就说了算。",
   "沙佩什特以盐矿为业——矿层贴两河口、便于外运；盐按粒色与纯苦分等，旱年缺盐价高，两河渡口即盐集。"),
 "village_K3_2": ("哈内希坐落于阿克坎丘陵北部的一处山嘴上；土石上的红色条纹透出底下有铁，村民正在开挖。",
   "哈内希的人顺着红纹找矿。村里人说山自己把记号画在石头上——顺着走，准找到。",
   "哈内希以露头铁矿为业——红纹为记、浅挖即得；矿石按成色分堆，驮运出丘陵交锻造作坊，铁价随兵事涨落。"),
 "village_K3_3": ("渔村马津坐落于库赛特人称作达那孜海的塔奈西斯湖北端；村民在喀拉卡孜河附近布下渔网，捕捉往返湖中的各种鱼。",
   "马津的人跟着鱼走。村里人说鱼从河进湖、又从湖回河——网就布在它必经的路上。",
   "马津以湖河渔获为业——河口设网、渔期随鱼群洄游；鲜鱼就近走湖岸市集，多盐渍干制外运，渔汛一过即转补网。"),
 "village_K4_2": ("兰萨木位于德夫赛格高原东南方的平原上；不久前还以游牧为生的村民，如今精于饲养马匹。",
   "兰萨木的人上马不用想。村里人说骑了一辈子，改行也改不掉——干脆拿养马当营生。",
   "兰萨木出马——平原草场开阔、牧马成习；马按用途分驿马与战马，汗帐征调在先，余马走高原集市。"),
 "village_K4_3": ("米万占坐落于德夫赛格高原东边的速仑山脚下；村民不久前还过着游牧生活，如今在山麓的丘地上饲养绵羊。",
   "米万占的羊圈搭在山根下。村里人说从前追着水草跑，现在守着这片坡——羊倒长得更匀了。",
   "米万占以山麓牧羊为业——坡草短、放牧近；羊毛按季剪收，肉畜赶到高原市集，圈养之后羊毛净度反有提升。"),
 "village_K4_4": ("乌伦占位于阿克坎山脚下、德夫赛格高原的西端附近；当地村民养牛以应对寒冬与酷暑。",
   "乌伦占的牛什么天都挨过。村里人说山根下冬冷夏晒，能站住的牛才留种——孬的早卖了。",
   "乌伦占以牧牛为业——山麓草场接壤高原，牛只耐役；皮张、腌肉与役牛三路出货，牛市在秋后与春初两旺。"),
 "village_K5_2": ("奥胡坦是一座位于小麦产区中央的村庄，地处库赛特人称作达那孜海的塔奈西斯湖与阿克坎丘陵之间的狭长平原。",
   "奥胡坦的麦地夹在湖和山中间。村里人说两边挡着，风小水足——这地方种粮，老天还算给面子。",
   "奥胡坦是高原粮村——狭长平原水土相宜、麦田连片；粮货走湖岸与丘陵商路外销，收粮看饱满与干湿，秋后集中出仓。"),
 "village_K5_3": ("依斯潘塔尔位于阿克坎丘陵的一处多岩山嘴上，俯瞰着库赛特人称作达那孜海的塔奈西斯湖；村民主要靠养羊过活。",
   "依斯潘塔尔的羊在石头坡上吃草。村里人说地薄，可羊不挑——石头缝里的草也是草。",
   "依斯潘塔尔以牧羊为主业——岩坡草零碎、放牧范围大；羊毛与羊皮同出，湖岸市集是主要销路，毛价随牧季起落。"),
 "village_K5_4": ("帕巴斯坦坐落于阿克坎丘陵与库赛特人称作达那孜海的塔奈西斯湖之间的狭长平原上；这里是种小麦的好地方。",
   "帕巴斯坦的人只认麦子。村里人说湖给水、山挡风，别的活儿不用想——把麦种好就够吃够卖。",
   "帕巴斯坦专营麦产——平原狭长、宜连片耕作；粮货经湖路外销，收粮看干湿与杂质，麦熟季全村下田、外来雇工多。"),
 "village_K6_1": ("喀拉罕位于柯希·罗希尼——黎明山脉山麓的丘陵地带；村民向迁徙的部落买马，自己也养一些，库赛特军队的许多马都出自此地。",
   "喀拉罕的马有一半是买来的。村里人说部落的马路过，我们就挑好的留下——留下的马，将来是要上战场的。",
   "喀拉罕是军马集散村——山麓草场兼作中转栏；马按血统齿口分等，军马优先应征，买进卖出的差价是村中大账。"),
 "village_K6_2": ("努丘克位于一片裸岩山脉之下——那山的达西语名为柯希·罗希尼，即黎明山脉；村民多是新近定居的游牧人，像祖先那样饲养绵羊。",
   "努丘克的人还惦记着旧日子。村里人说羊跟祖辈放的是一样，就是不再挪窝了——山还在，路不走了。",
   "努丘克以山麓牧羊为业——崖下草场避风、放牧固定；羊毛按季剪收，肉畜赶集市，定居之后羊群规模比游牧时稳定。"),
 "village_K6_3": ("达纳拉位于库赛特人称作昌尕淖尔的那片不解渴的盐湖旁；村民从苦涩的湖水里取盐。",
   "达纳拉的水喝不得，盐却用得。村里人说湖是咸的，日子也是咸的——可咸有咸的用处。",
   "达纳拉靠湖盐为生——湖水晒取成盐、粒粗味苦；盐货驮往高原与草海市集，收盐看粒色与干度，咸水湖边寸草不生。"),
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
        "WHERE settlementType='village' AND culture='Culture.khuzait' ORDER BY settlementId").fetchall()
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
    with io.open(os.path.join(WS_AUTH, "sources", "source-game-villages-desc-khuzait.yaml"), "w",
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
    print("generated:", len(made), "khuzait villages; snapshot hash:", file_hash[:16])


if __name__ == "__main__":
    main()
