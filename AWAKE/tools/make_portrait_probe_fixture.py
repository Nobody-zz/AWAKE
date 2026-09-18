"""生成画位探测用的占位图（纯 zlib 手写 PNG，不依赖 Pillow）。
尺寸跟 07 稿的画位一致：212x360。产物同时落到两种位置：
  1) AWAKE/tools/out/portrait-probe-fixture.png   —— 人看的
  2) AWAKE/src/AwakePortraitProbeFixture.cs       —— C# 里内嵌的 base64
"""

import base64
import os
import struct
import zlib

WIDTH = 212
HEIGHT = 360

BG_TOP = (26, 20, 16)
BG_BOTTOM = (58, 44, 32)
FRAME = (200, 168, 104)
BODY = (86, 70, 54)
NAMEBAR = (176, 146, 92)


def blend(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def build_pixels():
    rows = []
    for y in range(HEIGHT):
        row = bytearray()
        t = y / float(HEIGHT - 1)
        base = blend(BG_TOP, BG_BOTTOM, t)
        for x in range(WIDTH):
            r, g, b = base

            # 2px 鎏金外框
            if x < 2 or x >= WIDTH - 2 or y < 2 or y >= HEIGHT - 2:
                r, g, b = FRAME

            # 四角直角标记：让"这是占位图"一眼认出来
            corner = 26
            near_left, near_right = x < corner, x >= WIDTH - corner
            near_top, near_bottom = y < corner, y >= HEIGHT - corner
            if (near_left or near_right) and (near_top or near_bottom):
                if x < 8 or x >= WIDTH - 8 or y < 8 or y >= HEIGHT - 8:
                    r, g, b = FRAME

            # 简约胸像剪影：头 + 肩
            head_cx, head_cy, head_r = WIDTH // 2, 118, 30
            dx, dy = x - head_cx, y - head_cy
            if dx * dx + dy * dy <= head_r * head_r:
                r, g, b = BODY
            shoulder_top = 168
            if y >= shoulder_top:
                half = 26 + int((y - shoulder_top) * 0.9)
                if abs(x - head_cx) <= half:
                    r, g, b = BODY

            # 底部铭牌条：给将来的名字留位
            if HEIGHT - 46 <= y < HEIGHT - 34 and 26 <= x < WIDTH - 26:
                r, g, b = NAMEBAR

            row += bytes((r, g, b, 255))
        rows.append(bytes(row))
    return rows


def chunk(tag, payload):
    return (
        struct.pack(">I", len(payload))
        + tag
        + payload
        + struct.pack(">I", zlib.crc32(tag + payload) & 0xFFFFFFFF)
    )


def write_png(path, rows):
    raw = b"".join(b"\x00" + r for r in rows)
    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", WIDTH, HEIGHT, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9))
    png += chunk(b"IEND", b"")
    with open(path, "wb") as handle:
        handle.write(png)
    return png


def main():
    repo = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out_dir = os.path.join(repo, "tools", "out")
    os.makedirs(out_dir, exist_ok=True)

    png_path = os.path.join(out_dir, "portrait-probe-fixture.png")
    png = write_png(png_path, build_pixels())
    print("png=%s bytes=%d" % (png_path, len(png)))

    b64 = base64.b64encode(png).decode("ascii")
    lines = [b64[i : i + 100] for i in range(0, len(b64), 100)]

    cs_path = os.path.join(repo, "src", "AwakePortraitProbeFixture.cs")
    body = "\n".join('        "%s" +' % line for line in lines)
    body = body.rstrip(" +")
    with open(cs_path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(
            "namespace Awake;\n"
            "\n"
            "/// <summary>\n"
            "/// 画位探测用的内置占位图：212x360，与 07 稿画位同尺寸。\n"
            "/// 由 tools/make_portrait_probe_fixture.py 生成，别手改。\n"
            "/// 用途只有一个——**在没有出图后端的情况下，把「png → 纹理 → 上屏」这条链证死**。\n"
            "/// </summary>\n"
            "internal static class AwakePortraitProbeFixture\n"
            "{\n"
            "    internal const int Width = %d;\n"
            "    internal const int Height = %d;\n"
            "\n"
            '    internal const string PngBase64 =\n' % (WIDTH, HEIGHT)
            + body
            + ";\n"
            "}\n"
        )
    print("cs=%s base64_chars=%d lines=%d" % (cs_path, len(b64), len(lines)))


if __name__ == "__main__":
    main()
