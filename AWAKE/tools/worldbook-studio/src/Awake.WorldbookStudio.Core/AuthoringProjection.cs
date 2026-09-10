namespace Awake.WorldbookStudio.Core;

internal static class AuthoringProjection
{
    public static string MapDomain(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "politics" => "politics",
            "economy" => "economy",
            "culture" => "culture",
            "military" => "war",
            "geography" => "geography",
            _ => throw new InvalidOperationException($"WB-BATCH-EVIDENCE-422: 批量元数据分类无法映射到世界书分类：{value}。")
        };

    public static string MapFactKind(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "fact" => "fact",
            "definition" => "state",
            "relation" => "relation",
            "chronology" => "fact",
            "geography" => "fact",
            "interpretation" => "interpretation",
            "rumor" => "rumor",
            "state" => "state",
            _ => throw new InvalidOperationException($"WB-AI-DRAFT-422: 客观事实类型无法写入世界书档案：{value}。")
        };
}
