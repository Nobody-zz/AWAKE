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
            return "本周没有记录。世界暂时安静如常。";
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
        builder.AppendLine("这些事已被收入世界的记忆。你仍在卡拉迪亚的风暴之中。");
        return builder.ToString();
    }
}
