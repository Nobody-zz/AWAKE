# -*- coding: utf-8 -*-
"""
Awake.Button.Primary 出图（110×35，四态）—— 木面＋铁框＋一道嵌线

⚠️ 形制两条已定的硬约束（都是被甲方纠回来的，别往回走）：
  1. **几何对称**。「这是个按钮，是系统性的控件，是要到处都用的那种」
     ⇒ 轮廓必须是**严格矩形**：四个角是同一种角，四面外框等宽，嵌线四周闭合等宽。
     端斜切／鼓形／四角包铁角件 这些一律作废 —— 那是"手工器物"的读法，
     系统性控件要的是**可复现的几何**。（形制研究留档见 make_button_shape_study.py）
  2. **不按文明分皮**。UI 是**玩家的界面**，玩家用同一块面板，
     跟他自称帝国人还是巴旦尼亚人无关。
     "华丽是帝国文明的、蛮族不能称华丽"是**内容口径**（描述世界里的文明），
     控件是**控件口径**——把它俩混在一起是范畴错误。
     要按归属换皮的是**内容美术**（信件／旗帜／纹章＝"这是谁的"），不是按钮。
     工程上也别想分：Brushes 是静态 XML，没有"按玩家文明选 brush"的机制。

为什么是"木面＋铁框"（而不是单材料）：
  单材料只有一个调子，读出来是"一块材质样本"，不是"一件东西"。
  真实器物是**两种材料相处**：木给温、铁给硬与形，接缝处才看得出做工。
  卡拉迪亚满地是木＋铁（盾、门、车、箱）——木箱铁箍就是这个世界的容器语法。

**"肉要安静、骨要清楚"**（清晰度那一轮学到的）：结构感放在骨上
（轮廓／外框／嵌线／倒角），面只留极轻的一层 —— 面一旦花，摆成一排就是一片脏。

分工：
  AI 出「质」—— 一片旧木板的实拍肌理；金值也从实拍中位实测取
  代码出「骨」—— 轮廓／外框宽度／嵌线位置／倒角／四态／像素精确尺寸

依据（一手，本机模组自己的生产 Prefab）：
  AwakeMessenger.xml / DeveloperCheck.xml / NpcDialogue.xml 引用
  Brush="Awake.Button.Primary"，尺寸 110×35，状态 normal / hover / pressed / disabled。

产物：
  GUI/SpriteParts/ui_awake_button/btn_primary_110{,_hover,_pressed,_disabled}.png   （1× 实际像素，RGBA）
  out/button-primary/sheet_btn_primary.png                                           （样本页，大图行用 NEAREST）
"""
import os
import sys
import math
import random

from PIL import (Image, ImageDraw, ImageChops, ImageEnhance, ImageFont, ImageStat,
                 ImageFilter, ImageOps)

import curve_shape as _CS          # 端头曲线（本族三控件共用，见 curve_shape）

HERE = os.path.dirname(os.path.abspath(__file__))
AWAKE = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_BTN = os.path.join(AWAKE, "GUI", "SpriteParts", "ui_awake_button")
OUT_SHEET = os.path.join(HERE, "out", "button-primary")

_MEDIA = r"C:\Users\26811\.workbuddy\plugins\data\mcp-miora\miora-media"
IRON_SRC = (sys.argv[2] if len(sys.argv) > 2 else
            os.path.join(_MEDIA, "miora_text_to_image-1789318033039-0-ec95925f59df.jpg"))
WOOD_SRC = (sys.argv[1] if len(sys.argv) > 1 else
            os.path.join(_MEDIA, "miora_text_to_image-1789318413367-0-ca62188b12b5.jpg"))

W, H = 110, 35          # 交付尺寸（与既有约定一致：1× 实际像素）
SS = 4                  # 超采样倍数。8 是负优化：从 8× 缩回 1× 时，多出来的高频
                        # 会折成噪点，看着更糊。4 是"够抗锯齿、又不往图里塞读不出来的细节"。

# 清晰度的总开关：**取多大一块源图贴到按钮上**（源像素宽 ＝ 交付宽 × 此值）。
# 1.0 ＝ 源图原大（最锐，纹理很"近"）；2.0 ＝ 放大约两倍（木纹走向看得清）；
# 再往上就开始糊 —— 老版取 704px 贴到 110px＝6.4 倍压缩，木纹全被压到亚像素。
CROP_ZOOM = 2.0
CROP = (int(W * CROP_ZOOM), int(H * CROP_ZOOM))     # 木／铁都按这个像素尺寸取块

RADIUS = 1.0            # 圆角（1× 像素）——**只对"矩形兜底"那一档有效**（TIP_LEN=0 时用）。

# ════════════════════════════════════════════════════════════════════════════
# 🔴 铁轨的**第二道亮棱**（2026-09-14 11:0x，甲方：「不好看。你的质感和颜色太暗沉了」）
#
# 这条不是调参，是**补一处结构缺失**。量出来的证据链：
#   ① 原版中列剖面（缩到 h=35）**对称**：上 y02=**133.6**、下 y30=**127.9**，
#      也就是铁轨的**两条长边各有一道近白高光**；
#   ② 本项目只有**顶部一道**（y02=101.3），底部没有 ⇒ 亮度分布一路塌到 21；
#   ③ 后果直接写在直方图上 —— 高光 >85 占比：原版铁面 **11.6%** vs 本项目 **2.0%**；
#      次按钮／页签铁面甚至 **0.0%**（一颗都没有）。
#   ⇒ 原版的铁是"**两侧亮棱夹着一道暗沟**"，我们做成了"上亮下暗的一片灰"。
#      没有高光的铁 = 一坨灰 = 就是甲方说的"暗沉"。
#
# ⚠️ 为什么不是"删掉 FRAME_DIR"（那会让整圈均匀变亮）：
#   原版**上下都有亮棱**，所以这是**结构**、不是受光方向；但受光方向那点差异要保留
#   （按下态靠翻转 `dirf` 表达）。⇒ 保留下沿的 `FRAME_DIR` 压暗，**另加一道独立的亮棱**，
#   位置与上沿镜像，强度按上下比例缩放（下 127.9 / 上 133.6 = 0.957）。
RAIL_EDGE = True        # 铁轨下沿亮棱开关（关掉即退回"只有上沿有光"）
RAIL_EDGE_D = 2.0       # 离外形几像素（1×）——原版上沿亮棱在 y02、暗沟在 y05 ⇒ 取 2.0
RAIL_EDGE_W = 1.6       # 棱的过渡宽度（1× 像素）。`budget_shade` 只有 1× 的层分辨率
                        # （`dist` 的层号就是 1× 像素），所以棱本质是"一层"，宽了也没台阶。
RAIL_EDGE_L = 122.0     # 亮棱峰值（原版上沿 133.6、下沿 127.9；本项目取 122 与 FRAME_L 的 122 一致）
RAIL_EDGE_DIR = 0.957   # 下 / 上 的强度比（原版实测 127.9 / 133.6）

# ============================================================================
# 轮廓：**两端收尖**（2026-09-14 02:2x，甲方：「那个形状就比我们设计的好看」）
#
# 实测原版（`_probe_shape.py` → out/study/shape/shape_probe.txt）：
#   alpha>128 的**实体**是 **232×42**（不是 271×84 —— 外圈那 5000 多像素是软光晕）。
#   上下缘**平直**，只在**两端各 6.9% 宽（16px / 232）**内收；
#   尖端高度 6/42 = **0.143**，收的过程接近**直线**（不是圆弧、不是鼓形）。
#
# ⛔ 我此前把甲方「几何对称」读成了「**严格矩形**」—— 那是我自己加的码。
#    两端收尖**也是几何对称的**（左右镜像、上下镜像），而轮廓本身就是信息量
#    （规则 19）。这条诊断我 02:0x 就写下了，却没动手 —— 知而不行，这次落地。
TIP_LEN = 9.0           # 两端收束的长度（1× 像素）。
                        # ## 2026-09-14 12:3x **16 → 9**：端头退出"结构件"，退回"收"。
                        #
                        # 取值史＝一条绕回来的弯路，留着当教训：
                        #   12（按同高折算）→ 16（"跟原版绝对像素"）→ **9（按视觉权重）**
                        #
                        # 为什么 16 是错的：原版端头 14px 确实成立，但它在 **232 宽**上
                        #   ⇒ 端头/宽 = **6.0%**，读起来是"两端略收"。
                        #   我们 16px 落在 **110 宽**上 ⇒ **14.5%**，比例翻了一倍多，
                        #   端头从"收"变成"两端各一个件"——**三段式，最散**。
                        #   ⚠️ "跟原版绝对长度"这条看着有道理，是因为**没换算成比例**。
                        #      凡"跟原版某个绝对量"的取值，必须同时报出它换算成本项目比例后的值。
                        #
                        # 判据不靠感觉：`out/study/shape/tip_len_ladder.png`（真尺寸对照梯，
                        #   6/8/9/10/12/16 + 圆角矩形参照）：
                        #     6px 5.5% 读不出端头 | **9px 8.2% 是"收"** | 12px 10.9% 已成独立元素
                        #     | 16px 14.5% 三段式 | 圆角矩形r=3 是另一族（太现代）
                        #   ⇒ 取 9：端头件还立得住，又远不到"独立元素"的门槛。
TIP_H = 0.16            # 尖端高度 / 全高（**仅退化档用**：TIP_LEN=0 时作矩形无害处理）。
                        # ⚠️ 曲线档下这个值**不参与**——钝口高度由 `curve_shape.CURVE_D0` 决定。
TIP_SEG = 4             # 保留常量（曲线档已不用二次贝塞尔）；改动前 grep 一下还有谁读它。


# ⚠️ 2026-09-14 11:4x **端头（八边形／收束那一段）本轮故意不动**，别当成漏了。
#    量出来的差（`_probe_shape.py` 口径，按 x 逐列取块最亮值）：
#      列位       原版    本项目
#      2.2%      18.7     17.0      ← 一样黑
#      6.5%      13.6    **58.8**    ← 本项目多一道亮棱
#      15%       96.4   **120.0**    ← 本项目那道亮棱冲到了近白
#      60%      121.6    122.0       ← 长边轨的亮棱，对齐得很好
#      90%      121.6   **101.6**    ← 本项目的端头**塌下去**了
#      98%       15.9     **19.0**   ← 原版端头最亮只到 15.9，我们是一整块 19 的亮斑
#    ⇒ 原版的端头基本是**暗的**（最高 15.9），我们是一块**亮斑**；而且原版最亮处
#      出现在 90% 宽（＝**收细那一段本身是亮的**），我们却在 90% 塌掉。
#      也就是"两端该收、该暗、该把最亮放进收细段"三条全反。
#    ⛔ 但这一改要动 `END_NOSE_C` / `END_RIM` / `END_LIFT` 一组常量，
#      而上一轮"廉价感"就是**在这个地方被磨圆的**（甲方原话：「那个形状就比我们设计的好看」）。
#      形的东西同时改两处，出了问题分不清是谁的账 ⇒ 端头**另起一轮专门做**。
#      （甲方 11:4x 已确认："只加长收尖，端头先不动"。）


def _bez(p0, c, p1, n):
    """二次贝塞尔采样 n+1 个点（含两端）。**仿射不变** ⇒ 镜像出来的曲线与镜像的曲线完全一致。"""
    out = []
    for i in range(n + 1):
        t = i / float(n)
        mt = 1.0 - t
        out.append((mt * mt * p0[0] + 2 * mt * t * c[0] + t * t * p1[0],
                    mt * mt * p0[1] + 2 * mt * t * c[1] + t * t * p1[1]))
    return out


# ============================================================================
# 预算（2026-09-14 02:2x —— 甲方拍「按原版语汇重做」）
#
# 依据＝原版 `General/Button/main_button_regular` 按高缩到 35（113×35，与本项目同尺寸）
# 的**逐行实测**（`_probe_budget.py` → out/study/budget_probe.txt）：
#   可见体 25 行 ＝ 外缘暗 4 ＋ 亮棱 2 ＋ 暗缝 1 ＋ 面 10 ＋ 下 4
#   面 49 → 74 → 66（**拱形**，峰在 45% 高）；框最亮只到 79（≈ 面高），**最外 4 行是 0**。
# 本项目原状：框 5px / 面 23px，面**全程 23~27（动态范围 4 级）**，最亮 184 落在最外缘。
#   ⇒ 面是一块平色、最亮在外缘（凸起感反了）⇒ 读成「贴上去的亮块」。
# ============================================================================
FRAME = 7.0             # **长边那条铁轨**的宽度（1× 像素）。
                        # ⚠️ 10:2x 实测原版 h=35 的轨就是 **7px**（逐行：0,65,97,57,32,19,31），
                        #    所以 7 不是估的 —— 是量出来的。
