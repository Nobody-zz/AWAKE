using System;
using TaleWorlds.Library;

namespace Awake;

internal sealed class AwakeContactRowVM : ViewModel
{
    private readonly AwakeContactInfo _contact;
    private readonly Action _onSelect;

    internal AwakeContactInfo Contact => _contact;

    [DataSourceProperty]
    public string DisplayName => _contact.DisplayName;

    [DataSourceProperty]
    public string Identity => _contact.Identity;

    [DataSourceProperty]
    public string Status => _contact.Status;

    [DataSourceProperty]
    public bool IsNearby => _contact.IsNearby;

    [DataSourceProperty]
    public string StatusColor => _contact.IsNearby ? "#FF88CC88" : "#FF888888";

    internal AwakeContactRowVM(AwakeContactInfo contact, Action onSelect)
    {
        _contact = contact ?? new AwakeContactInfo(null, "未知", string.Empty, "不可用", false);
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
