using System;
using System.Globalization;
using TaleWorlds.GauntletUI;
using TaleWorlds.TwoDimension;
using EngineTextureRef = TaleWorlds.Engine.Texture;
using UiTexture = TaleWorlds.TwoDimension.Texture;
using TextureBridge = TaleWorlds.Engine.GauntletUI.EngineTexture;

namespace Awake;

/// <summary>
/// 把本地缓存里的一张 png 变成控件能吃的一张纹理。
///
/// **注册是零动作的**：<c>TextureProviderFactory.RefreshProviderTypes()</c> 扫
/// <c>AppDomain.CurrentDomain.GetAssemblies()</c>（无程序集过滤）、按**类简单名**收录所有非抽象
/// <c>TextureProvider</c> 子类；而 Awake.dll 在它刷新时必定已在册（Module.LoadSubModules 两段式）。
/// 代价是**类名必须全局唯一**——简单名撞车会让 `Dictionary.Add` 抛异常，把整批 provider 注册一起带下水。
/// 所以类名带模块前缀，一个字都别省。
///
/// 必须是 <c>public</c>：工厂走 <c>Activator.CreateInstance(type)</c>，而反射创建
/// 另一个程序集里的 internal 类型会被可见性拦下。本文件是全项目少数几个故意不写 internal 的地方。
///
/// 两个同名的 Texture 必须显式区分（官方反编译出来那份 OnlineImageTextureProvider 在这点上裸用
/// <c>Texture</c>，**照抄语义、别照抄文本**，抄了编不过）：
///   TaleWorlds.Engine.Texture        —— 引擎对象，CreateTextureFromPath 的产物
///   TaleWorlds.TwoDimension.Texture  —— 控件消费的那个，包一层 ITexture
/// </summary>
public sealed class AwakePortraitTextureProvider : TextureProvider
{
    /// <summary>只在"要落盘文件还没出现"时轮询，别把每帧都变成一次磁盘探测。</summary>
    private const float RetryIntervalSeconds = 0.25f;

    /// <summary>轮询窗口。文件迟早会出现（出图是异步的），但窗口要有限，别变成永久轮询。</summary>
    private const float RetryWindowSeconds = 60f;

    private readonly object _gate = new object();
    private string _portraitKey;
    private EngineTextureRef _engineTexture;
    private UiTexture _provided;
    private float _retryElapsed;
    private float _retrySinceLastAttempt;
    private bool _reportedMissing;

    /// <summary>
    /// 缓存键。**属性名必须与 AwakePortraitImageWidget 上的同名属性逐字一致**——
    /// 值是靠 <c>TextureWidget.SetTextureProviderProperty</c> 反射塞进来的。
    /// </summary>
    public string PortraitKey
    {
        get { return _portraitKey; }
        set
        {
            string incoming = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            lock (_gate)
            {
                if (string.Equals(_portraitKey, incoming, StringComparison.Ordinal)) return;
                _portraitKey = incoming;
                // 换图先摘掉旧纹理：不摘的话控件会继续画上一张，等新图就绪才跳变。
                _provided = null;
                _engineTexture = null;
                _reportedMissing = false;
                _retryElapsed = 0f;
                _retrySinceLastAttempt = RetryIntervalSeconds;
            }

            AwakePortraitCache.LogRootOnce();
            TryLoad();
        }
    }

    /// <summary>
    /// 无图时返回 <c>null</c>——控件据此不画，正好就是"生成中／占位"的表现，
    /// 不需要额外做一个空纹理。这里不做重活，加载放在 setter 与 <see cref="Tick"/>。
    /// </summary>
    protected override UiTexture OnGetTextureForRender(TwoDimensionContext twoDimensionContext, string name)
    {
        if (_provided != null) return _provided;
        TryLoad();
        return _provided;
    }

    public override void Tick(float dt)
    {
        base.Tick(dt);
        if (_provided != null) return;

        lock (_gate)
        {
            if (_portraitKey == null) return;
            _retryElapsed += dt;
            _retrySinceLastAttempt += dt;
            if (_retryElapsed > RetryWindowSeconds) return;
            if (_retrySinceLastAttempt < RetryIntervalSeconds) return;
            _retrySinceLastAttempt = 0f;
        }

        TryLoad();
    }

    /// <summary>
    /// 这里**故意不调** <c>Engine.Texture.Release()</c> / <c>ReleaseImmediately()</c>。
    ///
    /// 一手依据：本机反编译的官方 14 个 provider 里，没有任何一个调过 Release；
    /// 唯一按路径加载 png 的 <c>OnlineImageTextureProvider</c> 连 <c>Clear</c> 都不重写。
    /// tableau 那一族走的是自己那个 tableau 对象的 <c>OnFinalize()</c>，与文件纹理不是一条路。
    /// 而 <c>Release()</c> 内部第一句就摸 <c>RenderTargetComponent</c>——对一张普通文件纹理它是否为
    /// null 没验过，赌错的代价是在"关面板"这条最容易忽略的路径上抛异常。
    /// 所以本批照官方同款：**只摘引用**。
    ///
    /// 留待补验：反复换图是否会攒下未释放的 GPU 纹理（见 PLAN-AI-PORTRAIT-IMAGE-20260913.md 未验清单）。
    /// </summary>
    public override void Clear(bool clearNextFrame)
    {
        base.Clear(clearNextFrame);
        lock (_gate)
        {
            _provided = null;
            _engineTexture = null;
        }
    }

    private void TryLoad()
    {
        string key;
        lock (_gate)
        {
            if (_provided != null || _portraitKey == null) return;
            key = _portraitKey;
        }

        string absolutePath;
        long byteLength;
        if (!AwakePortraitCache.TryResolveExisting(key, out absolutePath, out byteLength))
        {
            // 只压日志，不落任何"放弃"标志——文件是异步出现的，后面几轮还得继续找。
            bool firstReport;
            lock (_gate)
            {
                firstReport = !_reportedMissing;
                _reportedMissing = true;
            }
            if (firstReport) AwakeLog.Write("portrait_texture_pending key=" + key);
            return;
        }

        try
        {
            // 官方一手用例：png 落盘 → CreateTextureFromPath。
            // 喂进去的是缓存自己算出来的路径对象，不另外拼字符串，免得和引擎的路径解析分叉。
            EngineTextureRef engine = EngineTextureRef.CreateTextureFromPath(AwakePortraitCache.PathFor(key));
            if (engine == null)
            {
                lock (_gate) { _reportedMissing = true; }
                AwakeLog.Write("portrait_texture_null key=" + key + " bytes=" + byteLength.ToString(CultureInfo.InvariantCulture));
                return;
            }

            UiTexture provided = new UiTexture(new TextureBridge(engine));
            lock (_gate)
            {
                _engineTexture = engine;
                _provided = provided;
                _reportedMissing = false;
            }
            AwakeLog.Write("portrait_texture_ready key=" + key
                + " bytes=" + byteLength.ToString(CultureInfo.InvariantCulture)
                + " size=" + engine.Width.ToString(CultureInfo.InvariantCulture)
                + "x" + engine.Height.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            lock (_gate) { _reportedMissing = true; }
            AwakeLog.Write("portrait_texture_load_failed key=" + key + " error=" + ex.Message);
        }
    }
}