SEAM = 1.2              # 嵌线宽度（1× 像素）
# ⛔ 2026-09-14 10:2x **嵌线位置从"箍内缘"搬到"离外形 1.6px"** —— 这是"廉价感"的一大来源。
#    原版那道亮棱（97）在离外形 **2px** 的位置，前面只有一道黑（0）和一层中调（65）；
#    箍内缘（5~6px）反而是**最暗的一段（19~32）**。我们原来把金线放在内缘 ⇒
#    铁轨里同时出现"亮棱 ＋ 金线"两道亮东西，7px 里塞两件 ⇒ 花、碎、廉价。
#    现在：**铁轨上只有一件亮东西＝那道嵌线**（它正好落在原版亮棱的位置上）。
GOLD_INSET = 1.6        # 嵌线**离外形**的距离（1× 像素）
CORNER = 0.0            # 四角包铁角件边长。**0 ＝ 关**：角件会让轮廓变成八边形，
                        # 那是"手工器物"的读法；控件不能有这种不可复现的台阶。
RIVETS = False          # 铆钉。**关**：单颗好看，摆一排就是一堆噪点（见组排研究）。
RIVET_R = 1.0           # （仅在 RIVETS=True 时用）

# ============================================================================
# 端头铸件（2026-09-14 03:0x）—— **这才是甲方说"形状好看"的那件东西**
#
# 把原版左端放大 ×8 看（`_probe_end.py` → out/study/shape/_ref_end.png）：
#   ① 外轮廓不是"矩形切角"，是一台**八边形**：上下缘平直 → 端头处两段约 45° 斜切
#      → 一条**很短的竖立面**（42 高里只占 6px）。收尖只是它的外沿。
#   ② 端头那块铁件**比长边轨厚得多**：长边轨约 5px（12% 高），端头铸件纵深约 23px
#      （55% 高）。也就是说 —— **原版自己就不是"四面等宽外框"**。
#      我写在文件头的那条"四面等宽＝几何对称"是**我自己加的码**（又一次）。
#   ③ 铸件里有一道**拱**：木面的端头是个**半圆头**，嵌进拱里；拱的内缘有一道亮棱。
#   ④ 拱的外侧有一个**圆孔**（铸件的螺栓孔），暗孔 ＋ 亮圈。
#
# ⇒ 所以本项目的"形"要补齐三件：八边形外轮廓（已有）＋ 端头铸件（本条）＋ 拱与圆孔。
#    ⚠️ 横向九宫格：端头铸件纵深 ＝ END_NOSE_C + 木面半头半径 ≈ 36.5px。
#       横向一旦要 StretchToParent，`ExtendLeft/Right` 必须 ≥ 37（110 里只剩 36 可拉）。
#       本项目横向按钮都是 Fixed，纵向才拉 —— 纵向只需 ≥ 9（见 NINE）。
# ============================================================================
END_BRACKET = False     # 端头铸件开关。
                        # ## 2026-09-14 12:3x **True → False**：端头不再是一件"东西"。
                        #
                        # 关掉它的理由不是"不好看"，是**层级用错了地方**：
                        #   · 按钮的第一印象该是"一块可以点的板"。
                        #     端头带铸件＋拱＋螺栓孔之后，它在跟按钮本体争注意力。
                        #   · 三控件同形（甲方定案）之后，**页签也长了铸件**——
                        #     页签要成排出现，一排下去全是结构件。
                        #   · 主次按钮的层级本来该由**形**分担，现在形状被拿去做"统一"，
                        #     只剩 10px 宽差和颜色深浅 —— 而颜色是最不靠谱的层级手段
                        #     （面板底色在换、有色弱玩家、还有光照）。
                        #   · **最后一条最硬**：`END_HOLE_R = 1.9` ⇒ 孔直径不到 4px。
                        #     在 35px 高的控件上它读不出"孔"，只能读出一块脏。
                        #     **细节密度超过显示分辨率之后，它不再是细节，是噪声。**
                        # ⚠️ 保留全部 END_* 常量与代码路径（`bracket=False` 即可复现旧观感），
                        #    但**默认关**。这条同时把 `NINE` 的左右边距从 16 松开（见 NINE）。
END_NOSE_C = 24.0       # 木面端头**半圆头**的圆心 x ⇒ 铸件纵深 ＝ 24 − 8.5 ＝ 15.5px
                        # （END_BRACKET=False 时本值不参与渲染；保留供复现）
                        # ⚠️ 2026-09-14 10:2x 从 28 收到 24：28 时两侧吃掉 39px，
                        #    110 里木面只剩 71px，读成"两块灰板夹一条木头"——**拥挤也是廉价**。
END_LIFT = 58.0         # 铸件**体色**（1× L）。
                        # ⚠️ 取值史＝一条弯路，留着当教训：72 → 96 → 38 → 70 → 58。
                        #    · 72 那版**从没生效**（坐标用错，见 budget_shade 的 ⚠️），所以"太暗"是假的结论；
                        #    · 96 是拿那个假结论去"修"，把大面积铁提成浅灰 ⇒ 更廉价（大面积浅灰是廉价的主犯）；
                        #    · 38 是拿**均值**对（原版端头均值只有 36~46），但**均值对 ≠ 看着对**；
                        #    · 70（上一版）实测端头中位 64.1，而原版中位 49.6 ⇒ 我们那块铁**比原版亮 15 级**。
                        # ⛔ 10:4x **终于量对了**（`_probe_end_profile.py` 打中线逐点）：
                        #    原版端头由外到内 = 52 / **88** / 49 →（质量 48~68）→ **16 / 25（暗缝）** → 木面。
                        #    ⇒ 铁件本体是 **48~68**、且它**外侧有一道 88 的亮边**、**贴木面那 2px 是暗的（16~25）**。
                        #    ⇒ 58 落在原版质量带的中段；亮边交给 END_EDGE，暗缝交给 END_RECESS_*。
                        #    **教训：拿均值当目标之前，先问"这个均值是被什么拉低的"。**
END_LIFT_D = 6.0        # （保留：无铸件那一档抬升用的过渡宽度）
END_OUTER_D = 2.0       # 端头铸件**外形边缘那道暗轮廓**的过渡宽度（1× 像素）
END_RIM = 110.0         # 拱内缘的**亮棱**（原版这里是最亮的一道，实测 149 —— 那是 42 高的原尺寸）
END_RIM_W = 2.6         # 亮棱宽度（1× 像素）
# ⛔ **暗槽**：铸件不能是一块实心板（那是"块"，廉价；原版那件东西是**线脚**）。
#    在离木面 END_GROOVE_D 处开一道暗槽，把铸件劈成"外圈线脚 ＋ 内圈凸台"两段
#    ⇒ 读起来像锻出来的线，不像贴上去的一块铁。
#    ⚠️ 槽底要**接近全黑**（8，不是 30）—— 原版那块铁的均值是 36，低的来源正是这些"透空"。
END_GROOVE_D = 7.0      # 暗槽**离木面**多远（1× 像素）
END_GROOVE_W = 3.0      # 槽宽（2.5 → 3.0：窄了只像一道划痕，宽了才像"开了个口"）
END_GROOVE_V = 8.0      # 槽底亮度（接近黑）
# ⚠️ 坑：`d_out` 的层数是**有上限**的（`band_out(..., END_DOUT_LAYERS)`）。
#    槽心一旦落在上限上，"上限以外"全被算成槽心 ⇒ **整块端头变成全黑**。
#    踩过：layers=8、END_GROOVE_D=8 ⇒ 端头实测 0~19（本该 70），图上看就是"端头没画出来"。
#    ⇒ 层数必须 **≥ 端头纵深（≈15.5px）**，槽心要留在层数以内。
END_DOUT_LAYERS = 18
END_HOLES = "none"      # 2026-09-14 12:3x **"axis" → "none"**：端头那一枚孔撤销。
                        # 理由见 END_BRACKET：孔直径 <4px，在本项目高度上读成噪点不读成孔。
                        # none | axis（轴上一枚）| pair（上下各一枚，仍严格上下镜像）
                        # 取 axis：**一枚**（原版每个端头也只有一枚），且落在水平轴上 ⇒
                        # 四向镜像都严格成立。pair 档在 1× 下读成"两个点"，偏吵。
END_HOLE_X = 8.0        # 孔心 x（两端镜像）⇒ 落在铸件实心那块里
END_HOLE_R = 1.9        # 孔半径。⚠️ 10:2x 从 2.6 收到 1.9：
                        #    2.6 ＋ 亮圈 156 出来读成**金属扣眼**（窗帘环），很廉价。
END_HOLE_OFF = 6.0      # pair 档：孔心离水平轴的距离
END_HOLE_DARK = 28.0    # 孔底亮度（收对比：20 → 28）
END_HOLE_RIM = 112.0    # 孔缘亮圈（156 → 112：孔只是"有个洞"，不该是个亮点）
END_HOLE_SOFT = 0.35    # 孔的软边半径（1× 像素）—— 硬边圆孔会读成"钻头打的"，软边才是"铸的"

# 铁轨的**剖面**：从外缘往内 7 档的目标亮度（1× L）。
# ⚠️ 2026-09-14 10:2x **直接抄原版实测**（`_probe_ref_values.py` → ref_values.txt）：
#    原版 h=35 逐行 0 ／ 65 ／ **97** ／ 57 ／ 32 ／ 19 ／ 31 ⇒ 然后接木面 44→72。
#    读法：外缘一道**黑**（0）→ 一层中调（65）→ **亮棱（97）在离外形 2px 处** →
#    往里一路暗下去（57/32/19）→ 到箍内缘微升（31）＝木与铁之间那道暗缝。
#    ⚠️ 我上一版写的是 (8,9,11,15,24,58,92,30) —— "外缘近黑、亮棱在箍中段"，
#       那是照"我以为的铁箍剖面"编的，不是量的。方向就错在这：**亮棱的位置在外侧、不在中段**。
#       （10:2x 起第 2 档 96 由嵌线盖住，见 GOLD_INSET —— 铁轨上只留一道亮线。）
FRAME_L_EDGE = (0.0, 62.0, 96.0, 58.0, 33.0, 20.0, 31.0)
# 备选：亮铁缓坡（同 7 档，明暗连成一条斜面，铁的金属感更清楚）。成品不符就换这一行。
FRAME_L_BEVEL = (10.0, 30.0, 62.0, 74.0, 66.0, 46.0, 34.0)
FRAME_L = FRAME_L_EDGE       # 默认档（交付用这档；换 B 只改这一行）
FRAME_DIR = 0.14        # 顶亮底暗的幅度（整圈剖面乘 1±此值）——留一点受光方向，好让按下态翻过来

# 木面的**拱**（目标亮度，1× L）。原版实测 49→74→66：拱顶在 45% 高，上下都是暗的。
# 🔴 2026-09-14 11:4x **FACE_PEAK 74 → 82** —— 这是"暗沉"的**开关**。
#    木面的亮度全由这条拱形曲线决定（`material()` 只归一到 TARGET_WOOD=96 的平色底）。
#    ⚠️ 注意：`FACE_PEAK` 不是"木面均值"，它只是曲线里的**峰值系数**，实际落点还要乘
#       上下文（`budget_shade` 的 `arch * lat`）。所以别按"74 → 82 就是亮 8 级"算，
#       改完必须**回量**（`_probe_ref_target.py` 的 p50/p90），落到原版那一带才算数。
#    依据：原版木面 **亮半区 p50 = 69.0 / p90 = 73.0**，本项目只有 **60.3 / 68.0**
#          ⇒ "暗沉"就写在"亮半区从来没上去"这一条上。
FACE_PEAK = 82.0        # 拱顶亮度（原 74.0）
FACE_TOP = 0.66         # 拱上端 / 峰值
FACE_BOT = 0.89         # 拱下端 / 峰值（下端比上端亮：受光偏上，下半是缓收）
FACE_MID_T = 0.45       # 拱顶位置（占面高的比例）
FACE_EDGE_K = 0.22      # 面靠箍处再压暗多少（侧向收边，让箍"坐"进面里）
FACE_EDGE_D = 7.0       # 侧向收边作用的列数

# 九宫格：横向按钮宽度都是 Fixed，不会拉；纵向 Primary 在 NpcDialogue 里是
# StretchToParent（35→60），所以纵边距只需**刚好包住外框那一圈**。
# ⚠️ 2026-09-14 02:2x：**FRAME 4→8 之后，上下边距必须 ≥ FRAME+SEAM（9），否则铁箍会被拉花。**
# ⚠️ 2026-09-14 10:2x 端头铸件收到 15.5 深（NOSE_C 28→24）之后：**左右边距 ≥ 16**。
#    ⚠️ 我第一版把这里写成 37（错把 NOSE_C ＋ 半径 当纵深），是**算错不是取值保守**。
#    改 END_NOSE_C 必须重算这个值。
#    真正生效的九宫格边距写在 GUI/Brushes/AwakeBrushes.xml 的 <BrushLayer Extend*>（UI 线维护），
#    这里只是同值的备忘。**不要**另生成 NineRegionSprite sidecar ——项目口径是"只写 brush"，
#    两处各写一份等于埋一个静默的不一致。
NINE = dict(left=16, right=16, top=10, bottom=10)

