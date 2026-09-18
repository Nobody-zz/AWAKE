# -*- coding: utf-8 -*-
"""查下沿那几行到底被谁接管 —— 白改了三次，得看块归属。"""
import make_button_primary as mb
from PIL import Image, ImageDraw, ImageChops

W, H, SS = mb.W, mb.H, mb.SS
pts = mb.end_taper_points(W, H, mb.TIP_LEN, mb.TIP_H)
shape = mb.poly_mask(pts, (W * SS, H * SS), SS)
woods = ImageChops.multiply(mb.wood_stadium(W, H, mb.FRAME + mb.SEAM, mb.END_NOSE_C, SS), shape)

b = lambda v: 255 if v else 0
band = ImageChops.subtract(mb.band_out(woods, SS, 1).point(b), woods)
ez = Image.new("L", (W * SS, H * SS), 0)
ed = int(round(mb.END_NOSE_C * SS))
ImageDraw.Draw(ez).rectangle((0, 0, ed, H * SS - 1), fill=255)
ImageDraw.Draw(ez).rectangle((W * SS - 1 - ed, 0, W * SS - 1, H * SS - 1), fill=255)
sp, wp, bp, ep = shape.load(), woods.load(), band.load(), ez.load()

dd = mb.band_field(pts, (W * SS, H * SS), SS, int((mb.FRAME + mb.SEAM) * SS) + 1).load()
x = 55 * SS
print("x=55 中列（下半区）归属 + d 值：")
for y in range(88, H * SS):
    if sp[x, y] == 0:
        tag = "透明"
    elif wp[x, y]:
        tag = "木"
    elif ep[x, y]:
        tag = "端头铁"
    elif bp[x, y]:
        tag = "铁轨"
    else:
        tag = "?"
    print("  1x_y=%5.2f  ss_y=%3d  d=%2d  %s" % (y / SS, y, dd[x, y], tag))
