#!/usr/bin/env python3
"""Player2 生图接口探针 / 参考实现。

用途：验证并演示 AWAKE 侧调用 Player2 生图接口的正确姿势。
依据 2026-09-14 本机实测（Player2 App 0.10.78），详见
    AWAKE/docs/PLAN-AI-PORTRAIT-IMAGE-20260913.md  §12.5

两个端点，行为**不一致**，别用同一套解析：
    POST /v1/image/generate   prompt + width/height        -> JPEG，image 字段 **带** data: 前缀
    POST /v1/image/edit       prompt + image(base64) + w/h -> PNG， image 字段 **不带** 前缀

最大的坑（本脚本存在的主要理由）：
    /image/generate 返回的 base64 带 "data:image/jpeg;base64," 前缀，而官方文档写的是
    "without data URI prefix"。直接整串 b64decode **不报错**，但前缀里 20 个合法字符
    (dataimage/jpegbase64) 会被当成数据解出 15 字节垃圾顶在文件头 —— 又因为 20 是 4 的
    倍数，后面的真数据恰好落在 4 字符边界，于是 JPEG 本身是好的，只是前面多了 15 字节。
    症状：既不是 PNG 也不是 JPEG 的文件头，往后十几字节却有完整 JPEG，PIL 打不开。

用法：
    python probe_player2_image.py --prompt "a medieval knight, warm candlelight"
    python probe_player2_image.py --prompt "add a gold crown, keep the face" --ref in.png
    python probe_player2_image.py --joules

本地 App 不需要认证；云端点（https://api.player2.game/v1）才要 Bearer，用 --base/--token。
"""

import argparse
import base64
import json
import sys
import time
import urllib.error
import urllib.request

DEFAULT_BASE = 'http://127.0.0.1:4315/v1'

MAGICS = [
    (b'\x89PNG\r\n\x1a\n', 'png'),
    (b'\xff\xd8\xff', 'jpg'),
    (b'GIF8', 'gif'),
    (b'RIFF', 'webp'),
    (b'BM', 'bmp'),
]


def _request(url, method='GET', body=None, token=None, timeout=240):
    data = json.dumps(body).encode('utf-8') if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header('Content-Type', 'application/json')
    req.add_header('Accept', 'application/json')
    if token:
        req.add_header('Authorization', 'Bearer ' + token)
    t0 = time.time()
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.status, r.read(), time.time() - t0
    except urllib.error.HTTPError as e:
        try:
            raw = e.read()
        except Exception:
            raw = b''
        return e.code, raw, time.time() - t0


def strip_data_uri(s):
    """剥掉 data URI 前缀。返回 (纯 base64, 前缀或 None)。

    这是本文件最关键的一步 —— 文档说不带前缀，实测 /image/generate 带。
    """
    if s.startswith('data:'):
        comma = s.find(',')
        if comma != -1:
            return s[comma + 1:], s[:comma]
    return s, None


def sniff(b):
    for magic, ext in MAGICS:
        if b.startswith(magic):
            return ext
    return None


def decode_image_field(field):
    """把响应里的 image 字段解成 (bytes, 扩展名, data前缀)。

    不假定格式、不假定有没有 data: 前缀 —— 两样都实测过不一致。
    """
    payload, prefix = strip_data_uri(field)
    raw = base64.b64decode(payload)
    ext = sniff(raw)
    return raw, ext, prefix


def call_image(base, token, prompt, width, height, ref_path=None):
    """ref_path 给了就打 /image/edit（吃参考图），否则 /image/generate。"""
    if ref_path:
        ref = open(ref_path, 'rb').read()
        body = {
            'prompt': prompt,
            'image': base64.b64encode(ref).decode('ascii'),
        }
        # 注意：实测该模型忽略 width/height，直接出 1024。带上无妨。
        if width:
            body['width'] = width
        if height:
            body['height'] = height
        path = '/image/edit'
    else:
        body = {'prompt': prompt}
        if width:
            body['width'] = width
        if height:
            body['height'] = height
        path = '/image/generate'

    status, raw, secs = _request(base + path, 'POST', body, token)
    return path, status, raw, secs


def main():
    ap = argparse.ArgumentParser(description='Player2 生图探针（AWAKE）')
    ap.add_argument('--prompt')
    ap.add_argument('--ref', help='参考图路径；给了就走 /image/edit')
    ap.add_argument('--width', type=int, default=None)
    ap.add_argument('--height', type=int, default=None)
    ap.add_argument('--out', default='player2_out')
    ap.add_argument('--base', default=DEFAULT_BASE, help='默认本地 App；云用 https://api.player2.game/v1')
    ap.add_argument('--token', default=None, help='仅云端点需要')
    ap.add_argument('--joules', action='store_true', help='只查余额')
    args = ap.parse_args()

    if args.joules:
        st, raw, _ = _request(args.base + '/joules', token=args.token, timeout=30)
        print(st, raw.decode('utf-8', errors='replace'))
        return 0

    if not args.prompt:
        ap.error('--prompt 必填（或用 --joules）')

    st, raw, _ = _request(args.base + '/joules', token=args.token, timeout=30)
    before = json.loads(raw.decode('utf-8')).get('joules') if st == 200 else None
    print(f'余额(前): {before}')

    path, status, raw, secs = call_image(
        args.base, args.token, args.prompt, args.width, args.height, args.ref)

    print(f'POST {path} -> HTTP {status}  {secs:.1f}s  body={len(raw)}B')
    if status != 200:
        print(raw[:600].decode('utf-8', errors='replace'))
        return 1

    obj = json.loads(raw.decode('utf-8'))
    field = obj.get('image') or ''
    data, ext, prefix = decode_image_field(field)

    print(f'  mimetype(响应字段) : {obj.get("mimetype")}')
    print(f'  data URI 前缀      : {prefix!r}')
    print(f'  解出字节           : {len(data)}B')
    print(f'  按 magic 判定格式  : {ext}')

    if ext is None:
        print('  !! 字节既不是 PNG/JPEG/GIF/WEBP/BMP —— 解码姿势可能不对，')
        print('     先检查有没有 data: 前缀没剥掉。头 16 字节:', data[:16].hex())
        return 2

    out = f'{args.out}.{ext}'
    with open(out, 'wb') as fh:
        fh.write(data)
    print(f'  已保存             : {out}')

    st, raw, _ = _request(args.base + '/joules', token=args.token, timeout=30)
    after = json.loads(raw.decode('utf-8')).get('joules') if st == 200 else None
    print(f'余额(后): {after}' + (f'   本次消耗 {before - after}' if before and after else ''))
    return 0


if __name__ == '__main__':
    sys.exit(main())