GOLD_REF = (217, 169, 83)     # 项目既有实测金值 #D9A953
GOLD_DULL = (126, 106, 74)    # "克制"往这里收：偏灰的黄铜，不是亮金
GOLD_STEEL = (152, 146, 134)  # 更冷的一档（暖灰钢）——见 GOLD_TIERS 的 `steel`
# ⚠️ 2026-09-14 10:2x **嵌线不能再是一条等宽等亮的描边**。
#    "四周一圈同亮度的线"＝选区描边＝最典型的廉价特征（甲方：「成品看起来也很粗糙，给人一种廉价感」）。
#    原版放大 ×8 看：那道亮线**上缘近白、下缘几乎看不见**，是"被光打到的一段金属"，
#    而且它是**灰白的钢线、不是金线**。⇒ 两条改法：
#      · 加一道**受光斜坡** SEAM_LIT_BOT：上缘全亮、下缘只留 34%；
#      · 颜色往 GOLD_STEEL 掺（`steel` 档）＋ 整体压暗（gain 1.30 → 1.14）、多磨一点（wear 0.42 → 0.60）。
SEAM_LIT_BOT = 0.34           # 嵌线下缘保留多少亮度（1.0 ＝ 又回到"等亮描边"）
# ⚠️ 下面两个只是**材质的曝光量**（＝剖面图的基准），真正的亮度由 budget_shade() 的剖面接管。
#    2026-09-14 02:2x：木 32 → 96、铁 62 → 128。
#      · 木 32 是「黑得下去白才起得来」的过头产物：面黑到连自己的 ramp 都显示不出来（实测面 23~27），
#        读成"洞"而不是"材料"。原版面在 49~74 ⇒ 目标就是那个带。
#      · 两个值同时是**分母**：shade 图以它们为 1.0，乘完正好落在 FRAME_L / FACE_PEAK 上。
TARGET_WOOD = 96.0
TARGET_IRON = 128.0
WOOD_GRAIN = 0.13             # **微颗粒**强度。注意：只压微颗粒，不压木纹本身——
                              # 木纹是"结构"（见 WOOD_STRUCT），靠结构才看得出是木头。
IRON_GRAIN = 0.15             # 铁皮的微颗粒（同上：麻点是"脏"，不是"糙"）
IRON_GRAIN_DARK = 0.08        # 铁皮的暗侧再收一档——暗斑就是"脏"的主因
IRON_SAT = 0.40               # 铁皮去色：底图那层褐锈色一浮出来就"脏"。
                              # ⚠️ 2026-09-14 11:4x **0.34 → 0.40**，这是被自己的量打回来的：
                              #    我上一轮跟甲方说"铁改成偏亮的**银灰钢**"——**原版没有这回事**。
                              #    实测（`_probe_ref_zone.py` 按**行位置**切片，不用颜色阈值自证）：
                              #      原版铁 R-B = **+5.0**（略暖），我们 −0.3（中性灰）；
                              #      铁底图取块本身就是 **#454440 / R-B +4.7**（暖的）。
                              #    ⇒ 是这道去色（sat 0.34）把暖度抹平的，不是底图冷。
                              #      sat 0.34 → 0.40 后铁面 R-B 回到 +3.5，与 +5 同向。
                              #    ⛔ 别再往"冷灰钢"走 —— 那是"银器/现代五金"的读法，
                              #      原版这套 UI 里的铁是**旧铁略带锈气**的暖灰。
IRON_GRAIN = 0.15             # 铁皮的微颗粒（同上：麻点是"脏"，不是"糙"）
STRUCT_GAIN = 0.88            # 铁的**中尺度结构**（锻痕）保留多少。清晰度靠这个。
MICRO_F = 0.013               # "中尺度"与"微颗粒"的分界半径（相对交付宽）
# ⚠️ 2026-09-14 10:2x 木面三改（甲方：「成品看起来也很粗糙，给人一种廉价感」）：
#   旧值（sat 1.06 / structure 0.26）出来的面是**一块橘褐色的光面**——它亮、它滑、它饱和，
#   三样加起来就读成**塑料**，跟旁边那块灰铁摆一起像"贴上去的"。
#   · WOOD_STRUCT 0.26 → 0.45：那一层斑驳就是"老木头"，没了它面就是塑料。
WOOD_STRUCT = 0.45            # 木面的中尺度结构（斑驳／木纹）。**这是"是木头"的唯一证据。**

# 🔴 2026-09-14 11:4x **上面这条"低饱和＋加绿"的诊断是错的，这次纠回来** ——
#    甲方：「不好看。你的质感和颜色太暗沉了」。量下来病根**只有木面一条**：
#
#    按**亮度分位**把"色调"和"明暗"拆开量同一块木面（`_probe_ref_target.py`）：
#      亮度分位      原版     本项目
#      暗半区 p50    58.7     60.3      ← **一样**
#      亮半区 p50    69.0     60.3      ← 差 **9 级**
#      亮半区 p90    73.0     68.0      ← 差 5 级
#    ⇒ **"暗沉"不是整体压暗了，是木面从来没亮起来过。**
#
#    再看**色相**（R-B ＝ 红减蓝，越大越暖）：
#      原版木面 R-B **+26 ~ +32**，本项目只有 **+12 ~ +18**；
#      而且本项目木面 **R 与 G 完全相等**（`#484739` 即 R 68 / G 68）——
#      **"绿"就是这么来的**：R 被压下去、G 被提上来。
#      这是我上一轮的 `WOOD_TINT (0.90, 1.05, 0.94)` 干的，方向正好反了：
#      原版是**明黄褐**（R ≫ G > B），不是橄榄绿。
#    ⇒ 两条一起改（甲方 11:4x 选了"按原版对齐"）：
#      · WOOD_TINT 去掉"提绿"，改成 **G 略降、R 与 B 略提** ⇒ 往暖走；
#      · WOOD_SAT 0.55 → **0.62**：原版木面 S 34.9%，本项目 24.0%，我们其实是**欠**饱和；
#        （上一轮记的"饱和度是廉价感主犯"对**当时那块橘褐光面**成立，对现在这块不成立——
#          当时的病是"亮＋滑＋饱和"三样叠加读成塑料，不是饱和单独致病。）
WOOD_SAT = 0.62               # 木面饱和度。0.55 → 0.62（"欠饱和"是暗沉的一半）
WOOD_TINT = (1.04, 0.94, 0.97)  # 逐通道增益：橄榄褐 → **明黄褐**。**只动色相**，不动亮度。
                               # 期望落点：R-B ≈ +27 ~ +29（原版 +26 ~ +32）。


# ⚠️ 2026-09-14 02:2x 三条**大幅下调**（原来是「数码感」的三个来源）：
#   · IRON_LIFT 1.55 → 1.20、IRON_HI_MIX 0.28 → 0.14：铁只留一点冷灰**色相**，亮度不归它管了。
#   · SPEC_MIX 0.44 → 0.00：**删掉最上沿那条近白镜面带**。它把全图最亮顶到 188，
#     而原版最亮才 95 ⇒ 对比全花在「一条白线 vs 一块黑」上（极值对撞＝廉价）。
#     现在最亮是铁箍的**亮棱**（FRAME_L 第 6 档 122），**在箍的中段、不在外缘**。
IRON_GLOSS = 0.42             # （保留：只影响铁的色相与内部起伏，不影响亮度）
IRON_LIFT = 1.20
IRON_HI_TINT = (188, 192, 198)  # 钢的冷灰高光——和木面的暖调反着走
IRON_HI_MIX = 0.14
WOOD_SHADE = 0.0              # ⚠️ **停用**：木面的形影改由 FACE_PEAK 的拱形接管。
                              #   乘性暗角（原 0.24）和"一条目标曲线"会互相打架 ⇒ 只留一条机制。

# 镜面：**已停用**（SPEC_MIX=0）。留着常量是为了留痕：这是"数码感"的头号来源。
SPEC_TINT = (234, 238, 244)   # 近白（略带冷调）——实测：没有它时全图最亮只到 159
SPEC_MIX = 0.00               # ← 原来的 0.44。删掉它，最亮从 188 回到 122。
SPEC_BAND = 0.46              # 带高 ＝ 铁箍宽度 × 此比例（从受光沿往内）

# 那道嵌线的**强度档位**。⚠️ 这是调参用的档位，**不是文明身份**。
#
# 本项目 UI 是**通用控件**，只出一档（soft = 默认交付）。
# 曾经按"帝国＝满金／蛮族＝无金"分过三档 —— 已推翻，那是个**范畴错误**：
#   "华丽是帝国文明的、巴旦尼亚库赛特斯特吉亚不能称华丽" 是**内容口径**（描述世界里的文明），
#   控件是**玩家的界面**，玩家用同一个对话面板，跟他是哪国人无关。
#   内容美术（信件／旗帜／纹章＝"这是谁的"）才该按归属换皮。两件事别混。
# 工程上分档也是坏的：三档 × 四态 × 四个按钮族 ＝ 三倍的图和 brush，
#   而 Brushes 是静态 XML，根本没有"按玩家文明选 brush"的机制。
GOLD_TIERS = {
    "off":   dict(a=0.00, dull=0.00, wear=0.00, shadow=0,   gain=1.00, steel=0.00),
    "soft":  dict(a=0.92, dull=0.24, wear=0.60, shadow=96,  gain=1.14, steel=0.40),
    "full":  dict(a=1.00, dull=0.00, wear=0.00, shadow=130, gain=1.42, steel=0.10),
}

FONT = r"C:\Windows\Fonts\simkai.ttf"


# ---------------------------------------------------------------- 取材

def load_plaque(src):
    """从 AI 图里取出牌面：按亮度剖面找 bbox，再裁紧。"""
    im = Image.open(src).convert("RGB")
    g = im.convert("L")
    w, h = g.size
    px = g.load()

    def span(vals, thr):
        idx = [i for i, v in enumerate(vals) if v > thr]
        return (idx[0], idx[-1]) if idx else (0, len(vals) - 1)

    cols = [sum(px[x, y] for y in range(0, h, 4)) / (h / 4) for x in range(w)]
    rows = [sum(px[x, y] for x in range(0, w, 4)) / (w / 4) for y in range(h)]
    x0, x1 = span(cols, max(cols) * 0.30)
    y0, y1 = span(rows, max(rows) * 0.30)
    print("  牌面 bbox = (%d,%d)-(%d,%d)  %dx%d" % (x0, y0, x1, y1, x1 - x0, y1 - y0))
    return im.crop((x0, y0, x1 + 1, y1 + 1))


