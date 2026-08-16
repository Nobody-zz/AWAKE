using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace Awake;

internal sealed class AwakeContactCardVM : ViewModel
{
    private CharacterImageIdentifierVM _portrait;
    private string _name = string.Empty;
    private string _identity = string.Empty;
    private string _status = string.Empty;
    private string _location = string.Empty;
    private string _canTalkText = string.Empty;
    private bool _canTalk;
    private bool _visible;

    [DataSourceProperty]
    public CharacterImageIdentifierVM Portrait => _portrait;

    [DataSourceProperty]
    public string Name
    {
        get => _name;
        private set => Set(ref _name, value, nameof(Name));
    }

    [DataSourceProperty]
    public string Identity
    {
        get => _identity;
        private set => Set(ref _identity, value, nameof(Identity));
    }

    [DataSourceProperty]
    public string Status
    {
        get => _status;
        private set => Set(ref _status, value, nameof(Status));
    }

    [DataSourceProperty]
    public string Location
    {
        get => _location;
        private set => Set(ref _location, value, nameof(Location));
    }

    [DataSourceProperty]
    public bool CanTalk
    {
        get => _canTalk;
        private set => Set(ref _canTalk, value, nameof(CanTalk));
    }

    [DataSourceProperty]
    public string CanTalkText
    {
        get => _canTalkText;
        private set => Set(ref _canTalkText, value, nameof(CanTalkText));
    }

    [DataSourceProperty]
    public bool Visible
    {
        get => _visible;
        private set => Set(ref _visible, value, nameof(Visible));
    }

    internal AwakeContactCardVM()
    {
        _portrait = null;
        Visible = false;
    }

    internal void Show(AwakeContactInfo contact)
    {
        if (contact == null)
        {
            Visible = false;
            return;
        }
        Name = contact.DisplayName;
        Identity = contact.Identity;
        Status = contact.Status;
        Location = string.IsNullOrWhiteSpace(contact.Location)
            ? AwakeLocalization.Resolve("awake.ui.contact_location_unknown", "位置未知")
            : contact.Location;
        CanTalk = contact.CanTalk;
        CanTalkText = AwakeLocalization.Resolve(
            contact.CanTalk ? "awake.ui.card_can_talk" : "awake.ui.card_cannot_talk",
            contact.CanTalk ? "可以交谈" : "当前无法交谈");
        Visible = true;
        TryBuildPortrait(contact.Target?.Hero);
    }

    internal void Clear()
    {
        Visible = false;
    }

    private void TryBuildPortrait(Hero hero)
    {
        if (hero == null)
        {
            if (_portrait != null)
            {
                _portrait = null;
                OnPropertyChanged(nameof(Portrait));
            }
            return;
        }
        try
        {
            HeroVM heroVm = new HeroVM(hero, false);
            _portrait = heroVm.ImageIdentifier;
            OnPropertyChanged(nameof(Portrait));
        }
        catch
        {
        }
    }

    private bool Set(ref string field, string value, string name)
    {
        value ??= string.Empty;
        if (string.Equals(field, value, System.StringComparison.Ordinal)) return false;
        field = value;
        OnPropertyChangedWithValue(value, name);
        return true;
    }

    private bool Set(ref bool field, bool value, string name)
    {
        if (field == value) return false;
        field = value;
        OnPropertyChangedWithValue(value, name);
        return true;
    }
}
