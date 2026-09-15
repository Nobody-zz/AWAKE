using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace Awake;

internal sealed class AwakeContactRowVM : ViewModel
{
    private readonly AwakeContactInfo _contact;
    private readonly Action _onSelect;
    private CharacterImageIdentifierVM _portrait;
    private bool _portraitResolved;
    private bool _isSelected;

    internal AwakeContactInfo Contact => _contact;

    [DataSourceProperty]
    public string DisplayName => _contact.DisplayName;

    [DataSourceProperty]
    public string CanonicalContactKey => _contact.CanonicalContactKey;

    [DataSourceProperty]
    public string Identity => _contact.Identity;

    [DataSourceProperty]
    public string Status => _contact.Status;

    [DataSourceProperty]
    public string Location => _contact.Location;

    [DataSourceProperty]
    public bool IsNearby => _contact.IsNearby;

    [DataSourceProperty]
    public bool CanTalk => _contact.CanTalk;

    [DataSourceProperty]
    public string StatusColor => _contact.IsNearby ? "#FF88CC88" : "#FF888888";

    /// <summary>
    /// 名册条目左侧的 40×44 原生肖像（对话页三列版的左列用）。
    /// **按需建**：只在真的被 Prefab 绑定时才构造 <see cref="HeroVM"/>——信使面板的条目
    /// 不绑它，因此那边一个都不会多建。（构造 hero VM 有成本，不能无条件铺。）
    /// </summary>
    [DataSourceProperty]
    public CharacterImageIdentifierVM Portrait
    {
        get
        {
            if (!_portraitResolved)
            {
                _portraitResolved = true;
                try
                {
                    Hero hero = _contact?.Target?.Hero;
                    if (hero != null) _portrait = new HeroVM(hero, false).ImageIdentifier;
                }
                catch (Exception ex)
                {
                    AwakeLog.Write("awake_contact_row_portrait_error error=" + ex.Message);
                }
            }
            return _portrait;
        }
    }

    /// <summary>是否当前选中的那一位（左列高亮）。由宿主 VM 推，不在本类里判断。</summary>
    [DataSourceProperty]
    public bool IsSelected => _isSelected;

    internal void SetSelected(bool value)
    {
        if (_isSelected == value) return;
        _isSelected = value;
        OnPropertyChangedWithValue(value, nameof(IsSelected));
    }

    internal AwakeContactRowVM(AwakeContactInfo contact, Action onSelect)
    {
        _contact = contact ?? new AwakeContactInfo(
            null,
            AwakeLocalization.Resolve("awake.ui.contact_unknown", "未知"),
            string.Empty,
            AwakeLocalization.Resolve("awake.ui.contact_unavailable", "不可用"),
            false,
            false,
            string.Empty);
        _onSelect = onSelect;
    }

    public void ExecuteSelect()
    {
        try
        {
            _onSelect?.Invoke();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_messenger_contact_select_error error=" + ex.Message);
        }
    }
}
