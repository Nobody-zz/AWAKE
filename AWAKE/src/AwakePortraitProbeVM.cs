using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace Awake;

/// <summary>
/// 画位探测面板的数据源。
///
/// 存在的理由只有一个：把「png 落盘 → 运行时纹理 → 控件上屏」这条链在**真实生产代码**里证死——
/// 出图后端还没接，这条链就得先能单独跑。面板打开时若缓存是空的，按「放一张测试图」
/// 会写进一张内置占位图（212×360，与 07 稿画位同尺寸），立刻就能看到图。
/// </summary>
internal sealed class AwakePortraitProbeVM : ViewModel
{
    private readonly Action _close;
    private readonly Action _openFolder;
    private readonly List<string> _keys = new List<string>();
    private int _index = -1;
    private string _portraitKey;
    private string _locationText;
    private string _statusText;
    private string _detailText;

    /// <summary>绑到 AwakePortraitImageWidget.PortraitKey——名字必须逐字一致。</summary>
    [DataSourceProperty]
    public string PortraitKey
    {
        get { return _portraitKey; }
        private set
        {
            if (string.Equals(_portraitKey, value, StringComparison.Ordinal)) return;
            _portraitKey = value;
            OnPropertyChangedWithValue(value, nameof(PortraitKey));
        }
    }

    [DataSourceProperty]
    public string TitleText { get { return "画位探测 · NPC 全身像"; } }

    [DataSourceProperty]
    public string LocationText
    {
        get { return _locationText; }
        private set
        {
            if (string.Equals(_locationText, value, StringComparison.Ordinal)) return;
            _locationText = value;
            OnPropertyChangedWithValue(value, nameof(LocationText));
        }
    }

    [DataSourceProperty]
    public string StatusText
    {
        get { return _statusText; }
        private set
        {
            if (string.Equals(_statusText, value, StringComparison.Ordinal)) return;
            _statusText = value;
            OnPropertyChangedWithValue(value, nameof(StatusText));
        }
    }

    [DataSourceProperty]
    public string DetailText
    {
        get { return _detailText; }
        private set
        {
            if (string.Equals(_detailText, value, StringComparison.Ordinal)) return;
            _detailText = value;
            OnPropertyChangedWithValue(value, nameof(DetailText));
        }
    }

    [DataSourceProperty]
    public string CloseText { get { return "关闭"; } }

    [DataSourceProperty]
    public string ReloadText { get { return "重扫目录"; } }

    [DataSourceProperty]
    public string SeedText { get { return "放一张测试图"; } }

    [DataSourceProperty]
    public string OpenFolderText { get { return "打开目录"; } }

    [DataSourceProperty]
    public string PreviousText { get { return "上一张"; } }

    [DataSourceProperty]
    public string NextText { get { return "下一张"; } }

    internal AwakePortraitProbeVM(Action close, Action openFolder)
    {
        _close = close;
        _openFolder = openFolder;
        LocationText = AwakePortraitCache.AbsoluteRootDirectory;
        Reload();
    }

    /// <summary>重新扫描缓存目录，并把画位切到第一张（没有就切空）。</summary>
    internal void Reload()
    {
        AwakePortraitCache.LogRootOnce();
        _keys.Clear();
        _keys.AddRange(AwakePortraitCache.ListKeys());
        _index = _keys.Count > 0 ? 0 : -1;
        ApplyCurrent("已重扫目录");
    }

    public void ExecuteClose()
    {
        _close?.Invoke();
    }

    public void ExecuteReload()
    {
        Reload();
    }

    public void ExecutePrevious()
    {
        if (_keys.Count == 0) return;
        _index = _index <= 0 ? _keys.Count - 1 : _index - 1;
        ApplyCurrent("上一张");
    }

    public void ExecuteNext()
    {
        if (_keys.Count == 0) return;
        _index = _index < 0 || _index >= _keys.Count - 1 ? 0 : _index + 1;
        ApplyCurrent("下一张");
    }

    /// <summary>把内置占位图写进缓存——没有出图后端也要能看见东西。</summary>
    public void ExecuteSeedFixture()
    {
        string key = AwakePortraitCache.BuildKey("dev.probe", "内置占位图", AwakePortraitProbeFixture.Width, AwakePortraitProbeFixture.Height, 0L);
        byte[] bytes = Convert.FromBase64String(AwakePortraitProbeFixture.PngBase64);
        string path = AwakePortraitCache.Save(key, bytes);
        if (string.IsNullOrEmpty(path))
        {
            StatusText = "写占位图失败，看 Logs/Awake.log 的 portrait_cache_save_* 那几行。";
            return;
        }

        Reload();
        // Reload 会把画位切到第一张；占位图按名字排序未必是它，这里直接指定。
        _index = _keys.IndexOf(key);
        if (_index < 0) _index = 0;
        ApplyCurrent("占位图已落盘 " + bytes.Length + " B");
    }

    public void ExecuteOpenFolder()
    {
        _openFolder?.Invoke();
    }

    private void ApplyCurrent(string action)
    {
        string key = _index >= 0 && _index < _keys.Count ? _keys[_index] : null;

        // 先清空再赋值：provider 只在"键变了"时才重载，
        // 同一个键重新出图（或被手工覆盖）必须让它看见一次变化。
        PortraitKey = null;
        PortraitKey = key;

        if (key == null)
        {
            StatusText = action + "：缓存里还没有图。";
            DetailText = "按「放一张测试图」立刻验证上屏链路；也可以把 png 按 portrait_<键>.png 丢进上面的目录。";
            return;
        }

        StatusText = action + "：" + ( _index + 1) + " / " + _keys.Count;
        DetailText = "键 " + key + "\n文件 " + AwakePortraitCache.DescribeKey(key);
    }
}
