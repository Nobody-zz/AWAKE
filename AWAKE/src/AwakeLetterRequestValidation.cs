namespace Awake;

/// <summary>
/// 信件发送请求的纯校验（零外部依赖，可离线单测）。
/// <para>
/// 从 <c>AwakeLetterService</c> 拆出：服务本体经「联系人键反查英雄」调用
/// <c>AwakeMessengerService</c> → <c>NpcDialogueLauncher</c> → Gauntlet UI overlay，
/// 整条链编不进 <c>AWAKE.Tests</c>；而本类不依赖任何战役/UI 类型，故单独成文件，
/// 让 SDK smoke 能直接覆盖拒绝原因契约。
/// </para>
/// </summary>
internal static class AwakeLetterRequestValidator
{
    internal static string GetRejectionReason(string contactKey, string text)
    {
        if (string.IsNullOrWhiteSpace(contactKey)) return "contact_missing";
        if (string.IsNullOrWhiteSpace(text)) return "text_missing";
        if (System.Text.Encoding.UTF8.GetByteCount(text) > AwakeLetterConstants.MaximumLetterBytes) return "text_too_long";
        return string.Empty;
    }
}
