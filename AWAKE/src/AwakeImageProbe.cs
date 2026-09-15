using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Library;

namespace Awake;

/// <summary>
/// 「测试出图」——MCM 上按一下，就真往填好的地址打一发，并把图落到本机。
///
/// 存在的意义：**"填了 url + key"和"url + key 能用"是两件事。**
/// 这一发把第二件事证掉，而且能立刻用眼睛看到（落盘路径会回显在提示与日志里）。
///
/// 走的是真实链路：有现成参考图就用 /image/edit，没有就纯文生图。
/// </summary>
internal static class AwakeImageProbe
{
    private const string OperationLabel = "awake_mcm_image_probe";

    /// <summary>探测用提示词。刻意写成一眼能认出是探测产物的东西，别和正式立绘混起来。</summary>
    private const string ProbePrompt =
        "medieval tavern portrait, warm candlelight, painterly canvas texture, calm expression, no text";

    private static readonly object StateGate = new object();
    private static string latestStatus = "尚未测试出图。";
    private static bool operationActive;

    internal static string Status
    {
        get
        {
            lock (StateGate) return latestStatus;
        }
    }

    internal static void Run()
    {
        lock (StateGate)
        {
            if (operationActive)
            {
                AwakeFeedback.ShowWarning("出图操作正在进行，请稍候。");
                return;
            }

            operationActive = true;
        }

        AwakeImageEndpoint endpoint;
        string resolveError;
        if (!AwakeImageEndpointResolver.TryResolve(AwakeSettings.Current, out endpoint, out resolveError))
        {
            lock (StateGate) operationActive = false;
            Finish(false, resolveError);
            return;
        }

        string apiKey = string.Empty;
        if (endpoint.IsCloud)
        {
            string keyError;
            apiKey = AwakeImageSecretStore.TryRead(out keyError);
            if (string.IsNullOrEmpty(apiKey))
            {
                lock (StateGate) operationActive = false;
                Finish(false, keyError);
                return;
            }
        }

        AwakeSettings.TrySaveCurrentConfiguration(out _);

        AwakeConfig config = AwakeSettings.Current;
        int width = config == null ? 512 : config.PortraitImageWidth;
        int height = config == null ? 512 : config.PortraitImageHeight;

        AwakeBackgroundTask.Run(async () =>
        {
            AwakeImageOutcome outcome;
            try
            {
                byte[] reference = TryLoadFirstCachedPortrait();
                AwakeImageRequest request = new AwakeImageRequest(ProbePrompt, reference, width, height);
                using (CancellationTokenSource timeout = AwakeImageClient.CreateTimeoutScope())
                {
                    outcome = await AwakeImageClient
                        .GenerateAsync(endpoint, request, apiKey, timeout.Token)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                AwakeLog.Write("image_probe_failed error=" + ex.Message);
                outcome = new AwakeImageOutcome
                {
                    Ok = false,
                    ErrorCode = "image_unexpected",
                    ErrorMessage = "出图探测异常：" + ex.Message
                };
            }
            finally
            {
                lock (StateGate) operationActive = false;
            }

            AwakeUiDispatcher.Enqueue(() => Report(outcome, endpoint));
        },
        OperationLabel);
    }

    private static void Report(AwakeImageOutcome outcome, AwakeImageEndpoint endpoint)
    {
        if (outcome == null)
        {
            Finish(false, "出图探测没有返回结果。");
            return;
        }

        if (!outcome.Ok)
        {
            AwakeLog.Write("image_probe_error url=" + outcome.RequestUrl
                + " code=" + outcome.ErrorCode
                + " status=" + outcome.StatusCode.ToString(CultureInfo.InvariantCulture)
                + " elapsed_ms=" + outcome.ElapsedMs.ToString(CultureInfo.InvariantCulture));
            Finish(false, outcome.ErrorMessage);
            return;
        }

        AwakeImageReply reply = outcome.Reply;
        string savedPath;
        string saveNote;
        bool saved = TrySaveProbeArtifact(reply, out savedPath, out saveNote);

        AwakeLog.Write("image_probe_ok shape=" + AwakeImageEndpointResolver.ShapeId(endpoint.Shape)
            + " url=" + outcome.RequestUrl
            + " status=" + outcome.StatusCode.ToString(CultureInfo.InvariantCulture)
            + " elapsed_ms=" + outcome.ElapsedMs.ToString(CultureInfo.InvariantCulture)
            + " format=" + reply.Format
            + " size=" + reply.Width.ToString(CultureInfo.InvariantCulture)
            + "x" + reply.Height.ToString(CultureInfo.InvariantCulture)
            + " bytes=" + reply.Bytes.Length.ToString(CultureInfo.InvariantCulture)
            + " declared_mime=" + (reply.DeclaredMime.Length == 0 ? "none" : reply.DeclaredMime)
            + " saved=" + (saved ? savedPath : "skipped"));

        StringBuilder message = new StringBuilder();
        message.Append("出图成功：").Append(reply.Describe());
        message.Append(" · 耗时 ").Append(FormatSeconds(outcome.ElapsedMs)).Append('s');
        message.Append(saved ? " · 已存到 " + savedPath : " · " + saveNote);
        Finish(true, message.ToString());
    }

    /// <summary>MCM 只读状态栏的唯一写入口。填钥匙那侧也走这里，状态不分家。</summary>
    internal static void SetStatus(string text)
    {
        lock (StateGate) latestStatus = (text ?? string.Empty).Trim();
        AwakeSettings.NotifyPortraitImageStatusChanged();
    }

    private static void Finish(bool success, string message)
    {
        string text = (message ?? string.Empty).Trim();
        if (text.Length == 0) text = success ? "出图完成。" : "出图失败。";

        SetStatus(text);

        if (success) AwakeFeedback.ShowSuccess(text);
        else AwakeFeedback.ShowError(text);
    }

    /// <summary>
    /// 缓存里只要有一张现成的立绘就拿它当参考图 —— 这正是 §13 那条路线
    /// （游戏自渲肖像当身份锚点）在模组侧的下半段，顺手也验了 /image/edit。
    /// </summary>
    private static byte[] TryLoadFirstCachedPortrait()
    {
        try
        {
            var keys = AwakePortraitCache.ListKeys();
            for (int i = 0; i < keys.Count; i++)
            {
                string path = AwakePortraitCache.AbsolutePathFor(keys[i]);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length > 0) return bytes;
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("image_probe_reference_failed error=" + ex.Message);
        }

        return null;
    }

    /// <summary>
    /// 落盘。名字带 <c>probe_</c> 前缀，和正式立绘（<c>portrait_</c>）分开，
    /// 免得探测产物被当成正式缓存命中。
    /// **扩展名按 magic 认出来的格式给，不按请求的格式给。**
    /// </summary>
    private static bool TrySaveProbeArtifact(AwakeImageReply reply, out string savedPath, out string note)
    {
        savedPath = null;
        note = string.Empty;

        string extension = ExtensionFor(reply.Format);
        if (extension == null)
        {
            note = "格式无法识别（" + reply.Format + "），未落盘，请检查服务端返回。";
            return false;
        }

        try
        {
            string fileName = "probe_"
                + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                + "_" + reply.Width.ToString(CultureInfo.InvariantCulture)
                + "x" + reply.Height.ToString(CultureInfo.InvariantCulture)
                + "." + extension;

            PlatformFilePath path = new PlatformFilePath(AwakePortraitCache.Root, fileName);
            SaveResult result = FileHelper.SaveFile(path, reply.Bytes);
            if (result != SaveResult.Success)
            {
                note = "落盘失败（" + result + "）：" + FileHelper.GetError();
                AwakeLog.Write("image_probe_save_failed result=" + result + " error=" + FileHelper.GetError());
                return false;
            }

            AwakePortraitCache.LogRootOnce();
            savedPath = path.FileFullPath;
            return true;
        }
        catch (Exception ex)
        {
            note = "落盘异常：" + ex.Message;
            AwakeLog.Write("image_probe_save_error error=" + ex.Message);
            return false;
        }
    }

    /// <summary>认不出的格式返回 null —— 宁可说认不出，也别拿 <c>.png</c> 装一张 jpg。</summary>
    private static string ExtensionFor(string format)
    {
        switch (format)
        {
            case "png": return "png";
            case "jpg": return "jpg";
            case "gif": return "gif";
            case "webp": return "webp";
            case "bmp": return "bmp";
            default: return null;
        }
    }

    private static string FormatSeconds(long milliseconds)
    {
        return (milliseconds / 1000.0).ToString("0.0", CultureInfo.InvariantCulture);
    }
}
