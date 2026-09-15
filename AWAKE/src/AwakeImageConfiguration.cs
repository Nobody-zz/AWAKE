using TaleWorlds.Library;

namespace Awake;

/// <summary>
/// 出图端点的 MCM 动作：填钥匙。**和 AI 链路那套完全平行、互不影响。**
/// 状态文本统一由 <see cref="AwakeImageProbe"/> 持有，MCM 里只有一个只读状态栏。
/// </summary>
internal static class AwakeImageConfiguration
{
    internal static void PromptForApiKey()
    {
        InformationManager.ShowTextInquiry(
            new TextInquiryData(
                "输入或替换出图 API Key",
                "输入出图服务的 API Key。输入框保持可见，便于你核对；"
                + "保存后只写入本机保护存储，不进入 MCM、存档或日志。"
                + "本机免认证服务（例如本机 Player2 App）不需要填，关闭“走云端”即可。",
                true,
                true,
                "保存",
                "取消",
                secret => SaveApiKey((secret ?? string.Empty).Trim()),
                null,
                false,
                null,
                string.Empty,
                string.Empty),
            true,
            false);
    }

    private static void SaveApiKey(string secret)
    {
        if (secret.Length == 0)
        {
            AwakeFeedback.ShowError("出图 API Key 不能为空。");
            return;
        }

        bool saved = AwakeImageSecretStore.Save(secret);
        AwakeLog.Write("image_key_action saved=" + (saved ? "true" : "false"));

        if (saved)
        {
            string message = "出图 API Key 已写入本机保护存储。现在可以点击“测试出图”。";
            AwakeImageProbe.SetStatus(message);
            AwakeFeedback.ShowSuccess(message);
            return;
        }

        string failure = "出图 API Key 保存失败：本机保护存储不可用。为安全起见没有落盘，请查看 Awake.log。";
        AwakeImageProbe.SetStatus(failure);
        AwakeFeedback.ShowError(failure);
    }
}