def material(plaque, box, size, target, grain=0.62, grain_dark=None,
             contrast=1.08, sat=1.06, blur_f=0.105, crop_px=None, structure=None,
             tint_gain=None):
    """按"中尺度结构 / 微颗粒"两层拆开取材质，再自适应曝光。

    四个关键点（全是踩出来的）：
      ① 取块要**按交付尺寸算**（crop_px ＝ 交付宽 × CROP_ZOOM），不能按源图的百分比。
         老版取源图 704px 贴到 110px 上 ＝ 6.4 倍压缩，木纹被压到亚像素 ⇒ 一片糊。
      ② 必须**压掉大尺度明暗**（AI 底图自带一道高光带和几块大斑），留着就永远不像控件。
      ③ 但**中尺度结构不能跟着一起压掉**——木纹走向、铁面的锻痕全在那一层，
         压掉就只剩"平色 ＋ 噪点"。**"看着不清晰"的真正原因就在这**：
         结构没了、颗粒还在。所以拆成 中尺度（STRUCT_GAIN 保留）／微颗粒（grain 压狠）。
      ④ 木可以糙，铁不能脏：铁一"脏"就是暗斑，暗斑缩到 1× 只有几个像素，读成霉点。
    """
    pw, ph = plaque.size
    if crop_px:
        cw, ch = min(pw, crop_px[0]), min(ph, crop_px[1])
        cx = (box[0] + box[2]) * 0.5 * pw
        cy = (box[1] + box[3]) * 0.5 * ph
        x0 = int(max(0, min(pw - cw, cx - cw * 0.5)))
        y0 = int(max(0, min(ph - ch, cy - ch * 0.5)))
    else:
        x0, y0 = int(pw * box[0]), int(ph * box[1])
        cw, ch = int(pw * box[2]) - x0, int(ph * box[3]) - y0
    c = plaque.crop((x0, y0, x0 + cw, y0 + ch))
    m = c.resize(size, Image.LANCZOS)

    gd = grain if grain_dark is None else grain_dark

    def adj(v):
        d = v - 128
        return max(0, min(255, int(128 + d * (grain if d >= 0 else gd))))

    lo = m.filter(ImageFilter.GaussianBlur(radius=max(1.0, size[0] * blur_f)))
    hi = m.filter(ImageFilter.GaussianBlur(radius=max(0.6, size[0] * MICRO_F)))
    micro = ImageChops.subtract(m, hi, 1.0, 128).point(adj)
    sg = STRUCT_GAIN if structure is None else structure
    struct = ImageChops.subtract(hi, lo, 1.0, 128).point(
        lambda v: max(0, min(255, int(128 + (v - 128) * sg))))

    tint = ImageStat.Stat(lo).mean[:3]                        # 基准色（大尺度照明已抹掉）
    # ⚠️ 2026-09-14 02:2x 改法：**把基准色直接归一到 target**，而不是"先压暗 0.42、再靠 enhance 提回来"。
    #    旧写法里 enhance 的 k 被 `max(0.70, min(2.60, ...))` 钳死（木需要 4.3×、铁需要 4.5×），
    #    结果是**静默地停在 2.60**：日志照样打「→96」，实测只有 58 —— 剖面整个人被拉低。
    #    而且旧写法会把 struct/micro **跟着放大 k 倍**（噪声同步放大）。
    #    新写法只把**平色底**拉到 target，结构层保持原幅度 ⇒ 面更安静（本来就该安静）。
    tl = 0.2126 * tint[0] + 0.7152 * tint[1] + 0.0722 * tint[2]
    kt = max(0.60, min(6.00, target / max(tl, 1.0)))
    flat = Image.new("RGB", size, tuple(min(255, int(c * kt)) for c in tint))
    m = ImageChops.add(flat, struct, 1.0, -128)               # 平色底 + 中尺度结构
    m = ImageChops.add(m, micro, 1.0, -128)                   # ＋ 微颗粒

    mean = ImageStat.Stat(m.convert("L")).mean[0]
    k = max(0.70, min(2.60, target / max(mean, 1.0)))
    m = ImageEnhance.Brightness(m).enhance(k)
    m = ImageEnhance.Contrast(m).enhance(contrast)
    m = ImageEnhance.Color(m).enhance(sat)
    if tint_gain:
        # 逐通道增益：**只动色相**，不动亮度与结构（亮度归 target 与结构层管）。
        # ⚠️ 局部名**不要**叫 `ch` —— 上面 `ch` 是裁块高度，下面那行日志要打它。
        #    踩过一次：`ch = list(m.split())` 把高度覆盖成通道列表 ⇒ 日志 `TypeError: %d ... not list`
        #    （错得很响，比静默好；但也不该写出来）。
        chs = list(m.split())
        for i, g in enumerate(tint_gain):
            chs[i] = chs[i].point(lambda v, g=g: min(255, int(round(v * g))))
        m = Image.merge("RGB", chs)
    print("    取块 %dx%d → %dx%d（净 %.2f×）｜基准色 %s ×%.2f ⇒ 平色底；微调 %.2f×｜实测均值 %.1f（目标 %.0f）"
          % (cw, ch, size[0], size[1], size[0] / float(cw),
             "#%02X%02X%02X" % tuple(int(c) for c in tint[:3]), kt, k, mean, target))
    return m


