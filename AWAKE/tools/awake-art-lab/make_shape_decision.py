# -*- coding: utf-8 -*-
"""**形状决策板** —— 把"形"这一层的每个变量、当前值、原版实测、建议值 摆在一张图上。

甲方 2026-09-14 11:5x：「先做**颜色以外**的按钮整体形状设计」。
⇒ 本图只谈"形"（轮廓/端头/结构件/比例），不谈明暗与颜色。

出：out/study/shape/shape_decision.png（宽 1820，按项目作图纪律）
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out", "study", "shape")
os.makedirs(OUT, exist_ok=True)

CW = 1820
BG = (26, 26, 28)
FG = (228, 228, 230)
DIM = (150, 150, 155)
OK = (120, 200, 130)
BAD = (220, 110, 100)
WARN = (235, 190, 100)

from curve_shape import tip_curve, CURVE_D0, CURVE_POW

# ── 行：形这一层要决定的东西 ─────────────────────────────────────────────
# (层, 变量, 现状, 原版实测, 结论, 状态)
ROWS = [
 ("轮廓", "端头走线", "近半圆（二次贝塞尔）", "单调斜线，6→42 无平台",
  "换成连续斜线（本轮 curve_shape 已做）", "变"),
 ("轮廓", "端头形状（次按钮/页签）", "八边形直线倒角，8 顶点全折", "同上",
  "与主按钮统一成同一条曲线", "变"),
 ("轮廓", "四角", "直角（CORNER=0）", "直角",
  "不动 —— 原版就是直角", "守"),
 ("轮廓", "长宽比", "3.14（110×35）", "5.52（232×42）",
  "不跟 —— 我们的槽位就更方，跟了会有大留白", "守"),

 ("端头", "端头占宽", "14.5%（16/110）", "6.0%（14/232）",
  "先不动 —— 绝对长度 16px 是对的，比例差是因为我们更短", "议"),
 ("端头", "尖端起始高", "16%（TIP_H=0.16）", "14.3%（6/42）",
  "收到 0.14 —— 现在这一档是为「让尖端看得见」加的码", "变"),
 ("端头", "尖端形态", "2px 竖面（平头）", "无平头，本身就是斜线起点",
  "去掉平头 —— 平头让尖端读成「被切过」", "变"),

 ("结构件", "外框宽度", "四面等宽 7px", "长边 5px / 端头 23px（不等宽）",
  "⚠️ 原版自己就不等宽；但要先定端头铸件的存废", "议"),
 ("结构件", "端头铸件", "开（纵深 15.5px，轴上一孔）", "有，纵深 23px（55% 高）",
  "形状上保留（它是「件」），孔位待定", "守"),
 ("结构件", "嵌线", "离外形 1.6px 一道钢线", "离外形 2px 一道亮棱",
  "位置已对；本轮只谈形——它是「线」不是「面」，形状上不动", "守"),
 ("结构件", "四角包件", "关", "无",
  "继续关 —— 开了轮廓就变八边形", "守"),
 ("结构件", "铆钉", "关", "无（原版是铸件不是铆接）", "继续关", "守"),
]

PAD = 26
TOP = 58
ROW_H = 46
COLX = [PAD, PAD + 110, PAD + 300, PAD + 700, PAD + 1030, PAD + 1520]
H = TOP + ROW_H * (len(ROWS) + 2) + PAD
board = Image.new("RGB", (CW, H), BG)
d = ImageDraw.Draw(board)

d.text((PAD, 20), "按钮形状决策板 —— 只论「形」，不论明暗与颜色", fill=FG)
d.text((PAD, 38), "2026-09-14 · 甲方：先做颜色以外的按钮整体形状设计", fill=DIM)

y = TOP
d.text((COLX[0], y), "层", fill=DIM)
d.text((COLX[1], y), "变量", fill=DIM)
d.text((COLX[2], y), "现状", fill=DIM)
d.text((COLX[3], y), "原版实测", fill=DIM)
d.text((COLX[4], y), "结论", fill=DIM)
d.text((COLX[5], y), "态", fill=DIM)
y += ROW_H - 14
d.line([(PAD, y), (CW - PAD, y)], fill=(70, 70, 74))
y += 10

for layer, var, cur, ref, concl, st in ROWS:
    col = {"变": WARN, "守": OK, "议": (150, 190, 235)}[st]
    d.text((COLX[0], y), layer, fill=DIM)
    d.text((COLX[1], y), var, fill=FG)
    d.text((COLX[2], y), cur, fill=(205, 205, 208))
    d.text((COLX[3], y), ref, fill=(205, 205, 208))
    d.text((COLX[4], y), concl, fill=col)
    d.text((COLX[5], y), st, fill=col)
    y += ROW_H

y += 6
d.line([(PAD, y), (CW - PAD, y)], fill=(70, 70, 74))
y += 12
d.text((PAD, y), "态：变＝要改 / 守＝确认不动 / 议＝需要你拍", fill=DIM)

board.save(os.path.join(OUT, "shape_decision.png"))
print("saved", board.size)
