#!/usr/bin/env python3
# 生成 SampleImages.cs —— 出图适配层验台用的图片样本。
#
# 为什么样本要在**仓库外**产：C# 侧要断言的"真尺寸"必须来自一个独立生产者，
# 不能由被测代码自己算。这里用 Pillow 出图、由 Pillow 自报尺寸，写进常量当判据。
#
# 运行（需要 Pillow）：
#   python make_sample_images.py
# 产出：
#   tools/image-shape-harness/SampleImages.cs      （覆盖，勿手改）
#
# 改样本尺寸/格式就在这里改，然后跑一遍；生成器与产出必须一致。

import base64
import io
import os

from PIL import Image

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
OUT_NAME = 'SampleImages.cs'

# (C# 常量前缀, Pillow 格式名, (宽, 高), 编码参数)
SAMPLES = [
    ('Png',  'PNG',  (320, 192), {}),
    ('Jpeg', 'JPEG', (320, 192), {'quality': 82}),
    ('Gif',  'GIF',  (24, 14),   {}),
    ('Bmp',  'BMP',  (64, 32),   {}),
    ('Webp', 'WEBP', (64, 32),   {}),
]


def make(size):
    """可复现的确定性图案：不引入随机，重跑必须逐字节一致。"""
    width, height = size
    img = Image.new('RGB', size)
    px = img.load()
    for y in range(height):
        for x in range(width):
            px[x, y] = ((x * 3) % 256, (y * 5) % 256, ((x + y) * 2) % 256)
    return img


def encode(img, fmt, **kw):
    buf = io.BytesIO()
    img.save(buf, format=fmt, **kw)
    return buf.getvalue()


def dims_of(raw):
    with Image.open(io.BytesIO(raw)) as im:
        return im.size


def main():
    entries = []
    for prefix, fmt, size, kw in SAMPLES:
        raw = encode(make(size), fmt, **kw)
        truth = dims_of(raw)
        entries.append((prefix, fmt, truth, raw))
        print('%-6s %-5s truth=%s bytes=%d head=%s' % (
            prefix, fmt, truth, len(raw), raw[:12].hex()))

    lines = [
        '// 由 tools/image-shape-harness/make_sample_images.py 产出，样本来自 Pillow（与本仓储的解析代码无关）。',
        '// 真尺寸（Pillow 自报）写进常量，供 C# 侧当独立判据；不要手改，改生成器后重跑。',
        'using System;',
        '',
        'namespace Awake.ImageShapeHarness;',
        '',
        'internal static class SampleImages',
        '{',
    ]

    for prefix, fmt, truth, raw in entries:
        b64 = base64.b64encode(raw).decode('ascii')
        lines.append('    internal const string %sFileFormat = "%s";' % (prefix, fmt))
        lines.append('    internal const int %sWidth = %d;' % (prefix, truth[0]))
        lines.append('    internal const int %sHeight = %d;' % (prefix, truth[1]))
        lines.append('    internal const string %sBase64 =' % prefix)
        for i in range(0, len(b64), 100):
            lines.append('        "%s" +' % b64[i:i + 100])
        lines[-1] = lines[-1][:-2] + ';'
        lines.append('')

    # 工厂方法按 entries 生成 —— 早先这里写死过 4 个、漏了 Webp，别再写死。
    for prefix, _fmt, _truth, _raw in entries:
        lines.append('    internal static byte[] %s() { return Convert.FromBase64String(%sBase64); }'
                     % (prefix, prefix))
    lines.append('}')

    path = os.path.join(OUT_DIR, OUT_NAME)
    with open(path, 'w', encoding='utf-8', newline='\n') as handle:
        handle.write('\n'.join(lines) + '\n')
    print('written: %s (%d bytes)' % (path, os.path.getsize(path)))


if __name__ == '__main__':
    main()
