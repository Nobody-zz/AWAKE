"""_mutate_color_order_20260915.py —— 对 Lab 新规则 color_channel_order / color_format 做变异检验。

为什么要这个：**全绿不算证据**。规则在真实数据上报了 55 处，只证明"它会报"，
不证明"它报得对、且不误报"。这里灌一份**故意写坏的**小 Prefab，
逐条断言"该报的报了、不该报的没报"。

跑法： python _mutate_color_order_20260915.py
产出： 临时目录（%TEMP%/awake-color-order-mutation）+ 一张对/错清单 + 退出码。
⚠️ 不碰仓库里的任何 Prefab。
"""
import json
import os
import subprocess
import sys
import tempfile

sys.stdout.reconfigure(encoding="utf-8")

HERE = os.path.dirname(os.path.abspath(__file__))
PY = sys.executable
TOOL = os.path.join(HERE, "preview", "preview_prefab_geometry.py")

# (控件片段, 期望规则, 期望 alpha 十六进制, 说明)
CASES_HTML = r"""<Prefab>
  <Window>
    <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="400" SuggestedHeight="300">
      <Children>
        <!-- ① alpha=00，首位 FF 当红通道 ⇒ 完全不可见。期望：报 -->
        <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="10" SuggestedHeight="10"
                Sprite="BlankWhite" Color="#FF000000" AlphaFactor="0.16" />

        <!-- ② 本来就是对的不透明金字，末位 FF。期望：不报 -->
        <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="10" SuggestedHeight="10"
                Sprite="BlankWhite" Color="#FFD700FF" />

        <!-- ③ 已经修好的写法。期望：不报 -->
        <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="10" SuggestedHeight="10"
                Sprite="BlankWhite" Color="#8C6B38FF" AlphaFactor="0.32" />

        <!-- ④ R 通道恰好=FF 的纯色（官方常见，两种读法都通）。本库口径：末字节非 FF 就算错，期望：报 -->
        <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="10" SuggestedHeight="10"
                Sprite="BlankWhite" Color="#FF0000CC" />

        <!-- ⑤ 6 位（缺 alpha）。期望：报 color_format -->
        <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="10" SuggestedHeight="10"
                Sprite="BlankWhite" Color="#8C6B38" />

        <!-- ⑥ 文字色走 Brush.FontColor 分支。期望：报 -->
        <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="100" SuggestedHeight="20"
                    Brush="Popup.Description.Text" Brush.FontColor="#FF5A5142" Text="x" />

        <!-- ⑦ 逻辑控件挂了 Sprite+Color（会画出来）。期望：报（这是本轮刚修的覆盖缺口） -->
        <DimensionSyncWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="2"
                             DimensionToSync="Vertical" WidgetToSync="..\..\"
                             Sprite="BlankWhite" Color="#FFD9A953" />

        <!-- ⑧ 已经正确的文字色。期望：不报 -->
        <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="100" SuggestedHeight="20"
                    Brush="Popup.Description.Text" Brush.FontColor="#E0C8B0FF" Text="y" />
      </Children>
    </Widget>
  </Window>
</Prefab>
"""

EXPECT_FLAG = {"①不可见": ("#FF000000", True), "②对": ("#FFD700FF", False),
               "③修好的": ("#8C6B38FF", False), "④纯色": ("#FF0000CC", True),
               "⑤六位": ("Color=#8C6B38 ", True), "⑥FontColor 分支": ("Brush.FontColor=#FF5A5142", True),
               "⑦逻辑控件": ("#FFD9A953", True), "⑧对的 FontColor": ("#E0C8B0FF", False)}


def main():
    tmp = os.path.join(tempfile.gettempdir(), "awake-color-order-mutation")
    os.makedirs(tmp, exist_ok=True)
    src = os.path.join(tmp, "MutationColorOrder.xml")
    with open(src, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(CASES_HTML)
    print("变异样本：%s" % src)

    out = os.path.join(tmp, "out")
    cmd = [PY, TOOL, "--prefab-dir", tmp, "--out", out, "--audit", "--json"]
    proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if proc.returncode != 0:
        print("跑挂了 rc=%s\n%s\n%s" % (proc.returncode, proc.stdout[-2000:], proc.stderr[-2000:]))
        return 2

    try:
        data = json.loads(proc.stdout)
    except Exception:
        print("输出不是 JSON，取尾部：\n%s" % proc.stdout[-2000:])
        return 2

    hits = []
    for prefab in data.get("prefabs", []):
        for issue in prefab.get("issues", []):
            if issue["rule"] in ("color_channel_order", "color_format"):
                hits.append(issue)

    print("\n== 规则命中 %d 条 ==" % len(hits))
    for h in sorted(hits, key=lambda x: x.get("line") or 0):
        print("  L%-4s %-20s %s" % (h.get("line"), h["rule"], h["message"]))

    bad = 0
    print("\n== 逐条断言 ==")
    # ⚠️ 关键词必须**唯一**。第一版拿 "FontColor" 当关键词 ⇒ ⑧ 被 ⑥ 的命中带成假阳性。
    #    这类"判据本身写错"比规则写错更难发现，所以关键词一律用完整色值字面量。
    for label, (token, want) in EXPECT_FLAG.items():
        got = any(token in h["message"] for h in hits)
        mark = "OK  " if got == want else "FAIL"
        if got != want:
            bad += 1
        print("  [%s] %-16s 关键词 %-28s 期望%s 实际%s"
              % (mark, label, token, "报" if want else "不报", "报" if got else "不报"))

    print("\n结论：%s（失败 %d）" % ("全部符合预期" if bad == 0 else "有 %d 条不符" % bad, bad))
    return 1 if bad else 0


if __name__ == "__main__":
    raise SystemExit(main())
