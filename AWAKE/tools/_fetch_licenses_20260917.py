# -*- coding: utf-8 -*-
"""向上线件里每一件第三方依赖的**官方包元数据**要许可声明（一手，不是凭记忆）。

数据源：nuget.org 的 nuspec（`https://api.nuget.org/v3-flatcontainer/<id>/<ver>/<id>.nuspec`），
里面的 `<license type="expression">` 就是包作者写的 SPDX 表达式。
版本号取自**上线件 DLL 的版本信息**（`shipped-versions-20260917.txt`），不是猜的。
"""
import re
import sys
import urllib.request

sys.stdout.reconfigure(encoding="utf-8")

# (包 id, 版本, 对应上线件里的什么)
TARGETS = [
    ("microsoft.netcore.app.runtime.win-x64", "8.0.28", ".NET 8 运行时（coreclr/hostfxr/System.* 那一大片）"),
    ("microsoft.netcore.app.host.win-x64", "8.0.28", "apphost（MarcusAwakeRuntimeService.exe 的宿主）"),
    ("microsoft.ml.onnxruntime", "1.18.0", "onnxruntime.dll / onnxruntime_providers_shared.dll / Microsoft.ML.OnnxRuntime.dll"),
    ("microsoft.ml.tokenizers", "2.0.0", "Microsoft.ML.Tokenizers.dll"),
    ("google.protobuf", "3.30.2", "Google.Protobuf.dll（Tokenizers 的传递依赖）"),
    ("microsoft.data.sqlite", "8.0.21", "Microsoft.Data.Sqlite.dll"),
    ("sqlitepclraw.batteries_v2", "2.1.10", "SQLitePCLRaw.batteries_v2.dll"),
    ("sqlitepclraw.core", "2.1.10", "SQLitePCLRaw.core.dll"),
    ("sqlitepclraw.provider.e_sqlite3", "2.1.10", "SQLitePCLRaw.provider.e_sqlite3.dll / e_sqlite3.dll"),
]

URL = "https://api.nuget.org/v3-flatcontainer/%s/%s/%s.nuspec"


def fetch(pkg, ver):
    url = URL % (pkg, ver, pkg)
    req = urllib.request.Request(url, headers={"User-Agent": "awake-notices/1.0"})
    with urllib.request.urlopen(req, timeout=40) as resp:
        return resp.read().decode("utf-8", errors="replace")


def pick(xml, tag):
    m = re.search(r"<%s[^>]*>([^<]*)</%s>" % (tag, tag), xml)
    return m.group(1).strip() if m else None


print("%-42s %-9s %-14s %s" % ("包", "版本", "许可(SPDX)", "作者"))
print("-" * 118)
for pkg, ver, what in TARGETS:
    try:
        xml = fetch(pkg, ver)
    except Exception as exc:                     # noqa: BLE001
        print("%-42s %-9s **取不到** %s" % (pkg, ver, exc))
        continue
    lic = pick(xml, "license") or "(nuspec 未写 expression)"
    lic_type = re.search(r'<license type="([^"]+)"', xml)
    auth = pick(xml, "authors") or "?"
    proj = pick(xml, "projectUrl") or ""
    print("%-42s %-9s %-14s %s" % (pkg, ver, lic, auth))
    print("    %s" % what)
    if lic_type and lic_type.group(1) != "expression":
        print("    ⚠️ license type = %s（不是 expression），需另找" % lic_type.group(1))
    if proj:
        print("    %s" % proj)
