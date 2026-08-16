using System.Collections.Generic;
using System.Text;

namespace Awake;

internal static class WorldEventInboxFormatter
{
    internal static string Format(IReadOnlyList<WorldEventRecord> week, int nowDay)
    {
        if (week == null || week.Count == 0) return "本周没有记录。";
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("事件收件箱");
        builder.AppendLine("────────────");
        for (int i = 0; i < week.Count && i < 20; i++)
        {
            WorldEventRecord record = week[i];
            builder.AppendLine("· 第 " + record.Day + " 天 [" + record.Kind + "] " + record.Text);
        }
        if (week.Count > 20)
        {
            builder.AppendLine("……还有 " + (week.Count - 20) + " 条更早记录。");
        }
        return builder.ToString();
    }
}