def sample_gold(plaque):
    """金值取实拍暖色像素的**中位带**（去掉镜面高光与暗角），再与项目既有实测金值融合。"""
    px = plaque.load()
    w, h = plaque.size
    cand = []
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if r > 70 and r > g + 8 and g > b + 5:
                cand.append((r + g, (r, g, b)))
    cand.sort(key=lambda t: -t[0])
    if not cand:
        mid = GOLD_REF
    else:
        band = cand[len(cand) // 3: max(len(cand) // 3 + 1, len(cand) * 2 // 3)]
        mid = tuple(int(sum(c[1][i] for c in band) / len(band)) for i in range(3))
    gold = tuple(int(mid[i] * 0.35 + GOLD_REF[i] * 0.65) for i in range(3))
    print("  实拍中位金 #%02X%02X%02X -> 采用 #%02X%02X%02X" % (mid + gold))
    return gold


# ---------------------------------------------------------------- 骨

def value_noise(size, cells, seed):
    rnd = random.Random(seed)
    cw, ch = cells
    grid = [[rnd.random() for _ in range(cw + 1)] for _ in range(ch + 1)]
    w, h = size
    out = Image.new("L", size, 0)
    p = out.load()
    for y in range(h):
        fy = y / (h - 1) * ch
        y0 = min(int(fy), ch - 1)
        ty = fy - y0
        ty = ty * ty * (3 - 2 * ty)
        for x in range(w):
            fx = x / (w - 1) * cw
            x0 = min(int(fx), cw - 1)
            tx = fx - x0
            tx = tx * tx * (3 - 2 * tx)
            v = (grid[y0][x0] * (1 - tx) + grid[y0][x0 + 1] * tx) * (1 - ty) + \
                (grid[y0 + 1][x0] * (1 - tx) + grid[y0 + 1][x0 + 1] * tx) * ty
            p[x, y] = int(max(0.0, min(1.0, v)) * 255)
    return out


def rounded_ring(size, inset, thickness, radius):
    """圆角矩形描边（向内生长），返回 L 掩膜。inset=0 即最外一圈。"""
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle(
        (inset, inset, size[0] - 1 - inset, size[1] - 1 - inset),
        radius=radius, outline=255, width=max(1, int(round(thickness))))
    return m


def end_taper_points(w, h, tip_len, tip_h):
    """**整条按钮轮廓**（1× 连续坐标，0..w / 0..h）。四向严格镜像。

    ## 2026-09-14 12:3x 定案：改用与次按钮/页签**同一条曲线**

    甲方定案：本族三控件**完全同一轮廓，只改长度**；端头长度**跟绝对 16px**。
    曲线定义在 `curve_shape`（`CURVE_D0=0.10` / `CURVE_POW=1.12`），端点钝口。

    ## 被替掉的旧做法（二次贝塞尔「半圆头」）

    旧版用 8 段二次贝塞尔，控制点落在形角上 ⇒ 端头向外鼓成**近半圆**。
    逐列高实测（`out/study/shape/primary_tip_probe.txt`，110×35、端头 16px）：

        旧（贝塞尔半圆） x=0..12:  13 19 23 27 29 29 31 31 33 33 33 33 35
        新（曲线）       x=0..15:   5  7  9 11 13 15 17 19 21 23 25 27 29 31 33 35

    ⚠️ **旧的那条正是"线条不流畅"的数值形态**：前 4 列就吃掉 83% 的高度
       （13→29），然后 8 列几乎不动（29→35）——**一段大陡坡接一段死平**。
       新的是严格 +2 等差，**与原版那条阶梯同性格**（原版前五列也是 +2）。

    ## 关于原版那条到底长什么样（`_fit4.py` 全 14 列拟合）

    原版（满高 42、端头 14 列）逐列增量是
        +2 +2 +2 +2 [ +4 +2 +4 +4 +4 ] +2 +2 +2 +2 +2
    ⇒ 它是「**缓—陡—缓**」三段式（S 形性格），**本文件用的单参数幂族
      `d = d0 + (1-d0)(1-(1-t)^p)` 在数学上表达不了它**（p<1 凸、p>1 凹，都是单调偏一侧）。

    但**缩到我们这个尺寸后这点差别看不见**：把原版 14 列映射到我们的 16px 端头、
    逐 1px 列比（`_probe_scale.py`），`p=1.12` 与原版映射的**最大差 1.5px、
    绝大多数列在 ±1px 内**；而 p 取 1.40/1.70 会在中段（x=4~7）**反而超出** 1.5~2px。
    ⇒ **保持 p=1.12**，那 1px 残差是分辨率极限、不是缺陷。
    ⚠️ 教训：**先确认"这点差别在这个尺寸下看不看得见"，再决定要不要换模型。**
       不然就会为了 1px 去换一整套曲线族，白做一轮。

    ## ⚠️ 坐标系

    本函数返回 **0..w / 0..h 的连续坐标**（`poly_mask` 会乘 `ss`），
    而 `artkit.chamfer_pts` 走的是 **0..w-1 的像素格**。两套不一样，别混。
    `curve_shape.tip_curve_pts` 属后者 ⇒ 这里要把它**映射**过来（见下 `_map`）。
    """
    cy = h * 0.5
    th = max(0.5, tip_h * h * 0.5)
    tl = max(0.0, min(tip_len, w * 0.5 - 1.0))
    if tl <= 0.0:
        # 退化档（TIP_LEN=0）：四个角，别让它退化成"两条共线的竖边"——
        # 那样 offset_poly 会撞上平行线、整块形返回空（症状：按钮整个不见了）。
        return [(0.0, 0.0), (float(w), 0.0), (float(w), float(h)), (0.0, float(h))]

    # 曲线在"像素格"语义下（0..w-1）算，再映射回"连续"语义（0..w）。
    #   x_pixel = x_cont * (w-1)/w ；y 同理。端点钝口由 CURVE_D0 决定。
    pts_px = _CS.tip_curve_pts(w - 1, h - 1, tl)
    kx = float(w) / (w - 1)
    ky = float(h) / (h - 1)
    return [(x * kx, y * ky) for x, y in pts_px]


def poly_mask(pts, size, ss):
    """多边形填充掩膜（在 SS 网格上画 ⇒ 缩回 1× 自带抗锯齿）。空多边形返回全 0。"""
    m = Image.new("L", size, 0)
    if len(pts) >= 3:
        ImageDraw.Draw(m).polygon([(x * ss, y * ss) for x, y in pts], fill=255)
    return m


def _shoelace(pts):
    a = 0.0
    n = len(pts)
    for i in range(n):
        x0, y0 = pts[i]
        x1, y1 = pts[(i + 1) % n]
        a += x0 * y1 - x1 * y0
    return a * 0.5


def offset_poly(pts, d):
    """凸多边形**向内**偏移 d（每条边沿内法线平移，再求相邻两条偏移线的交点）。

    向内那一侧靠**面积变小**判定 —— 不猜法线方向（顶点序写反了就会往外长）。

    ⚠️ 2026-09-14 10:2x 加了"**共线接头**"的兜底，这是曲线收尖必须的：
       曲线与它相邻的直边**相切**（切线连续）时，两条边的法线完全相同 ⇒ 行列式恰好为 0，
       交点根本不存在。原来的写法一遇到就 `return []`，而调用方 `band_field` 会把
       "空"理解成"这一层没有" ⇒ **整条深度场塌成 0** ⇒ 整圈铁变成纯黑
       （症状：整个边框不见了，只有木面还在；日志一切正常）。
       兜底做法：共线时把**原始顶点**沿该边的法线平移 d —— 这正是"偏移一个共线接头"的定义。
    """
    if d <= 0:
        return list(pts)
    # 先去重（相邻重复点 → 零长边）：零长边的法线不存在，留着就是除零/退化。
    # 这是**输入卫生**，不是兜底 —— 生成侧的重复点该在生成侧修掉。
    clean = [pts[0]]
    for p in pts[1:]:
        if abs(p[0] - clean[-1][0]) > 1e-9 or abs(p[1] - clean[-1][1]) > 1e-9:
            clean.append(p)
    if len(clean) > 1 and (abs(clean[-1][0] - clean[0][0]) < 1e-9
                           and abs(clean[-1][1] - clean[0][1]) < 1e-9):
        clean.pop()
    pts = clean
    if len(pts) < 3:
        return []
    for nx, ny in ((1, -1), (-1, 1)):          # 两种法线方向，取面积更小的那个
        lines, ok = [], True
        n = len(pts)
        for i in range(n):
            x0, y0 = pts[i]
            x1, y1 = pts[(i + 1) % n]
            dx, dy = x1 - x0, y1 - y0
            L = math.hypot(dx, dy)
            if L < 1e-9:
                ok = False
                break
            # (a,b) 是**单位**法线 ⇒ 下面用点积改写直线方程时不必再除 a²+b²
            a, b = nx * dy / L, ny * dx / L
            lines.append((a, b, a * x0 + b * y0 + d))
        if not ok:
            continue
        out = []
        for i in range(n):
            a1, b1, c1 = lines[i - 1]
            a2, b2, c2 = lines[i]
            det = a1 * b2 - a2 * b1
            if abs(det) < 1e-9:                 # 共线接头：交点不存在，取顶点在偏移线上的投影
                px, py = pts[i]
                t = c2 - (a2 * px + b2 * py)
                out.append((px + a2 * t, py + b2 * t))
                continue
            out.append(((c1 * b2 - b1 * c2) / det, (a1 * c2 - c1 * a2) / det))
        if _shoelace(out) < _shoelace(pts):     # 变小了 ⇒ 方向对
            return out
    return []


def band_field(pts, size, ss, depth_px):
    """每个像素的**离边深度**（1× 像素，连续量化的整数层），上限 depth_px。

    ⚠️ 换成"逐层内缩多边形"而不是 Chebyshev 距离场：斜边（就是收尖那两段）上
       Chebyshev 会明显偏大 ⇒ 剖面在端头被压扁。逐层内缩量的是**真正的垂距**。
    """
    field = Image.new("L", size, depth_px)
    prev = poly_mask(pts, size, ss)
    for i in range(depth_px):
        inner = offset_poly(pts, (i + 1) / float(ss))
        nxt = poly_mask(inner, size, ss) if inner else Image.new("L", size, 0)
        field.paste(i, (0, 0), ImageChops.subtract(prev, nxt))
        prev = nxt
    return field


def band_out(mask, ss, layers):
    """从 `mask` 的边界**向外**的层号（1× 像素）。层 i 的像素值 ＝ i；mask 内 ＝ 0。

    用 MaxFilter(2·ss+1) 每层长 ss 个超采样像素 ＝ 正好 1 个 1× 像素。
    ⚠️ 与 MinFilter 那次的坑不同：这里是**往外长**，不会在图像边界被钳位（0 区无所谓）。
    """
    field = Image.new("L", mask.size, 0)
    prev = mask
    for i in range(1, layers + 1):
        nxt = prev.filter(ImageFilter.MaxFilter(2 * ss + 1))
        field.paste(i, (0, 0), ImageChops.subtract(nxt, prev))
        prev = nxt
    return field


def wood_stadium(w, h, rail, nose_c, ss):
    """木面：两端**半圆头（拱）**的水平条 —— 圆角半径取到最大值即得半圆头。

    这是端头铸件成立的前提：铸件的内缘轮廓**就是**木面的外缘。
    ⚠️ `w`/`h`/`rail`/`nose_c` **一律是 1× 像素**，只在函数内部乘 `ss`。
       踩过：外面传进来的 `hh` 已经是 SS 尺度，再在这里乘一次 ⇒ 半径变成 15.25 而不是 8.5，
       木面把整条边框吃掉 ⇒ 端头铸件渲染成了一块木头（看图才发现，日志一切正常）。
    """
    r = h * 0.5 - rail                                     # 1×
    m = Image.new("L", (w * ss, h * ss), 0)
    ImageDraw.Draw(m).rounded_rectangle(
        ((nose_c - r) * ss, rail * ss, (w - nose_c + r) * ss, (h - rail) * ss - 1),
        radius=r * ss, fill=255)
    return m


def hole_masks(w, h, mode, hole_x, hole_r, off, ss):
    """端头铸件上的**圆孔**掩膜（两端镜像；pair 档再上下镜像）。返回 (孔, 孔缘亮圈)。

    ⚠️ `w`/`h`/`hole_x`/`hole_r`/`off` **一律 1×**，只在建布时乘一次 `ss`。
       踩过（和 wood_stadium 同一个坑）：外面传进来的 `hh` 已经是 SS 尺度，
       这里又乘一次 ⇒ 孔心落到 y=254（图高才 140）⇒ 孔在画布外，
       **日志一切正常、图上也看不出错**（只是"那个细节没有了"）。
    """
    if mode == "none":
        z = Image.new("L", (w * ss, h * ss), 0)
        return z, z
    cy = (h - 1) * 0.5
    ys = [cy] if mode == "axis" else [cy - off, cy + off]
    xs = [hole_x, (w - 1) - hole_x]
    core = Image.new("L", (w * ss, h * ss), 0)
    d = ImageDraw.Draw(core)
    rr = hole_r * ss
    for cx in xs:
        for yy in ys:
            px, py = cx * ss, yy * ss
            d.ellipse((px - rr, py - rr, px + rr, py + rr), fill=255)
    core = core.filter(ImageFilter.GaussianBlur(max(0.4, END_HOLE_SOFT * ss)))
    hard = core.point(lambda v: 255 if v > 128 else 0)
    rim = ImageChops.subtract(band_out(hard, ss, 1).point(lambda v: 255 if v else 0), hard)
    rim = rim.filter(ImageFilter.GaussianBlur(max(0.4, END_HOLE_SOFT * ss * 0.6)))
    return core, rim


def budget_shade(size, ss, dist, frame_mask, wood_mask, seam_mask, frame_l=None,
                 d_out=None, hole=None, hole_rim=None, end_zone=None,
                 rail_by_dout=False):
    """**亮度剖面图**（L）—— 本轮返工的核心机制。

    把原来分散在四处的东西（材质曝光／上亮下暗的线性 ramp／最上沿镜面带／倒角线）
    收成**一条机制**：给定「这是哪一块 ＋ 离哪条边几像素」，读出一个亮度。

      · 长边铁轨：暗轮廓 → 渐亮 → **亮棱（在轨的中段，不在外缘）** → 内缘回落
      · 端头铸件：**不按"离外形几像素"走**（见下 ⚠️），而是按**离木面的拱几像素**走：
        拱内缘一道**亮棱 END_RIM** → 落到体色 END_LIFT → 外形边缘 3px 内压成暗轮廓
      · 圆孔：孔底压暗 ＋ 孔缘亮圈（**软边** —— 硬边圆孔读成"钻头打的"）
      · 木面：拱形 49 → 74（45% 高）→ 66，并向两侧收边（FACE_EDGE_K）
      · 嵌线那一行不动（shade = 255 ⇒ 乘性恒等），金线自己有色

    ⚠️ **必须显式拿 `frame_mask` / `wood_mask`**：端头铸件把框撑厚之后，
       "离边 8px 以外＝木面"这个暗含假设不成立（铸件里离边 15px 的地方还是铁）。
    ⚠️ **端头铸件更不能拿 `dist`（离外形距离）当坐标** —— 这是本轮最贵的那个坑：
       八边形收尖把两端的外形**收窄**了，于是铸件中心离外形的距离也一路上不去（实测只有 5.2px），
       整块铸件全落在**铁轨那 8px 的剖面**里 ⇒ 体色 96 从来没生效，实测 60.6。
       （第一版更黑：END_LIFT 72 同样没生效，实测还是走铁轨剖面。）
       治本＝端头区**换成 `d_out`（离木面的拱向外几像素）**当坐标。
    """
    fl = FRAME_L if frame_l is None else frame_l
    ww, hh = size
    f = FRAME * ss
    band = (FRAME + SEAM) * ss
    n = len(fl)
    out = Image.new("L", size, 0)
    op, dp, sp = out.load(), dist.load(), seam_mask.load()
    fp, wp = frame_mask.load(), wood_mask.load()
    op_out = (d_out or Image.new("L", size, 0)).load()
    hp = (hole or Image.new("L", size, 0)).load()
    rp = (hole_rim or Image.new("L", size, 0)).load()
    ez = (end_zone or Image.new("L", size, 0)).load()
    rim_w = max(0.5, END_RIM_W)          # d_out 的层号就是 1× 像素，**不要再乘 ss**
    gro_d = END_GROOVE_D                 # 同上，d_out 已是 1× 像素
    gro_h = max(0.5, END_GROOVE_W * 0.5)
    outer_d = max(1.0, END_OUTER_D * ss)  # 外形边缘那道暗轮廓的过渡宽度（SS 像素）
    for y in range(hh):
        t = (y - band) / float(max(1.0, hh - 1 - 2 * band))
        t = 0.0 if t < 0.0 else (1.0 if t > 1.0 else t)
        if t <= FACE_MID_T:
            arch = FACE_TOP + (1.0 - FACE_TOP) * (t / FACE_MID_T)
        else:
            arch = 1.0 - (1.0 - FACE_BOT) * ((t - FACE_MID_T) / (1.0 - FACE_MID_T))
        dirf = 1.0 + FRAME_DIR * (1.0 - 2.0 * y / float(hh - 1))
        for x in range(ww):
            if sp[x, y]:
                op[x, y] = 255
                continue
            d = dp[x, y]
            if fp[x, y]:                                   # —— 铁 ——
                if ez[x, y]:                               # 端头铸件：坐标是 d_out
                    v = END_LIFT
                    if END_RIM > 0:
                        v = max(v, END_RIM * max(0.0, 1.0 - op_out[x, y] / rim_w))
                    ov = fl[0] + (END_LIFT - fl[0]) * min(1.0, d / outer_d)
                    v = min(v, ov)
                    # 暗槽：把铸件劈成"外圈线脚 ＋ 内圈凸台"——**读成线，不读成块**。
                    if END_GROOVE_V > 0:
                        gd = op_out[x, y] - gro_d
                        if -gro_h < gd < gro_h:
                            v = min(v, END_GROOVE_V + (END_LIFT - END_GROOVE_V)
                                    * abs(gd) / gro_h)
                else:                                      # 长边铁轨
                    # 🔴 2026-09-14 12:4x **坐标从 `d` 改成 `d_out`（无铸件档）。**
                    #    `d = dist`（离**外形**的垂距）在曲线端头处会塌陷 ——
                    #    端头曲率大，等距内缩线向曲率中心收得更快，`d` 涨不上去，
                    #    于是端头那一段铁轨查到的层号全挤在最外几档 ⇒ 剖面错乱。
                    #    （与"铁轨宽度骤降"是同一个几何根因，见 `_probe_frame_def.py`。）
                    #    铁轨现在由**木面反推**得到（`frame = shape − 木面 − 嵌线`），
                    #    所以它的自然坐标就是 `d_out`（离木面向外 0→FRAME）。
                    #    ⚠️ `FRAME_L` 表的索引 0 是**最外缘**、最后一项是**箍内缘**，
                    #       而 `d_out` 是 0=木面边 ⇒ **必须反着读**。
                    if rail_by_dout:
                        de = op_out[x, y]
                        dd = f if de * ss > f else de * ss
                        dd = f - dd                      # 反读：d_out 0 → 表尾（箍内缘）
                    else:
                        dd = d if d < f else f
                    # ⚠️ 这里**不能**写 `elif d < f:` 再配一个 else —— 轨的掩膜是按**木面的边界**
                    #    切的，而木面边界在 SS 上是第 32 行，比 d=f=28 多出 4 个 SS 像素。
                    #    于是"d 恰好 = f"那一圈会掉进 else，拿到端头件的体色 ⇒ **一圈假亮线**。
                    #    （实测那一行 92，本该 31。）⇒ 直接夹住，不留 else。
                    #    （夹取已在上面按 rail_by_dout 分支里做过，这里不再重复。）
                    u = dd / f * (n - 1)
                    i = int(u)
                    v = (fl[n - 1] if i >= n - 1
                         else fl[i] + (fl[i + 1] - fl[i]) * (u - i))
                    # 🔴 下沿亮棱（见 RAIL_EDGE 的文件头）。
                    #    ⚠️ 判定**不能**靠 `d`（`d` 在下沿会一路涨到 f=28，早不是 2 了）：
                    #       铁轨的坐标是"离**最近**外形几像素"，下沿那一带的 d 由**下边**提供，
                    #       但剖面表 `FRAME_L` 是按**上沿的顺序**排的 ⇒ 下沿必须**反着读**。
                    #       实测（`AWAKE_DBG`）：1× y=28 处 d=28 ⇒ 查表落 `fl[6]=31`（最暗档），
                    #       再乘 `dirf` 压一档 ⇒ 就是那串 28/26/21。
                    #    ⇒ 正确做法：下半区把 `dd` **镜像回上沿的坐标**再查表，这样
                    #       "外缘黑 → 中调 → 亮棱 → 内缘回落"整条剖面**两侧都有**，
                    #       与上游 `d_t`（工作区里量到 0:0,1:1,…,5:4 **完全对称**）自洽。
                    if y > hh // 2 and not rail_by_dout:
                        dd = f - dd
                        u = dd / f * (n - 1)
                        i = int(u)
                        v = (fl[n - 1] if i >= n - 1
                             else fl[i] + (fl[i + 1] - fl[i]) * (u - i))
                    #     ⚠️ `d_out` 模式**不镜像** —— `d_out` 的定义就是"离木面向外的层号"，
                    #        上下沿天然同号同值，镜像反而会把剖面翻过去。
                    if RAIL_EDGE and y > hh * 0.88:
                        _de = (op_out[x, y] * ss if rail_by_dout else dd)
                        e = RAIL_EDGE_L * RAIL_EDGE_DIR * max(
                            0.0, 1.0 - abs(_de / ss - RAIL_EDGE_D) / RAIL_EDGE_W)
                        if e > v:
                            v = e
                rel = v * dirf / TARGET_IRON
                if hp[x, y]:                               # 圆孔（在铁上，故接在铁之后）
                    hd = hp[x, y] / 255.0
                    rel = rel * (1.0 - hd) + (END_HOLE_DARK * dirf / TARGET_IRON) * hd
                if rp[x, y]:
                    rel = max(rel, (END_HOLE_RIM * dirf / TARGET_IRON) * (rp[x, y] / 255.0))
            elif wp[x, y]:                                 # —— 木 ——
                dx = min(x - band, (ww - 1 - band) - x)
                lat = 1.0 - FACE_EDGE_K * max(0.0, 1.0 - max(0.0, dx) / (FACE_EDGE_D * ss))
                rel = FACE_PEAK * arch * lat / TARGET_WOOD
            else:
                op[x, y] = 0
                continue
            op[x, y] = max(0, min(255, int(round(255.0 * rel))))
    return out


def corner_brackets(size, side, radius):
    """四角各一块方形包铁角件——铁包木的做法，也让四角从形上立出来。"""
    m = Image.new("L", size, 0)
    d = ImageDraw.Draw(m)
    s, w, h = int(round(side)), size[0], size[1]
    for x0, y0 in ((0, 0), (w - s, 0), (0, h - s), (w - s, h - s)):
        d.rounded_rectangle((x0, y0, x0 + s - 1, y0 + s - 1),
                            radius=radius, fill=255)
    return m


# ---------------------------------------------------------------- 五金细节（四重镜像 · 软起伏）
#
# 「四个角，包括上下左右四边都可以用铆钉或者尖刺做一点细节，保持对称，但不单调」
#
# ⛔ **语汇红线（2026-09-14 01:5x，甲方：「你这钉子的像素…看着不像骑砍的，倒向 minecraft 的」）**
#    我先做的是「实心亮块 ＋ 硬边 ＋ 等距重复」——那是**体素语言**，不是骑砍的。
#    回一手源核过（`_ref_button_hardware.py`，读原版 `General/Button/main_button_regular` 271×84）：
#      · **原版一个亮块铆钉都没有**。它的细节全在 **①轮廓剖面 ②值域起伏**（暗一点／亮一点）。
#      · 原版的边是**软的**（抗锯齿的圆滑剖面），不是硬边几何填充。
#      · 原版按高度缩到 35（113×35，与本项目同尺寸）之后，小结构**主动糊成一段渐变**，
#        不是硬撑成一个方块 —— 那是「画大的、缩下来」的手感。
#    ⇒ 所以五金必须是**对局部像素做值域起伏**（底下材质纹理还在），
#      而且**边要软**（掩膜过一道高斯）。硬边 ＋ 纯色 ＋ 等距重复 ⇒ 立刻变体素。
#
# 三条自我约束（沿用）：
#   ① **镜像**：点位成对（x ↔ w-x、y ↔ h-y）＋ 掩膜取四向并集，对称是算出来的。
#   ② **不动形**：细节全落在**外框那一条**上，轮廓仍是严格矩形。
#   ③ **低对比**：一排看是"珠饰"，凑近才读成五金。
#
# 两条档（2026-09-14 02:2x 起 **默认＝none**）：
#   none ＝ 不画五金。**甲方原话**：「你这个铆钉看着就是一坨鼻涕滴在上面」。
#          回一手源也站得住：原版 `main_button_regular` **一个亮块铆钉都没有** ——
#          它的信息量全在 ①轮廓（两端收尖）②值域起伏（剖面）。
#   studs ＝ 四角大一枚 ＋ 四边各两枚（保留档，供对照；收尖之后点位会落到形外，用前要重排）
#   spikes＝ 内缘软锯齿一圈（保留档）
DETAIL = "none"         # none | studs（软凸起）| spikes（内缘软锯齿）
STUD_CORNER_R = 2.2     # 角钉凸起半径（1× 像素）。**圆**不是菱形——菱形在 1× 下是个"十字"。
STUD_EDGE_R = 1.4       # 边钉凸起半径
STUD_RING = 0.8         # 落影圈往外扩多少（凸起的暗缘）
STUD_HILITE = 0.5       # 迎光心**上移**多少像素 —— 这一移才是"圆包"（上亮下暗），不移就是"贴片"
STUD_SOFT = 0.5         # 掩膜高斯半径（1× 像素）。**这一条就是"不像 minecraft"的关键**：硬边＝体素。
STUD_LIGHT = (152, 146, 136)    # 迎光面：往这个暖灰**混**（不是填实色，纹理还在）
STUD_LIGHT_A = 0.38             # ⚠️ 0.60 → 0.38：箍压暗之后（亮棱 100），原来的 193/0.60 太跳，
                                #    读成"贴上去的白扣子"。铆钉的活是"珠饰"，不是"高光点"。
STUD_DARK = (18, 15, 13)        # 落影圈：往暗处混
STUD_DARK_A = 0.58
SPIKE_LEN = 3.0
SPIKE_W = 3.2
SPIKE_DARK = (24, 20, 18)
SPIKE_DARK_A = 0.52
SPIKE_METAL = (196, 190, 182)
SPIKE_METAL_A = 0.50

# ---- 点位（**镜像一致**是算出来的，不是看出来的）----
# ⚠️ 偶数宽度上 `w - x` **不是**镜像：110 的镜像轴在 x = 54.5（落在 54 与 55 之间），
#    x 的镜像是 (w-1) - x。曾经用 `w - fx` 算角点 ⇒ 左右差 1px，镜像差从 6.11 涨到 6.49。
#    所以这里改成"以镜像轴 c 为基准取成对偏移"，并且偏移取 .5 结尾 ——
#    这样两侧算出来都落在**整数像素**上（例：54.5 ∓ 17.5 ⇒ 37 与 72）。
# 35 是奇数，镜像轴 17.0 正好落在像素 17 上，所以短边可以只有正中一枚。
# 节奏（**克制**：原版一个都没有，这里只在角与四边中段点几个）：
#   长边：角(2) · 37 · 72 · 角(107) —— 间距 35/35/35，匀
#   短边：角(2) · 17 · 角(32)       —— 间距 15/15，匀
CORNER_OFF_X = 50.5            # ⇒ x = 4 / 105（铁箍带正中：FRAME 8 ⇒ 中心在 4）
CORNER_OFF_Y = 13.0            # ⇒ y = 4 / 30
EDGE_OFF_X = (17.5,)           # 长边 ⇒ x = 37 / 72
RING_OFF_X = (45.5, 27.5, 9.5)  # 软锯齿一圈 ⇒ x = 9 / 100 / 27 / 82 / 45 / 64
RING_OFF_Y = (8.0,)            # 软锯齿一圈 ⇒ y = 9 / 25


def _sym(c, offs):
    """以 c 为镜像轴，按偏移量取**成对**位置。返回顺序：先各对的下侧、再上侧。"""
    out = []
    for o in offs:
        out += [c - o, c + o]
    return out


def corner_points(w, h, frame):
    """四角各一枚，落点在**外框带的正中**（fx = FRAME/2）。四重镜像。"""
    fx = frame * 0.5
    xs = _sym((w - 1) * 0.5, (CORNER_OFF_X,))
    ys = _sym((h - 1) * 0.5, (CORNER_OFF_Y,))
    return [(x, y) for y in ys for x in xs]


def edge_points(w, h, frame):
    """四边上的点位（**不含角**）：长边两枚（x = 37／72）；短边只有纵向中点一枚。
    返回 (x, y, side)——side 决定锯齿朝哪扎。"""
    fx, fy = frame * 0.5, frame * 0.5
    pts = []
    for x in _sym((w - 1) * 0.5, EDGE_OFF_X):
        pts.append((x, fy, "top"))
        pts.append((x, (h - 1) - fy, "bottom"))
    cy = (h - 1) * 0.5
    pts.append((fx, cy, "left"))
    pts.append(((w - 1) - fx, cy, "right"))
    return pts


def _discs(mask, pts, r, ss):
    """**圆**盘（不是菱形）。1× 下菱形缩出来是个"十字"，读成星星，是像素画的雷。"""
    d = ImageDraw.Draw(mask)
    for cx, cy in pts:
        px, py, rr = cx * ss, cy * ss, r * ss
        d.ellipse((px - rr, py - rr, px + rr, py + rr), fill=255)


def _spike(mask, along, side, w, h, length, width, ss):
    """一枚齿：底边压在**外框内缘**，尖朝面板里扎。
    along ＝ 沿那条边的位置（top/bottom 用 x，left/right 用 y）。
    ⚠️ 内缘要用 (w-1)-FRAME / (h-1)-FRAME —— 直接用 w-FRAME 会差 1px，就又不镜像了。"""
    hw = width * 0.5
    if side == "top":
        tri = [(along - hw, FRAME), (along + hw, FRAME), (along, FRAME + length)]
    elif side == "bottom":
        yb = (h - 1) - FRAME
        tri = [(along - hw, yb), (along + hw, yb), (along, yb - length)]
    elif side == "left":
        tri = [(FRAME, along - hw), (FRAME, along + hw), (FRAME + length, along)]
    else:                                   # right
        xb = (w - 1) - FRAME
        tri = [(xb, along - hw), (xb, along + hw), (xb - length, along)]
    ImageDraw.Draw(mask).polygon([(x * ss, y * ss) for x, y in tri], fill=255)


def _spike_ring(mask, w, h, ss, length, width):
    """内缘软锯齿一圈（spikes 档）：长边各 6 枚、短边各 2 枚，全部按镜像轴成对。"""
    for x in _sym((w - 1) * 0.5, RING_OFF_X):
        _spike(mask, x, "top", w, h, length, width, ss)
        _spike(mask, x, "bottom", w, h, length, width, ss)
    for y in _sym((h - 1) * 0.5, RING_OFF_Y):
        _spike(mask, y, "left", w, h, length, width, ss)
        _spike(mask, y, "right", w, h, length, width, ss)


def _symmetrize(mask):
    """把掩膜**强制**取成左右＋上下镜像的并集（四向并集）。
    点位已经按镜像轴算过了，但栅格化还有半个像素的取整余量：
    实测 studs 档的左右镜像差从 6.11 涨到 6.50——就是这点余量。
    控件要的是"看着对称"，那就**别把这件事交给图形库的取整规则**。"""
    m = ImageChops.lighter(mask, ImageOps.mirror(mask))
    return ImageChops.lighter(m, ImageOps.flip(m))


def _symmetrize_x(mask):
    """只取**左右**镜像并集。
    ⚠️ 迎光心**不能**走上下镜像：光本来上亮下暗，把掩膜上下对称化，
    等于把"圆包"碾成"贴片"。**上下不是形状上的对称、是光**；左右才是必须严格镜像的形状。"""
    return ImageChops.lighter(mask, ImageOps.mirror(mask))


def _paint(img, mask, color, strength=1.0):
    """把掩膜处往 `color` **混**（`strength` ＝ 混多少）。
    ⚠️ 是"混"，不是"填实色"：底下的材质纹理要留在里面 —— 填实色的结果是一块贴上去的塑料片，
    "混"出来的才是**表面起伏**。同一条也是"软掩膜"能成立的前提。
    另：只动 RGB，alpha 通道原样带过去（透明区不能因为刷五金变成不透明）。"""
    rgb = img.convert("RGB")
    m = mask if strength >= 1.0 else mask.point(lambda v: int(v * strength))
    out = Image.composite(Image.new("RGB", img.size, color), rgb, m)
    if img.mode == "RGBA":
        out = out.convert("RGBA")
        out.putalpha(img.getchannel("A"))
    return out


def _soft(mask, ss, radius=None):
    """过一道高斯 —— **硬边就是体素语言**。半径按 1× 像素给，这里乘回超采样倍率。"""
    r = STUD_SOFT if radius is None else radius
    if r <= 0:
        return mask
    return mask.filter(ImageFilter.GaussianBlur(max(0.4, r * ss)))


def draw_detail(img, w, h, mode=None):
    """在外框那一条上加对称五金（**软起伏**）。返回新图；mode="none" 原样返回。
    **要在 1× 的图上调**（见文件头 ⚠️②）。"""
    mode = DETAIL if mode is None else mode
    if mode == "none":
        return img
    ww, hh = img.size
    ss = ww // w
    corners = corner_points(w, h, FRAME)
    edges = edge_points(w, h, FRAME)

    # —— 钉＝软圆包：先落影圈，再迎光心（心**上移**半像素 ⇒ 上亮下暗 ⇒ 读成"圆包"）——
    if mode == "studs":
        core = Image.new("L", (ww, hh), 0)
        ring = Image.new("L", (ww, hh), 0)
        _discs(core, corners, STUD_CORNER_R, ss)                    # 角上大一枚（主）
        _discs(ring, corners, STUD_CORNER_R + STUD_RING, ss)
        xy = [(x, y) for x, y, _ in edges]
        _discs(core, xy, STUD_EDGE_R, ss)                           # 边上小一枚（从）
        _discs(ring, xy, STUD_EDGE_R + STUD_RING, ss)
        core = _soft(_symmetrize_x(core).transform(
            core.size, Image.AFFINE, (1, 0, 0, 0, 1, -STUD_HILITE * ss),
            resample=Image.BILINEAR), ss)
        ring = _soft(_symmetrize(ring), ss)
        ring = ImageChops.subtract(ring, core)                      # 落影只剩"心外面那一圈"
        img = _paint(img, ring, STUD_DARK, STUD_DARK_A)
        img = _paint(img, core, STUD_LIGHT, STUD_LIGHT_A)

    # —— 软锯齿 ——
    if mode in ("spikes", "mixed"):
        sm = Image.new("L", (ww, hh), 0)
        if mode == "spikes":
            _spike_ring(sm, w, h, ss, SPIKE_LEN, SPIKE_W)
        else:
            for x, y, side in edges:
                along = x if side in ("top", "bottom") else y
                _spike(sm, along, side, w, h, SPIKE_LEN, SPIKE_W, ss)
        sm = _soft(_symmetrize(sm), ss)
        img = _paint(img, sm, SPIKE_DARK, SPIKE_DARK_A)
        # 迎光：齿的上沿先亮（自己减去上移一行）。别用腐蚀取轮廓——1× 下轮廓就是全部。
        rim = ImageChops.subtract(sm, ImageChops.offset(sm, 0, 1))
        img = _paint(img, rim, SPIKE_METAL, SPIKE_METAL_A)
    return img


def build_states(gold, detail=None, frame_l=None, tip_len=None, tip_h=None,
                 end_bracket=None, holes=None):
    tip_len = TIP_LEN if tip_len is None else tip_len
    tip_h = TIP_H if tip_h is None else tip_h
    bracket = END_BRACKET if end_bracket is None else end_bracket
    hmode = END_HOLES if holes is None else holes
    ww, hh = W * SS, H * SS
    print("  [木]")
    wood = material(load_plaque(WOOD_SRC), (0.28, 0.28, 0.72, 0.72),
                    (ww, hh), TARGET_WOOD, grain=WOOD_GRAIN, crop_px=CROP,
                    structure=WOOD_STRUCT, sat=WOOD_SAT, tint_gain=WOOD_TINT)
    print("  [铁]")
    iron = material(load_plaque(IRON_SRC), (0.36, 0.46, 0.64, 0.74),
                    (ww, hh), TARGET_IRON,
                    grain=IRON_GRAIN, grain_dark=IRON_GRAIN_DARK,
                    contrast=1.00, sat=IRON_SAT, blur_f=0.13, crop_px=CROP)

    # 外形（透明底、边缘抗锯齿）—— **两端收尖**（见 TIP_LEN 的文件头说明）。
    # 收尖是"几何对称"的：左右镜像、上下镜像各一层；轮廓本身带信息，不再靠内部小件补。
    pts = end_taper_points(W, H, tip_len, tip_h)
    shape = poly_mask(pts, (ww, hh), SS)

    # 离边深度场（铁轨那 8px 的剖面用）。大的偏移量下端头会退化 ⇒ 那几层为空，
    # 由 **掩膜**（frame/wood）决定每块怎么处理，不看"离边多少"猜。
    dist = band_field(pts, (ww, hh), SS, int((FRAME + SEAM) * SS) + 1)

    if bracket:
        # 木面 ＝ 两端半圆头的水平条（＝原版的拱），铁 ＝ 外形减去它。
        woods_m = ImageChops.multiply(wood_stadium(W, H, FRAME + SEAM, END_NOSE_C, SS), shape)
        # 端头区（x < NOSE_C 或 x > W−NOSE_C）：这一带的铁**不按"离外形几像素"**着色，
        # 见 budget_shade 的 ⚠️ —— 收尖把外形收窄了，dist 在这里根本涨不上去。
        end_zone = Image.new("L", (ww, hh), 0)
        edge = int(round(END_NOSE_C * SS))
        ImageDraw.Draw(end_zone).rectangle((0, 0, edge, hh - 1), fill=255)
        ImageDraw.Draw(end_zone).rectangle((ww - 1 - edge, 0, ww - 1, hh - 1), fill=255)
        # ⚠️ 嵌线分两道，各在"该亮的地方"（原版实测，见 GOLD_INSET 的说明）：
        #   · 长边轨上 ⇒ 离**外形** GOLD_INSET 那一条窄带（＝原版亮棱的位置）；
        #   · 端头件上 ⇒ 紧贴**木面**外的一圈（＝原版拱内缘那道最亮的）。
        #   之前只有后一种，于是长边的线被顶到箍内缘（那是原版最暗的一段）。
        i0 = int(round(GOLD_INSET * SS))
        i1 = int(round((GOLD_INSET + SEAM) * SS))
        seam_rail = dist.point(lambda v: 255 if i0 <= v < i1 else 0)
        not_end = ImageChops.invert(end_zone)
        seam = ImageChops.multiply(seam_rail, not_end)
        ring = ImageChops.subtract(
            band_out(woods_m.point(lambda v: 255 if v > 128 else 0), SS, 1)
            .point(lambda v: 255 if v else 0), woods_m)
        seam = ImageChops.lighter(seam, ImageChops.multiply(ring, end_zone))
        seam = ImageChops.multiply(seam, shape)
        # 🔴 2026-09-14 11:0x **铁轨 ＝ 外形 − 木面 − 嵌线**，不能再用"木面外扩 1 层"。
        #    踩过（这次很难看）：`band_out(woods, SS, 1)` 只往外长 **1 个 1× 像素**，
        #    于是木面下边界（1× y=26.75）之外的那 7px 铁轨**整个落在掩膜外**，
        #    每像素都走 `else: op[x,y] = 0`（透明）⇒ 被 `shape` 边缘的抗锯齿混成
        #    一串 28/26/21 —— **看着像"铁很暗"，其实是那儿根本没画铁**。
        #    这正是"次按钮／页签的铁面 100% 落在最暗一档、高光 0.0%"的唯一原因：
        #    那些像素**全是边缘**，不是铁。
        #    ⇒ `frame = shape − 木面 − 嵌线`（下面这行本来就是这么写的，别再另算一份）。
        #
        # 🔴 2026-09-14 11:3x **但光这么写还漏一层**（本轮第二次踩同一个坑，这次是"漏"而不是"缺"）：
        #    木面走的是 `wood_stadium(..., FRAME + SEAM, ...)`，它的边界落在
        #    **1× 的 (FRAME+SEAM)=8.2 处**，而铁轨的坐标只有 `dist < FRAME=7`。
        #    8.2 与 7 之间那 **1.2px** 既不属于铁轨、也不属于木面、也不在嵌线上
        #    ⇒ 每个像素都走 `else: op[x,y]=0`，而被 `shape` 的抗锯齿一混，
        #    就成了一道**假暗线**。
        #    实测（`btn_secondary_100` 逐行）：y4 均值 **22.4**，而它上下邻居是 **62.1 / 68.1**
        #    —— 用眼睛看就是"按钮正中有一道横的黑缝"。主按钮 y7 47.3 也是同一处，
        #    只是被木面亮起来盖住了一半，不那么显眼。
        #    ⇒ 铁轨**按"离外形"定义**，再把木面与嵌线从中挖掉：
        #      `frame = (dist < band) − 木面 − 嵌线`。这样 band 以内的每一层都有着落，
        #      "没有材料的地方"这件事被结构性排除，不再依赖两个数的巧合对齐。
        _band_ss = int(round((FRAME + SEAM) * SS))
        frame = ImageChops.subtract(
            dist.point(lambda v: 255 if v < _band_ss else 0),
            ImageChops.lighter(woods_m, seam))
        panel = woods_m
        # 离木面**向外**的层号 ⇒ 拱内缘那道亮棱的位置。
        d_out = band_out(woods_m, SS, END_DOUT_LAYERS)
        hole, hole_rim = hole_masks(W, H, hmode, END_HOLE_X, END_HOLE_R,
                                    END_HOLE_OFF, SS)
        hole = ImageChops.multiply(hole, frame)
        hole_rim = ImageChops.multiply(hole_rim, frame)
        # ⚠️ 静默失败的哨兵：孔这种"小件"一旦 1×/SS 尺度搞错，**图上只是"没画出来"**，
        #    不会报错。所以每次都对 bbox 说一句话（踩过：孔心被算到 y=254，画布才 140）。
        hb = hole.point(lambda v: 255 if v > 40 else 0).getbbox()
        print("    孔 %s bbox=%s（画布 %dx%d）" % (hmode, hb, ww, hh))
        if hmode != "none" and hb is None:
            raise SystemExit("孔掩膜为空 —— 尺度又搞错了，别往下跑")
    else:
        # ================= 无铸件：木面先定形，铁轨从它反推 =================
        #
        # 🔴 2026-09-14 12:4x **重写**。旧写法是三者都按 `dist`（离外形的垂距）切：
        #       frame = dist < FRAME ; seam = dist < FRAME+SEAM ; panel = shape − frame − seam
        #    八边形时代这没问题（45° 直线斜切处等距内缩线仍是直线、宽度不变）。
        #    **但端头换成曲线之后它塌了** —— 端头曲率大的地方，等距内缩线向曲率中心收得更快，
        #    `dist` 在那里涨不上去 ⇒ 铁轨在 x=7 处厚度从 27 **骤降到 14**、再掉到 5。
        #    实测（`_probe_frame_def.py`，TIP_LEN=9）：
        #       甲（旧） 端头带 13/27/18.57   直边带 7/7/7   ← 端头骤降
        #       乙（新） 端头带  5/31/21.43   直边带 8/8/8   ← 平滑
        #    ⚠️ 这说明"按垂距定义铁轨"与"曲线端头"**在几何上不兼容**，是必然不是 bug。
        #
        # 新写法：**木面 ＝ 同一曲线内缩 (FRAME+SEAM)**，铁轨由外形减去木面得到。
        #    这样铁轨宽度由"两条同族曲线的间距"保证，端头与直边自动等宽。
        #    这也是 `bracket=True` 分支末尾早就写下的结论（`frame = shape − 木面 − 嵌线`），
        #    只是当时木面由 `wood_stadium` 给，无铸件时没有等价物 —— 现在用曲线内缩补上。
        # ⚠️ 坐标尺度：`artkit.chamfer_pts` 走 **0..w-1 像素格**，本文件其它函数走 **0..w 连续**。
        #    这里直接在 1× 上算两个掩膜再放大到 SS 网格（不要在 SS 网格上重算，
        #    否则又要换算一次尺度 —— 这个坑本项目踩过多次）。
        from artkit import chamfer_pts as _cham
        outer1 = Image.new("L", (W, H), 0)
        ImageDraw.Draw(outer1).polygon(
            [(x, y) for x, y in _cham(W, H, tip_len, 0)], fill=255)
        wood1 = Image.new("L", (W, H), 0)
        ImageDraw.Draw(wood1).polygon(
            [(x, y) for x, y in _cham(W, H, tip_len, FRAME + SEAM)], fill=255)
        # 木面：1× 掩膜放大到 SS 网格，再与 shape 取交（shape 带抗锯齿边）
        woods_m = ImageChops.multiply(wood1.resize((ww, hh), Image.NEAREST), shape)
        # 嵌线：贴着木面外缘往外的 SEAM 一圈（用"离木面向外"的层号，和铸件分支同一种做法）
        d_out = band_out(woods_m.point(lambda v: 255 if v > 128 else 0), SS,
                         END_DOUT_LAYERS)
        seam_layers = max(1, int(round(SEAM * SS)))
        seam = d_out.point(lambda v: 255 if 0 < v <= seam_layers else 0)
        seam = ImageChops.multiply(seam, shape)
        # 铁轨 ＝ 外形 − 木面 − 嵌线。**每一层都有着落**，不留"没材料"的缝。
        frame = ImageChops.subtract(shape, ImageChops.lighter(woods_m, seam))
        panel = woods_m
        hole = hole_rim = end_zone = None
        # ⚠️ 静默失败的哨兵：这一段一旦尺度搞错，图上只是"铁轨没了"或"木面没了"，
        #    不报错。所以每次都对面积说一句话。
        n_frame = sum(1 for v in frame.getdata() if v > 127)
        n_panel = sum(1 for v in panel.getdata() if v > 127)
        print("    无铸件档：铁轨 %d px / 木面 %d px（SS 网格）" % (n_frame, n_panel))
        if n_frame < 100 or n_panel < 1000:
            raise SystemExit("铁轨或木面几乎为空 —— 内缩尺度搞错了，别往下跑")

    if CORNER > 0:
        # 角件（默认关）：一开就会截断嵌线，轮廓读成"手工器物"。
        frame = ImageChops.lighter(
            frame, corner_brackets((ww, hh), CORNER * SS, RADIUS * SS))
        seam = ImageChops.subtract(
            seam, corner_brackets((ww, hh), (CORNER + 2.4) * SS, RADIUS * SS))

    # 金线＝嵌在箍内缘那道线。"磨"只用**低频**起伏（约 15px 一段）：
    # 老版用双频相乘，高频那层缩到 1× 会把线切成 1~2px 的碎点——那是噪点，不是工艺。
    # 线**不断**，只明暗起伏；"克制"靠线细 ＋ 整体不亮。
    wn = value_noise((ww, hh), (14, 3), seed=77)

    # —— 嵌线的**受光斜坡**：上缘全亮 → 下缘只留 SEAM_LIT_BOT ——
    #    这一条就是"线"和"选区描边"的分界：等亮的圈是画上去的，上亮下暗的圈是被光照到的。
    _rc = Image.new("L", (1, hh))
    _rc.putdata([int(round(255 * (1.0 - (1.0 - SEAM_LIT_BOT) * y / float(hh - 1))))
                 for y in range(hh)])
    seam_lit = _rc.resize((ww, hh), Image.NEAREST)

    # —— 亮度剖面（**不随状态变**，只算一次）——
    shade = budget_shade((ww, hh), SS, dist, frame, woods_m, seam, frame_l,
                         d_out=d_out, hole=hole, hole_rim=hole_rim, end_zone=end_zone,
                         rail_by_dout=not bracket)
    shade_flip = ImageOps.flip(shade)      # 按下态：光从下走

    # ⛔ 倒角线 / 镜面带 / 乘性暗角 —— **全部退役**（2026-09-14 02:2x）。
    #    它们的活现在由 shade 一条机制干完（见 budget_shade 的 docstring）：
    #      · 倒角线：只作用在最外 1~2 行，而剖面要求最外两行是**暗的**（原版外缘是 0）。
    #      · 镜面带：把最亮顶到 188（原版最亮才 95）⇒「一条白线 vs 一块黑」，这是"数码感"的头号来源。
    #      · WOOD_SHADE 暗角：和面的拱形曲线打架（一个乘性暗角 vs 一条目标曲线），只留一条。

    def compose(gl, bright=1.0, desat=1.0, flip=False):
        """gl ＝ GOLD_TIERS 里的一档；a=0 那档即"无金"，同一个位置留成暗缝。

        ⚠️ 2026-09-14 02:2x 重写：**只有一条机制在管亮度 ＝ shade**。
           铁箍＝暗轮廓→渐亮→亮棱（在箍的中段，不在外缘）→内缘回落；
           木面＝拱形（49→74→66，峰在 45% 高）。材质只负责**纹理**，不再负责明暗。
        """
        img = Image.composite(iron, wood, frame)                 # 铁箍压住木面
        # 铁的冷灰**色相**（亮度归 shade 管，这里只留"光泽"这一层差异）
        cool = Image.blend(img, Image.new("RGB", (ww, hh), IRON_HI_TINT), IRON_HI_MIX)
        img = Image.composite(cool, img, frame)
        # 剖面（乘性：底下的材质纹理原样保留，只有大尺度明暗被重排）
        sh = shade_flip if flip else shade
        img = ImageChops.multiply(img, Image.merge("RGB", (sh, sh, sh)))

        if gl["a"] > 0:
            # 颜色：先往"灰黄铜"收一点（克制），再往**钢灰**掺（steel），最后整体提亮。
            col = gold if gl["dull"] <= 0 else tuple(
                int(gold[i] * (1 - gl["dull"]) + GOLD_DULL[i] * gl["dull"])
                for i in range(3))
            if gl.get("steel", 0) > 0:
                st = gl["steel"]
                col = tuple(int(col[i] * (1 - st) + GOLD_STEEL[i] * st) for i in range(3))
            col = tuple(min(255, int(c * gl["gain"])) for c in col)
            gm = ImageChops.multiply(seam, wn.point(
                lambda v: int(255 - (255 - v) * gl["wear"])))
            gm = ImageChops.multiply(gm, seam_lit)      # ← 受光方向（上亮下暗）
            gm = gm.point(lambda v: int(v * gl["a"]))
            img = Image.composite(Image.new("RGB", (ww, hh), col), img, gm)
            # 金线底下压一道暗影：金才读成"嵌进箍里"，不是"描在箍上"
            img = Image.composite(Image.new("RGB", (ww, hh), (16, 12, 9)), img,
                                  seam.point(lambda v: v * gl["shadow"] // 255))
        else:
            # 无金：同一个位置留成一道暗缝——木与铁的分界，不靠金也立得住。
            # ⚠️ 掩膜必须按 v 缩放（`v * 235 // 255`）。写成常数会把**整张图**都盖上，
            #    按钮会变成一块黑砖（这个坑踩过一次）。
            img = Image.composite(Image.new("RGB", (ww, hh), (12, 10, 8)), img,
                                  seam.point(lambda v: v * 235 // 255))

        # 五金细节（铆钉／尖刺）：点位在 corner_points()／edge_points()，四重镜像，**不动轮廓**。
        # ⚠️ 它**不在这里画**——要等缩回 1× 之后再画（见 down()），否则会被 LANCZOS 抹糊。
        if bright != 1.0:
            img = ImageEnhance.Brightness(img).enhance(bright)
        if desat != 1.0:
            img = ImageEnhance.Color(img).enhance(desat)
        rgba = img.convert("RGBA")
        rgba.putalpha(shape)
        return rgba

    def down(im):
        # 缩回 1× 后给一道**很轻**的锐化：1× 下靠这一步把边缘"立"起来。
        # 半径必须小（0.7）、阈值不低（3）——重了会出白边，比不锐化还脏。
        out = im.resize((W, H), Image.LANCZOS)
        rgb = out.convert("RGB").filter(
            ImageFilter.UnsharpMask(radius=0.7, percent=58, threshold=3))
        rgba = rgb.convert("RGBA")
        rgba.putalpha(out.split()[3])
        # 五金细节**在这里**画（1× 图上）：这是 1× sprite，3px 的钉／齿在 4× 画再缩，
        # 会被 LANCZOS 抹成一团糊——试过，四档在样本页上长得一模一样，等于没加。
        return draw_detail(rgba, W, H, detail)

    def scaled(tier, k):
        """同一档在不同状态下的收放：禁用时那条线也磨掉大半，并多断几口。"""
        gl = dict(tier)
        gl["a"] = tier["a"] * k
        gl["wear"] = min(1.0, tier["wear"] + (0.0 if k >= 1.0 else 0.35))
        return gl

    def four(tier):
        """同一副骨出四态。tier 只是嵌线的强度档（默认 soft），**不是文明身份**。"""
        return {
            "": down(compose(scaled(tier, 1.00))),
            "_hover": down(compose(scaled(tier, 1.00), bright=1.14)),
            "_pressed": down(compose(scaled(tier, 0.92), bright=0.80, desat=0.94, flip=True)),
            "_disabled": down(compose(scaled(tier, 0.45), bright=0.90, desat=0.45)),
        }

    return {name: four(GOLD_TIERS[name]) for name in ("soft", "full", "off")}


# ---------------------------------------------------------------- 交付

def write_sprite(states, out_dir=None, tag=""):
    out_dir = out_dir or OUT_BTN
    os.makedirs(out_dir, exist_ok=True)
    for suffix, im in states.items():
        path = os.path.join(out_dir, "btn_primary_110%s%s.png" % (tag, suffix))
        im.save(path)
        print("  -> %s  %dx%d" % (os.path.basename(path), im.width, im.height))


# 这里**故意不生成** NineRegionSprite sidecar。
# 项目口径（见 GUI/Brushes/AwakeBrushes.xml 抬头）：九宫格边距只写在 brush 的
# <BrushLayer ExtendLeft/Top/Right/Bottom>，不写进 SpriteData。两处各写一份，
# 改一处忘另一处就是一个静默的不一致（九宫格会悄悄退回整体拉伸，而且不报错）。
# 曾生成过 SpriteParts/ui_awake_button/btn_primary_110.xml（产 btn_primary_110_9），已删。


def build_sheet(states):
    os.makedirs(OUT_SHEET, exist_ok=True)
    pad, gap, zoom = 40, 26, 3
    cw = W * zoom
    sh = Image.new("RGB", (pad * 2 + 4 * cw + 3 * gap,
                           pad * 2 + 64 + H * zoom + 540), (16, 14, 13))
    d = ImageDraw.Draw(sh)
    f_title = ImageFont.truetype(FONT, 26)
    f_lbl = ImageFont.truetype(FONT, 17)
    f_small = ImageFont.truetype(FONT, 14)
    dim = (120, 106, 90)
    warm = (190, 172, 142)

    d.text((pad, pad), "Awake.Button.Primary · 110×35 · 木面＋铁轨＋端头铸件＋嵌线 · 四态", font=f_title,
           fill=(214, 196, 160))
    d.text((pad, pad + 38),
           "形：八边形轮廓（两端收尖）＋ 端头铸件（拱＋圆孔）；色：暗轮廓→亮棱→体色的剖面；"
           "材质肌理与金值＝实拍取材",
           font=f_small, fill=(140, 124, 104))

    y = pad + 64
    for i, (suffix, lbl) in enumerate(zip(["", "_hover", "_pressed", "_disabled"],
                                          ["默认", "悬停", "按下", "禁用"])):
        x = pad + i * (cw + gap)
        sh.paste(states[suffix].resize((cw, H * zoom), Image.NEAREST), (x, y))
        d.rectangle([x, y, x + cw, y + H * zoom], outline=(60, 52, 44))
        d.text((x, y + H * zoom + 10), "%s  btn_primary_110%s" % (lbl, suffix),
               font=f_lbl, fill=warm)
        d.text((x, y + H * zoom + 34), "实尺 ↓", font=f_small, fill=dim)
        sh.paste(states[suffix], (x, y + H * zoom + 54))

    # 真实落点
    import glob
    frames = glob.glob(os.path.join(AWAKE, "GUI", "SpriteParts", "ui_awake_frame",
                                    "panel_input_800.png"))
    y2 = y + H * zoom + 96
    d.text((pad, y2), "真实落点（NpcDialogue 输入区，实尺拼接）", font=f_lbl, fill=warm)
    y2 += 26
    if frames:
        band = Image.open(frames[0]).convert("RGBA")
        band.putalpha(255)
        sh.paste(band, (pad, y2))
        for k, (suf, txt) in enumerate([("", "发送"), ("_hover", "悬停"),
                                        ("_disabled", "禁用")]):
            by = y2 + 10 + k * 40
            sh.paste(states[suf], (pad + 800 - 115, by))
            d.text((pad + 800 - 115 + 32, by + 10), txt, font=ImageFont.truetype(FONT, 17),
                   fill=(226, 214, 194))
        d.rectangle([pad, y2, pad + 800, y2 + 130], outline=(60, 52, 44))

    # UI 是通用控件：只有一档
    y3 = y2 + 170
    d.text((pad, y3), "通用 UI · 只有一档，不按文明分皮", font=f_lbl, fill=warm)
    for j, ln in enumerate([
        "控件＝玩家的界面：同一块对话面板，跟玩家是帝国人还是巴旦尼亚人无关。",
        "曾按「帝国＝满金／蛮族＝无金」分过三档，已推翻——那是把**内容口径**（描述世界里的文明）",
        "套到了**控件口径**上。要按归属换皮的是内容美术（信件／旗帜／纹章＝这是谁的），不是按钮。",
        "工程上也不成立：Brushes 是静态 XML，没有「按玩家文明选 brush」这种机制。",
    ]):
        d.text((pad, y3 + 32 + j * 22), ln, font=f_small, fill=(150, 134, 112))

    path = os.path.join(OUT_SHEET, "sheet_btn_primary.png")
    sh.save(path)
    print("  -> %s  %dx%d" % (path, sh.width, sh.height))


def main():
    gold = sample_gold(load_plaque(IRON_SRC))
    print("[2] 出四态（UI 只有一档 —— 通用控件，不按文明分皮）")
    variants = build_states(gold)
    print("[3] 落盘")
    write_sprite(variants["soft"])
    print("[4] 样本页")
    build_sheet(variants["soft"])
    print("done.")


if __name__ == "__main__":
    main()
