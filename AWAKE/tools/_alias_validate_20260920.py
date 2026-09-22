# -*- coding: utf-8 -*-
"""别名收紧 · 校验前后对比：当前 WS vs 备份 WS，各跑一次 studio validate，比问题数。

证明「只删了 C 类冗余别名」没有引入任何新的 schema/来源问题。
"""
import io
import json
import os
import re
import shutil
import subprocess

REPO = r"D:\AWAKE-Dev\AWAKE"
CUR = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\authoring")
BAK = os.path.join(REPO, r"docs\worldbook-migration\projection\_archive-alias-tighten-20260920\authoring-WS-afterC")
DLL = os.path.join(REPO, r"tools\worldbook-studio\src\Awake.WorldbookStudio.Cli\bin\Release\net10.0\worldbook-studio.dll")
TMP = os.path.join(os.environ.get("TEMP", r"C:\Windows\Temp"), "awake-alias-validate-20260920")


def run_on(tag, src):
    w = os.path.join(TMP, tag)
    if os.path.isdir(w):
        shutil.rmtree(w)
    os.makedirs(os.path.join(w, "authoring"))
    for f in os.listdir(src):
        s = os.path.join(src, f)
        d = os.path.join(w, "authoring", f)
        if os.path.isdir(s):
            shutil.copytree(s, d)
        else:
            shutil.copy(s, d)
    p = subprocess.run(["dotnet", DLL, "validate", "--workspace", w],
                       cwd=REPO, capture_output=True)
    out = p.stdout.decode("utf-8", "replace")
    err = p.stderr.decode("utf-8", "replace")
    return p.returncode, out, err


print("DLL 存在：%s" % os.path.exists(DLL))
res = {}
for tag, src in (("before", BAK), ("after", CUR)):
    rc, out, err = run_on(tag, src)
    codes = re.findall(r'"Code"\s*:\s*"([^"]+)"', out) + re.findall(r'"code"\s*:\s*"([^"]+)"', out)
    n_issues = len(re.findall(r'"severity"\s*:', out, re.I)) or len(codes)
    m = re.search(r"(问题数|issues?|共)\D{0,6}(\d+)", out, re.I)
    res[tag] = (rc, sorted(codes), out)
    print("=" * 70)
    print("[%s] rc=%s  Code 计数=%d  问题数(粗)=%s" % (tag, rc, len(codes), m.group(2) if m else n_issues))
    from collections import Counter
    c = Counter(codes)
    for k, v in c.most_common(12):
        print("    %-28s %d" % (k, v))
    # 末几行摘要
    tail = [x for x in out.strip().split("\n") if x.strip()][-6:]
    for x in tail:
        print("    | %s" % x.strip()[:150])

b, a = res["before"], res["after"]
print("=" * 70)
print("Code 集合相同：%s" % (b[1] == a[1]))
print("before 独有：%s" % (set(b[1]) - set(a[1])))
print("after  独有：%s" % (set(a[1]) - set(b[1])))
