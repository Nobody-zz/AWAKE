using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Awake;

/// <summary>
/// 画位控件：把"缓存里的哪一张"透给 <see cref="AwakePortraitTextureProvider"/>。
///
/// prefab 里的**标签名＝本类的简单名**（<c>WidgetFactory._builtinTypes[type.Name]</c>），所以写：
/// <code>&lt;AwakePortraitImageWidget PortraitKey="@PortraitKey" /&gt;</code>
/// 属性名必须与 provider 上的同名属性逐字一致——值不是直接赋给 provider，而是走
/// <c>SetTextureProviderProperty</c> 反射进 provider。
///
/// 必须是 <c>public</c>：工厂走 <c>Type.GetConstructor(… BindingFlags.Public …)</c> 再从另一个程序集
/// 创建本类型，internal 会被可见性拦下（症状是实例化失败、只剩一个空 Widget）。
/// 同理，构造签名必须是**单个 <c>UIContext</c> 参数**——工厂就是照这个签名找的。
///
/// provider 名写在构造函数里而不是 prefab 里：让"这个控件用哪个 provider"只在本类出现一次。
/// </summary>
public sealed class AwakePortraitImageWidget : TextureWidget
{
    private string _portraitKey;

    public AwakePortraitImageWidget(UIContext context)
        : base(context)
    {
        TextureProviderName = "AwakePortraitTextureProvider";
    }

    /// <summary>缓存键（见 <see cref="AwakePortraitCache.BuildKey"/>）。置空即回到"无图"。</summary>
    public string PortraitKey
    {
        get { return _portraitKey; }
        set
        {
            if (string.Equals(_portraitKey, value, StringComparison.Ordinal)) return;
            _portraitKey = value;
            // 先塞属性、再要纹理：provider 是懒创建的，顺序反了会白跑一帧。
            SetTextureProviderProperty("PortraitKey", value);
            RefreshState();
        }
    }
}
