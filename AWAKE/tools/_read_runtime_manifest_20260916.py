"""看清模组 Runtime 目录的部署校验机制：manifest.json 与 SHA256SUMS.txt 各管什么。"""
import json
import os

RT = (r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
      r"\Modules\AWAKE\bin\Win64_Shipping_Client\Runtime")

man = os.path.join(RT, "manifest.json")
sums = os.path.join(RT, "SHA256SUMS.txt")

print("=== manifest.json ===")
print("存在 =", os.path.exists(man), " 大小 =", os.path.getsize(man) if os.path.exists(man) else "-")
if os.path.exists(man):
    d = json.load(open(man, encoding="utf-8"))
    if isinstance(d, dict):
        print("顶层键 =", list(d.keys())[:15])
        for k, v in list(d.items())[:15]:
            if isinstance(v, list):
                print("  %-24s list(%d)  样例=%s" % (k, len(v), json.dumps(v[0], ensure_ascii=False)[:160] if v else None))
            elif isinstance(v, dict):
                print("  %-24s dict keys=%s" % (k, list(v.keys())[:8]))
            else:
                print("  %-24s = %s" % (k, str(v)[:120]))
    elif isinstance(d, list):
        print("是列表，条数 =", len(d), " 样例 =", json.dumps(d[0], ensure_ascii=False)[:300])

print()
print("=== SHA256SUMS.txt ===")
lines = [l.rstrip("\n") for l in open(sums, encoding="utf-8")]
print("行数 =", len(lines))
print("前三行:")
for l in lines[:3]:
    print("  ", l)
print("末三行:")
for l in lines[-3:]:
    print("  ", l)

# 校验器在哪
print()
print("=== 搜部署校验器 / 清单生成器 ===")
