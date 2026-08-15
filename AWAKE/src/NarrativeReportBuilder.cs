using System;
using System.Collections.Generic;
using System.Text;

namespace Awake;

internal static class NarrativeReportBuilder
{
    internal static string Build(IReadOnlyList<WorldEventRecord> week, int nowDay)
    {
        if (week == null || week.Count == 0)
        {
            return "本周没有记录。世界安静如常，只有女神的目光仍落在卡拉迪亚之上。";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("世界周报");
        builder.AppendLine("────────────");
        builder.AppendLine("这一周，卡拉迪亚没有真正平静下来。");
        builder.AppendLine();
        for (int i = 0; i < week.Count && i < 5; i++)
        {
            builder.AppendLine("· 第 " + week[i].Day + " 天：" + week[i].Text);
        }
        if (week.Count > 5)
        {
            builder.AppendLine("……还有 " + (week.Count - 5) + " 件旧事沉入记忆。");
        }
        builder.AppendLine();
        builder.AppendLine("女神将这些事收入眼底，而你正站在她注视的方向。");
        return builder.ToString();
    }
}
