using System;
using MCM.Common;

namespace Awake;

/// <summary>
/// 出图接口的**请求形状**——进来什么、出去什么。
///
/// 刻意不叫厂商名以外的东西：形状相同就能互换，形状不同才需要再加一个适配器。
/// 换服务商 / 换模型 / 从自建中转切到本机 App，全都是改配置，调用方一行不动。
/// </summary>
internal enum AwakeImageShape
{
    /// <summary>Player2：base64 进、base64 出。无参考图走 /image/generate，有参考图走 /image/edit。</summary>
    Player2 = 0,

    /// <summary>OpenAI 兼容的 /images/generations（绝大多数自建中转也暴露这一形状）。</summary>
    OpenAiCompatible = 1
}

/// <summary>
/// 一次出图要用的全部连接信息。**只讲"往哪儿发、要不要带钥匙"，不含任何厂商硬编码。**
/// </summary>
internal sealed class AwakeImageEndpoint
{
    internal AwakeImageEndpoint(AwakeImageShape shape, string baseUrl, bool isCloud, string credentialReference)
    {
        Shape = shape;
        BaseUrl = baseUrl ?? string.Empty;
        IsCloud = isCloud;
        CredentialReference = credentialReference ?? string.Empty;
    }

    internal AwakeImageShape Shape { get; }

    /// <summary>归一化后的 API 根地址。子路径由形状适配器拼，这里不带尾巴。</summary>
    internal string BaseUrl { get; }

    /// <summary>云端才带凭据；本机免认证服务（本机 App / 本机推理）不带。</summary>
    internal bool IsCloud { get; }

    /// <summary>密钥在本机保护存储里的槽位名。只有 <see cref="IsCloud"/> 为真时才用它。</summary>
    internal string CredentialReference { get; }

    internal string ShapeLabel
    {
        get { return AwakeImageEndpointResolver.ShapeLabel(Shape); }
    }

    /// <summary>写给日志用的一行。**绝不包含密钥本身。**</summary>
    internal string Describe()
    {
        return "shape=" + AwakeImageEndpointResolver.ShapeId(Shape)
            + " base_url=" + BaseUrl
            + " cloud=" + (IsCloud ? "true" : "false")
            + " credential=" + (IsCloud && CredentialReference.Length > 0 ? CredentialReference : "none");
    }
}

/// <summary>
/// 把 MCM 里那两栏（地址 + 云端开关）解析成一份可用的出图端点。
///
/// **为什么生图是独立一组配置，而不是复用 AI 链路那组**——不是洁癖，是分层硬约束：
///   模组进程（net472）只能引用 <c>MarcusAwakeFramework</c>；
///   凭据存储与出网实现都在 net8 的独立进程里（<c>MarcusAwakeProvider</c> / <c>MarcusAwakeRuntimeService</c>）。
///   ⇒ 模组进程**读不到 AI 链路那把 Key**，也没有任何回读接口。
///   所以模组侧要自己出网，就必须有自己的地址 + 自己的钥匙。
///   共用一栏只会造出"地址跟着走了、钥匙拿不到"这种半吊子。
/// </summary>
internal static class AwakeImageEndpointResolver
{
    /// <summary>生图密钥在本机保护存储（模组侧 DPAPI 文件）里的槽位名。</summary>
    internal const string CredentialReference = "awake.image.default";

    private static readonly string[] ShapeLabels = { "Player2", "OpenAI 兼容" };
    private static readonly string[] ShapeIds = { "player2", "openai_compatible" };

    internal static string ShapeLabel(AwakeImageShape shape)
    {
        return ShapeLabels[ShapeIndex(shape)];
    }

    internal static string ShapeId(AwakeImageShape shape)
    {
        return ShapeIds[ShapeIndex(shape)];
    }

    private static int ShapeIndex(AwakeImageShape shape)
    {
        int index = (int)shape;
        return index < 0 || index >= ShapeLabels.Length ? 0 : index;
    }

    internal static AwakeImageShape ResolveShape(AwakeConfig config)
    {
        int selected = config == null || config.PortraitImageShape == null
            ? 0
            : config.PortraitImageShape.SelectedIndex;
        return selected == 1 ? AwakeImageShape.OpenAiCompatible : AwakeImageShape.Player2;
    }

    internal static Dropdown<string> CreateShapeDropdown()
    {
        return new Dropdown<string>(ShapeLabels, 0);
    }

    /// <summary>
    /// 解析生效端点。地址为空、或不是合法的 HTTP(S) 绝对地址 ⇒ 失败并给出人话原因。
    /// 地址允许直接粘完整接口地址（归一化会剥掉尾部子路径），见 ProviderEndpointSuffixes。
    /// </summary>
    internal static bool TryResolve(AwakeConfig config, out AwakeImageEndpoint endpoint, out string error)
    {
        endpoint = null;
        error = string.Empty;

        if (config == null)
        {
            error = "AWAKE 配置尚未加载。";
            return false;
        }

        string raw = (config.PortraitImageBaseUrl ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            error = "出图服务地址为空。请填写出图接口的 API 根地址；"
                + "本机 Player2 App 填 http://127.0.0.1:4315/v1。";
            return false;
        }

        if (!AwakeProviderConfiguration.TryNormalizeProviderBaseUrl(raw, out string baseUrl, out error))
        {
            return false;
        }

        endpoint = new AwakeImageEndpoint(
            ResolveShape(config),
            baseUrl,
            config.PortraitImageIsCloud,
            CredentialReference);
        return true;
    }
}
