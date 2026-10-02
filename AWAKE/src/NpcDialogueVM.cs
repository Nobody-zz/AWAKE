using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Awake;

internal sealed class NpcDialogueVM : ViewModel
{
    /// <summary>立绘请求上下文的预算。比出网超时（<c>AwakeImageClient</c> 的 120s）留出余量。</summary>
    private static readonly TimeSpan PortraitContextBudget = TimeSpan.FromSeconds(180);

    private readonly NpcDialogueService _service;
    private readonly Action _close;
    private readonly MBBindingList<NpcDialogueChatRowVM> _chatRows = new MBBindingList<NpcDialogueChatRowVM>();
    // 三列版（稿 07）左列「在场」名册 + 中列信息区。数据源与信使面板同一条
    // （AwakeMessengerService.BuildContacts），复用同一对 VM 类型 ⇒ 两个面板长得一样。
    private readonly MBBindingList<AwakeContactRowVM> _presentRows = new MBBindingList<AwakeContactRowVM>();
    private readonly AwakeContactCardVM _selectedCard = new AwakeContactCardVM();
    private string _selectedContactKey = string.Empty;

    // ---------------- 中列画位：AI 全身像（宿主 VM） ----------------
    //
    // 这是 AwakePortraitSlot.xml 的第一处真实宿主。画位控件本身早就写好了，缺的一直是
    // 「谁来提供那 6 个开关 + 7 段文案 + 1 个方法」——就是下面这一组。
    //
    // 出图这条链走的全是模组已有的东西，本文件只做**编排**，一行网络代码都不新写：
    //   AwakeImageEndpointResolver（找端点）→ AwakeImageSecretStore（取钥匙）
    //   → AwakePortraitCache.BuildKey（幂等键）→ AwakeImageClient.GenerateAsync（真发）
    //   → AwakePortraitCache.Save（落盘）→ PortraitKey（上屏，纹理由 provider 去读）
    //
    // 三条不可退让的约束（照 AwakeImageProbe 的既有口径）：
    //   · 网络只在后台线程发，**绝不在 VM 命令里同步等**；
    //   · 落盘前必须确认**真是 png** —— 缓存文件名写死 .png、纹理按路径加载，
    //     拿 jpg 去冒充 png 会变成"文件在、纹理空"这种最难查的形态；
    //   · 日志只记 npc/形状/状态码/耗时/大小，**不记提示词原文与钥匙**。
    private bool _portraitGenerating;
    private bool _portraitPresent;
    private bool _portraitFailed;
    private string _portraitKey;
    private string _portraitBadge = string.Empty;
    private string _portraitError = string.Empty;
    private string _portraitNpcKey = string.Empty;

    /// <summary>进程级出图闸：出图是重活，同时只许一发（跨面板共用）。</summary>
    private static int _portraitGate;

    private string _titleText;
    private string _statusText = string.Empty;
    private string _noticeText;
    private string _inputText = string.Empty;
    private string _streamingText = string.Empty;
    private bool _isLoading;
    private bool _closed;

    /// <summary>
    /// P1-3：本次发送的取消源。关面板、或玩家又发了一条时取消它，让这一回合的**前置阶段**
    /// （存储读、状态刷新、提示词构建）当场停下，而不是任它跑完。
    /// 服务侧 `SendAsync` 对取消已有完整路径
    /// （`catch (OperationCanceledException)` → `ClearActive()` → 返回 `npc_dialogue.cancelled`），
    /// 所以这里只负责把取消信号递进去、再兜住"取消不是错误"。
    /// ⚠ 与 `AwakeMessengerVM` 对齐：信使面板一开始就往 `SendAsync` 传会话 token（`AwakeMessengerVM.cs:788`），
    /// 本面板原来传 `CancellationToken.None`，两侧不一致。
    /// </summary>
    private CancellationTokenSource _sendCts;

    [DataSourceProperty]
    public string TitleText
    {
        get => _titleText;
        private set => Set(ref _titleText, value, nameof(TitleText));
    }

    [DataSourceProperty]
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value, nameof(StatusText));
    }

    [DataSourceProperty]
    public string NoticeText
    {
        get => _noticeText;
        private set => Set(ref _noticeText, value, nameof(NoticeText));
    }

    [DataSourceProperty]
    public string InputText
    {
        get => _inputText;
        set
        {
            if (Set(ref _inputText, value, nameof(InputText)))
            {
                OnPropertyChangedWithValue(CanSend, nameof(CanSend));
                OnPropertyChanged(nameof(IsInputEmpty));
            }
        }
    }

    [DataSourceProperty]
    public string StreamingText
    {
        get => _streamingText;
        private set => Set(ref _streamingText, value, nameof(StreamingText));
    }

    [DataSourceProperty]
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (Set(ref _isLoading, value, nameof(IsLoading)))
            {
                OnPropertyChangedWithValue(CanSend, nameof(CanSend));
            }
        }
    }

    [DataSourceProperty]
    public bool CanSend => _service != null && _service.IsAvailable && !_isLoading && !string.IsNullOrWhiteSpace(_inputText);

    [DataSourceProperty]
    public bool CanSelectActionMode => _service != null && !_service.IsSceneShout;

    [DataSourceProperty]
    public bool CanChangeActionMode => CanSelectActionMode && _service.CanChangeActionMode;

    [DataSourceProperty]
    public bool IsChatMode => _service == null || _service.ActionMode == NpcDialogueActionMode.Chat;

    [DataSourceProperty]
    public bool IsNegotiationMode => _service != null && _service.ActionMode == NpcDialogueActionMode.Negotiation;

    // 输入框占位提示：空输入时叠加显示（Gauntlet 绑定仅正向路径）。
    [DataSourceProperty]
    public bool IsInputEmpty => string.IsNullOrWhiteSpace(_inputText);

    [DataSourceProperty]
    public string InputHintText => AwakeLocalization.Resolve("awake.ui.input_hint", "写下要说的话…（回车发送）");

    [DataSourceProperty]
    public string ChatModeText => AwakeLocalization.Resolve("awake.ui.chat_mode", "闲聊");

    [DataSourceProperty]
    public string NegotiationModeText => AwakeLocalization.Resolve("awake.ui.negotiation_mode", "交涉");

    [DataSourceProperty]
    public string SendButtonText => AwakeLocalization.Resolve(
        _service != null && _service.IsSceneShout ? "awake.scene_shout.send" : "awake.ui.talk",
        _service != null && _service.IsSceneShout ? "喊话" : "交谈");

    [DataSourceProperty]
    public string CloseButtonText => AwakeLocalization.Resolve("awake.ui.close", "离开");

    [DataSourceProperty]
    public MBBindingList<NpcDialogueChatRowVM> ChatRows => _chatRows;

    // ---------------- 三列版（稿 07）：左列名册 + 中列信息区 ----------------

    [DataSourceProperty]
    public MBBindingList<AwakeContactRowVM> PresentRows => _presentRows;

    [DataSourceProperty]
    public AwakeContactCardVM SelectedCard => _selectedCard;

    /// <summary>左列列头右侧的计数（Gauntlet 不支持表达式，算好再发）。</summary>
    [DataSourceProperty]
    public string PresentCountText =>
        AwakeLocalization.Resolve(
            "awake.ui.present_count",
            "共 " + _presentRows.Count + " 人",
            new System.Collections.Generic.Dictionary<string, string>
            {
                ["COUNT"] = _presentRows.Count.ToString()
            });

    // Gauntlet 绑定只认正向 bool ⇒ 空/非空两个方向都要有。
    [DataSourceProperty]
    public bool HasPresentRows => _presentRows.Count > 0;

    [DataSourceProperty]
    public bool IsPresentRowsEmpty => _presentRows.Count == 0;

    [DataSourceProperty]
    public string PresentTitle => AwakeLocalization.Resolve("awake.ui.scene_present", "在场");

    [DataSourceProperty]
    public string EmptyPresentText =>
        AwakeLocalization.Resolve("awake.ui.scene_present_empty", "附近没有别人。");

    /// <summary>中列画位的标签（AwakePortraitSlot 的 ① 空位层）。</summary>
    [DataSourceProperty]
    public string SlotLabelText =>
        AwakeLocalization.Resolve("awake.ui.portrait_slot_label", "全 身 像");

    /// <summary>旧名，留着不删：三列版早期中列用过它。新结构用 <see cref="SlotLabelText"/>。</summary>
    [DataSourceProperty]
    public string PortraitSlotLabel => SlotLabelText;

    // ---- 画位的六个开关 ----
    // ⚠️ Gauntlet 绑定只认正向 bool，每个可见性都要有自己的属性，不能靠取反。
    // 分派（照 UI-PORTRAIT-SLOT-CONTRACT §2 的状态表）：
    //   生成中  → 只 ShowGenerating，两颗按钮都为 false（防重复触发），底下的层保持原样 ⇒ 不闪
    //   空位    → ShowEmptySlot + ShowCenteredButton
    //   已生成  → ShowPortrait + ShowRegenerateButton
    //   无图失败→ 仍 ShowEmptySlot + ShowCenteredButton（文案换成「重新生成」）+ ShowError
    //   有图失败→ ShowPortrait + ShowRegenerateButton + ShowError
    // ⚠️ ShowRegenerateButton **不是** ShowPortrait 的别名：有旧图时 ShowPortrait 仍为真，
    //    用它会让按钮在生成中露出来。

    [DataSourceProperty]
    public bool ShowGenerating => _portraitGenerating;

    [DataSourceProperty]
    public bool ShowPortrait => _portraitPresent;

    [DataSourceProperty]
    public bool ShowEmptySlot => !_portraitPresent;

    [DataSourceProperty]
    public bool ShowError => !_portraitGenerating && _portraitFailed;

    [DataSourceProperty]
    public bool ShowCenteredButton => !_portraitGenerating && !_portraitPresent;

    [DataSourceProperty]
    public bool ShowRegenerateButton => !_portraitGenerating && _portraitPresent;

    // ---- 画位的七段文案 ----

    /// <summary>缓存键。**键变了 provider 才重载**，所以换图必须先发一次 null（见 ApplyPortrait）。</summary>
    [DataSourceProperty]
    public string PortraitKey => _portraitKey;

    [DataSourceProperty]
    public string PortraitBadgeText => _portraitBadge;

    [DataSourceProperty]
    public string GeneratingText =>
        AwakeLocalization.Resolve("awake.ui.portrait_generating", "正在生成全身像…");

    [DataSourceProperty]
    public string PortraitErrorText => _portraitError;

    [DataSourceProperty]
    public string GenerateButtonText => _portraitFailed
        ? AwakeLocalization.Resolve("awake.ui.portrait_regenerate", "重新生成")
        : AwakeLocalization.Resolve("awake.ui.portrait_generate", "生成全身像");

    [DataSourceProperty]
    public string RegenerateButtonText =>
        AwakeLocalization.Resolve("awake.ui.portrait_regenerate", "重新生成");

    [DataSourceProperty]
    public string EndButtonText => AwakeLocalization.Resolve("awake.ui.end", "结束");

    internal NpcDialogueVM(NpcDialogueService service, Action close)
    {
        _service = service;
        _close = close;
        _titleText = service.DisplayTitle;
        _noticeText = AwakeLocalization.Resolve(
            service.IsSceneShout ? "awake.scene_shout.notice" : "awake.ui.notice_opening",
            service.IsSceneShout ? "你向场景喊了一句，声音在人群里传开。" : "对方似乎有话想对你说。");
        _statusText = AwakeLocalization.Resolve(
            service.IsSceneShout ? "awake.scene_shout.status_starting" : "awake.ui.status_starting",
            service.IsSceneShout ? "场景正在回应……" : "对话正在苏醒……");
        AddChatRow(service.SpeakerName, _noticeText);
        RefreshPresentRows();
    }

    /// <summary>
    /// 建一次「在场」名册，并把正在对话的那位默认选中。
    /// 只建一次：本页的生命周期短（打开一次对话），名单中途不刷 —— 刷新会让左列滚动位置与选中态一起跳。
    /// </summary>
    private void RefreshPresentRows()
    {
        if (_presentRows.Count > 0) return;
        try
        {
            foreach (AwakeContactInfo info in AwakeMessengerService.BuildContacts())
            {
                AwakeContactInfo captured = info;
                _presentRows.Add(new AwakeContactRowVM(captured, () => SelectPresent(captured)));
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_vm_present_rows_error error=" + ex.Message);
        }

        AwakeContactInfo match = null;
        foreach (AwakeContactRowVM row in _presentRows)
        {
            if (string.Equals(row.DisplayName, _service?.SpeakerName, StringComparison.Ordinal))
            {
                match = row.Contact;
                break;
            }
        }
        if (match == null && _presentRows.Count > 0) match = _presentRows[0].Contact;
        SelectPresent(match);

        OnPropertyChanged(nameof(HasPresentRows));
        OnPropertyChanged(nameof(IsPresentRowsEmpty));
        OnPropertyChanged(nameof(PresentCountText));
    }

    /// <summary>
    /// 换一个选中的人：只换「信息区」显示谁，**不换对话对象**。
    /// 真正把对话切到另一个人需要重建 NpcDialogueService（它是按 heroId 构造的），
    /// 属于下一步；在此之前点击的语义＝"看看这个人是谁"。
    /// </summary>
    private void SelectPresent(AwakeContactInfo contact)
    {
        _selectedContactKey = contact?.CanonicalContactKey ?? string.Empty;
        foreach (AwakeContactRowVM row in _presentRows)
        {
            row.SetSelected(string.Equals(row.CanonicalContactKey, _selectedContactKey,
                                         StringComparison.Ordinal));
        }
        if (contact == null)
        {
            _selectedCard.Clear();
            RefreshPortraitSlot(null);
            return;
        }
        _selectedCard.Show(contact);
        RefreshPortraitSlot(contact);
    }

    // ==================== 中列画位：AI 全身像 ====================

    /// <summary>
    /// ⚠️ **立绘提示词 —— 全项目唯一那道留给甲方的口子。**
    ///
    /// 甲方 09-15 明示「先别动提示词，我还没做」⇒ 这里**故意返回空串**，不擅自编一个风格词。
    /// 空串的后果是被 <see cref="ExecuteGeneratePortrait"/> 拦下并显示「提示词还没定」，
    /// **不会拿占位词去发请求**——出图是花钱的重活，宁可不动。
    ///
    /// 接入时要守的两条（否则会踩缓存）：
    ///   1. 只能依赖「这个 NPC 是谁」这类**事实**，别掺时间戳/随机数；
    ///      缓存键 = npcKey ＋ 提示词 ＋ 尺寸 ＋ 种子，提示词一变就是另一张图、旧图仍留盘上。
    ///   2. 返回空串/空白 ⇒ 画位停用（就是现在的状态）。
    ///
    /// 上下游都不用改：填好这里（或改成读 AwakeConfig 的字段）即可。
    /// </summary>
    private static string BuildPortraitPrompt(AwakeContactInfo contact)
    {
        return string.Empty;
    }

    /// <summary>换人／开面板时重置画位：先看这位有没有现成的图，没有就摆空位。</summary>
    private void RefreshPortraitSlot(AwakeContactInfo contact)
    {
        _portraitFailed = false;
        _portraitError = string.Empty;
        _portraitPresent = false;
        _portraitKey = null;
        _portraitBadge = string.Empty;
        _portraitNpcKey = contact == null ? string.Empty : PortraitIdentityOf(contact);

        if (contact != null)
        {
            string prompt = BuildPortraitPrompt(contact);

            // 提示词还没定 ⇒ 不查缓存（键算不出来），直接摆空位。
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                int width;
                int height;
                ResolvePortraitSize(out width, out height);
                string key = AwakePortraitCache.BuildKey(_portraitNpcKey, prompt, width, height, 0L);

                string absolutePath;
                long byteLength;
                if (AwakePortraitCache.TryResolveExisting(key, out absolutePath, out byteLength))
                {
                    _portraitPresent = true;
                    _portraitKey = key;
                    _portraitBadge = ComposePortraitBadge(key);
                }
            }
        }

        NotifyPortraitSlot();
    }

    /// <summary>画位按钮的唯一命令。空位时是「生成全身像」，有图时是「重新生成」——同一颗按钮。</summary>
    public void ExecuteGeneratePortrait()
    {
        if (_closed || _portraitGenerating) return;

        AwakeContactInfo contact = FindSelectedContact();
        if (contact == null)
        {
            FailPortrait(AwakeLocalization.Resolve("awake.ui.portrait_no_target", "还没选中要给谁画像。"));
            return;
        }

        string prompt = BuildPortraitPrompt(contact);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            AwakeLog.Write("dialogue_portrait_prompt_missing npc=" + _portraitNpcKey);
            FailPortrait(AwakeLocalization.Resolve("awake.ui.portrait_prompt_missing",
                "立绘提示词还没定下来，先把它写上。"));
            return;
        }

        int width;
        int height;
        ResolvePortraitSize(out width, out height);
        string npcKey = PortraitIdentityOf(contact);
        string key = AwakePortraitCache.BuildKey(npcKey, prompt, width, height, 0L);

        // 有图时按下的是「重新生成」⇒ **跳过缓存**，重发一发、原地覆盖同一个键。
        // 覆盖而不是换种子：缓存键不含随机数，旧文件不残留；上屏靠 ApplyPortrait 的
        // "先发 null 再发键"让 provider 看见一次变化。
        if (!_portraitPresent)
        {
            string existingPath;
            long existingLength;
            if (AwakePortraitCache.TryResolveExisting(key, out existingPath, out existingLength))
            {
                _portraitPresent = true;
                _portraitKey = key;
                _portraitBadge = ComposePortraitBadge(key);
                NotifyPortraitSlot();
                return;
            }
        }

        AwakeImageEndpoint endpoint;
        string resolveError;
        if (!AwakeImageEndpointResolver.TryResolve(AwakeSettings.Current, out endpoint, out resolveError))
        {
            FailPortrait(resolveError);
            return;
        }

        string apiKey = string.Empty;
        if (endpoint.IsCloud)
        {
            string keyError;
            apiKey = AwakeImageSecretStore.TryRead(out keyError);
            if (string.IsNullOrEmpty(apiKey))
            {
                FailPortrait(keyError);
                return;
            }
        }

        if (Interlocked.CompareExchange(ref _portraitGate, 1, 0) != 0)
        {
            FailPortrait(AwakeLocalization.Resolve("awake.ui.portrait_busy", "正在出一张图，等它结束。"));
            return;
        }

        _portraitGenerating = true;
        _portraitFailed = false;
        _portraitError = string.Empty;
        NotifyPortraitSlot();

        // 闭包只带值类型与不可变字符串，别把 VM 的可变状态带进后台线程。
        AwakeImageEndpoint endpointForRun = endpoint;
        string apiKeyForRun = apiKey;
        string promptForRun = prompt;
        string npcKeyForRun = npcKey;
        string keyForRun = key;

        AwakeBackgroundTask.Run(async () =>
        {
            AwakeImageOutcome outcome;
            try
            {
                using (CancellationTokenSource timeout = AwakeImageClient.CreateTimeoutScope())
                {
                    // 门与选路都在生成器里：框架路（Host.Media 出图 → Host.Assets 取字节）优先，
                    // 不可用时退回模组侧直连。两条路共用同一道云外发门，所以退回不构成绕过治理。
                    RequestContext portraitContext = _service.CreateAiContext(PortraitContextBudget);
                    outcome = await _service.PortraitGenerator
                        .GenerateAsync(endpointForRun, promptForRun, width, height, apiKeyForRun, portraitContext, timeout.Token)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                AwakeLog.Write("dialogue_portrait_run_error npc=" + npcKeyForRun + " error=" + ex.Message);
                outcome = new AwakeImageOutcome
                {
                    Ok = false,
                    ErrorCode = "image_unexpected",
                    ErrorMessage = "出图异常：" + ex.Message
                };
            }

            AwakeUiDispatcher.Enqueue(() => ReportPortrait(outcome, npcKeyForRun, keyForRun));
        }, "awake_dialogue_portrait_generate");
    }

    /// <summary>回到游戏线程后收尾：成功落盘＋上屏，失败说人话。</summary>
    private void ReportPortrait(AwakeImageOutcome outcome, string npcKey, string key)
    {
        Interlocked.Exchange(ref _portraitGate, 0);

        // 面板已关：只把闸放掉，别再往回写状态。
        if (_closed)
        {
            _portraitGenerating = false;
            return;
        }

        _portraitGenerating = false;

        if (outcome == null)
        {
            FailPortrait("出图没有返回结果。");
            return;
        }

        if (!outcome.Ok)
        {
            AwakeLog.Write("dialogue_portrait_error npc=" + npcKey
                + " url=" + outcome.RequestUrl
                + " code=" + outcome.ErrorCode
                + " status=" + outcome.StatusCode.ToString(CultureInfo.InvariantCulture)
                + " elapsed_ms=" + outcome.ElapsedMs.ToString(CultureInfo.InvariantCulture));
            FailPortrait(outcome.ErrorMessage);
            return;
        }

        AwakeImageReply reply = outcome.Reply;

        // ⚠️ 只吃 png。缓存文件名写死 .png、纹理由 CreateTextureFromPath 按路径加载，
        // 存 jpg 进去会变成"文件明明在、纹理却是空的"。宁可明说，也不冒充。
        if (reply == null || !string.Equals(reply.Format, "png", StringComparison.OrdinalIgnoreCase))
        {
            string format = reply == null ? "unknown" : reply.Format;
            AwakeLog.Write("dialogue_portrait_format_rejected npc=" + npcKey
                + " format=" + format + " bytes=" + (reply == null ? 0 : reply.Bytes.Length));
            FailPortrait(AwakeLocalization.Resolve("awake.ui.portrait_format_unsupported",
                "出图服务返回的不是 png（" + format + "），画位只吃 png。",
                new System.Collections.Generic.Dictionary<string, string> { ["FORMAT"] = format }));
            return;
        }

        string savedPath = AwakePortraitCache.Save(key, reply.Bytes);
        if (string.IsNullOrEmpty(savedPath))
        {
            FailPortrait(AwakeLocalization.Resolve("awake.ui.portrait_save_failed", "图出来了，但没能存到本机。"));
            return;
        }

        AwakeLog.Write("dialogue_portrait_ok npc=" + npcKey
            + " format=" + reply.Format
            + " size=" + reply.Width.ToString(CultureInfo.InvariantCulture)
            + "x" + reply.Height.ToString(CultureInfo.InvariantCulture)
            + " bytes=" + reply.Bytes.Length.ToString(CultureInfo.InvariantCulture)
            + " elapsed_ms=" + outcome.ElapsedMs.ToString(CultureInfo.InvariantCulture));

        ApplyPortrait(key);
        AwakeFeedback.ShowSuccess(AwakeLocalization.Resolve("awake.ui.portrait_done", "全身像已生成。"));
    }

    /// <summary>
    /// 把画位切到这张图。
    /// **先发 null 再发键**——provider 只在"键变了"时才重载，同一个键原地覆盖新图时
    /// 不这么做它会一直画旧纹理（AwakePortraitProbeVM 踩过同一个坑）。
    /// </summary>
    private void ApplyPortrait(string key)
    {
        _portraitPresent = true;
        _portraitFailed = false;
        _portraitError = string.Empty;
        _portraitBadge = ComposePortraitBadge(key);

        _portraitKey = null;
        NotifyPortraitSlot();
        _portraitKey = key;
        NotifyPortraitSlot();
    }

    private void FailPortrait(string message)
    {
        Interlocked.Exchange(ref _portraitGate, 0);
        _portraitGenerating = false;
        _portraitFailed = true;
        _portraitError = string.IsNullOrWhiteSpace(message)
            ? AwakeLocalization.Resolve("awake.ui.portrait_failed", "出图失败。")
            : message.Trim();
        AwakeLog.Write("dialogue_portrait_failure text=" + _portraitError);
        NotifyPortraitSlot();
    }

    private void NotifyPortraitSlot()
    {
        OnPropertyChanged(nameof(ShowGenerating));
        OnPropertyChanged(nameof(ShowPortrait));
        OnPropertyChanged(nameof(ShowEmptySlot));
        OnPropertyChanged(nameof(ShowError));
        OnPropertyChanged(nameof(ShowCenteredButton));
        OnPropertyChanged(nameof(ShowRegenerateButton));
        OnPropertyChanged(nameof(PortraitKey));
        OnPropertyChanged(nameof(PortraitBadgeText));
        OnPropertyChanged(nameof(PortraitErrorText));
        OnPropertyChanged(nameof(GenerateButtonText));
    }

    private AwakeContactInfo FindSelectedContact()
    {
        foreach (AwakeContactRowVM row in _presentRows)
        {
            if (string.Equals(row.CanonicalContactKey, _selectedContactKey, StringComparison.Ordinal))
            {
                return row.Contact;
            }
        }
        return null;
    }

    /// <summary>缓存身份用 StableId（跨会话稳定）；没有就退回联系人键。</summary>
    private static string PortraitIdentityOf(AwakeContactInfo contact)
    {
        if (contact == null) return string.Empty;
        return string.IsNullOrEmpty(contact.TargetId) ? contact.CanonicalContactKey : contact.TargetId;
    }

    private static void ResolvePortraitSize(out int width, out int height)
    {
        AwakeConfig config = AwakeSettings.Current;
        width = config == null ? 512 : config.PortraitImageWidth;
        height = config == null ? 512 : config.PortraitImageHeight;
        if (width <= 0) width = 512;
        if (height <= 0) height = 512;
    }

    /// <summary>左上角标："AI 全身像 · 09-15"。日期取缓存文件自己的写入时间。</summary>
    private static string ComposePortraitBadge(string key)
    {
        string label = AwakeLocalization.Resolve("awake.ui.portrait_badge", "AI 全身像");
        try
        {
            string path = AwakePortraitCache.AbsolutePathFor(key);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                return label + " · " + File.GetLastWriteTime(path)
                    .ToString("MM-dd", CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("dialogue_portrait_badge_error key=" + key + " error=" + ex.Message);
        }
        return label;
    }

    internal void OnFrameTick()
    {
        if (_closed || _service == null) return;
        NpcDialogueUiEvent evt;
        while (_service.TryDrainUiEvent(out evt))
        {
            DrainEvent(evt);
        }
    }

    public void ExecuteClose()
    {
        if (_closed) return;
        _closed = true;
        try
        {
            _service?.CancelActiveAsync();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_vm_close_error error=" + ex.Message);
        }
        try
        {
            _close?.Invoke();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_vm_close_callback_error error=" + ex.Message);
        }
    }

    internal void ShowWaitingHint()
    {
        if (_closed) return;
        NoticeText = AwakeLocalization.Resolve(
            "awake.dialogue.long_wait_hint",
            "对方仍在回应。等待 60 秒后可关闭并取消生成。");
    }

    public void ExecuteSend()
    {
        if (!CanSend) return;
        string text = _inputText.Trim();
        if (text.Length > NpcDialogueConstants.MaxPlayerInputLength)
        {
            text = AwakeRuntime.TruncateTextElements(text, NpcDialogueConstants.MaxPlayerInputLength);
        }
        InputText = string.Empty;
        StreamingText = string.Empty;
        AddChatRow(AwakeLocalization.Resolve("awake.ui.you", "你"), text);
        IsLoading = true;
        // 新一条先立自己的取消源，再取消上一条 —— 顺序反了会把刚建的也取消掉。
        CancellationTokenSource previousSend = _sendCts;
        _sendCts = new CancellationTokenSource();
        _ = SendAsyncSafe(text, _sendCts.Token);
        if (previousSend != null)
        {
            try { previousSend.Cancel(); }
            catch (ObjectDisposedException) { }
        }
    }

    public void ExecuteSetChatMode()
    {
        SetActionMode(NpcDialogueActionMode.Chat);
    }

    public void ExecuteSetNegotiationMode()
    {
        SetActionMode(NpcDialogueActionMode.Negotiation);
    }

    internal new void OnFinalize()
    {
        _closed = true;
        CancellationTokenSource sendCts = _sendCts;
        if (sendCts != null)
        {
            try { sendCts.Cancel(); }
            catch (ObjectDisposedException) { }
        }
        _service?.CancelActiveAsync();
    }

    private async Task SendAsyncSafe(string text, CancellationToken cancellationToken)
    {
        try
        {
            await _service.SendAsync(text, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 关面板、或玩家又发了一条 ⇒ 正常取消路径，不是错误。
            // （服务侧通常已把取消转成 `npc_dialogue.cancelled` 返回值，这里只是兜底。）
            AwakeLog.Write("npc_dialogue_vm_send_cancelled");
            AwakeUiDispatcher.Enqueue(() => IsLoading = false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_vm_send_error error=" + ex.Message);
            AwakeUiDispatcher.Enqueue(() => IsLoading = false);
        }
    }

    private void DrainEvent(NpcDialogueUiEvent evt)
    {
        switch (evt.Kind)
        {
            case NpcDialogueUiEventKind.Status:
                StatusText = evt.Text;
                break;
            case NpcDialogueUiEventKind.StreamDelta:
                StreamingText = AppendStream(StreamingText, evt.Text);
                break;
            case NpcDialogueUiEventKind.CommandConfirmationRequired:
                ShowCommandConfirmation(evt.Confirmation);
                break;
            case NpcDialogueUiEventKind.TurnCompleted:
                NpcDialogueTurnResult turnResult = evt.Turn;
                StreamingText = string.Empty;
                AddChatRow(_service.SpeakerName, turnResult.Reply);
                NoticeText = string.IsNullOrWhiteSpace(turnResult.Mood)
                    ? AwakeLocalization.Resolve(
                        _service.IsSceneShout ? "awake.scene_shout.replied" : "awake.ui.replied",
                        _service.IsSceneShout ? "场景有了回应。" : "对方已回应。")
                    : AwakeLocalization.Resolve(
                        _service.IsSceneShout ? "awake.scene_shout.replied_mood" : "awake.ui.replied_mood",
                        _service.IsSceneShout ? "场景有了回应（" + turnResult.Mood + "）。" : "对方已回应（" + turnResult.Mood + "）。",
                        new System.Collections.Generic.Dictionary<string, string> { ["MOOD"] = turnResult.Mood });
                IsLoading = false;
                break;
            case NpcDialogueUiEventKind.TurnFailed:
                NpcDialogueTurnResult failedResult = evt.Turn;
                StreamingText = string.Empty;
                AddChatRow(_service.SpeakerName, failedResult.ErrorDisplay);
                NoticeText = failedResult.ErrorDisplay;
                IsLoading = false;
                break;
        }
    }

    private void AddChatRow(string speaker, string text)
    {
        foreach (NpcDialogueChatRowVM row in _chatRows) row.SetLatest(false);
        while (_chatRows.Count >= 100) _chatRows.RemoveAt(0);
        NpcDialogueChatRowVM added = new NpcDialogueChatRowVM(speaker, text);
        added.SetLatest(true);
        _chatRows.Add(added);
    }

    private void ShowCommandConfirmation(NpcDialogueCommandConfirmation confirmation)
    {
        if (confirmation == null) return;
        InformationManager.ShowInquiry(new InquiryData(
            AwakeLocalization.Resolve("awake.ui.dialogue_proposal_title", "确认交涉结果"),
            AwakeLocalization.Resolve("awake.ui.dialogue_proposal_text", "对方提出以下可结算事项：{TEXT}\n\n确认后才会写入游戏状态。", new System.Collections.Generic.Dictionary<string, string>
            {
                ["TEXT"] = confirmation.DisplayText
            }),
            true,
            true,
            AwakeLocalization.Resolve("awake.ui.confirm", "确认"),
            AwakeLocalization.Resolve("awake.ui.cancel", "取消"),
            () => _service?.ConfirmPendingCommand(),
            () => _service?.RejectPendingCommand(),
            string.Empty,
            0f,
            null,
            null,
            null), true, false);
    }

    private void SetActionMode(NpcDialogueActionMode mode)
    {
        if (_closed || _service == null || !_service.CanChangeActionMode) return;
        if (!_service.TrySetActionMode(mode)) return;
        OnPropertyChanged(nameof(IsChatMode));
        OnPropertyChanged(nameof(IsNegotiationMode));
        OnPropertyChanged(nameof(CanChangeActionMode));
        NoticeText = mode == NpcDialogueActionMode.Negotiation
            ? AwakeLocalization.Resolve("awake.ui.negotiation_notice", "你正在明确提出条件；只有对方明确接受，且游戏状态实际完成结算，才会产生变化。")
            : AwakeLocalization.Resolve("awake.ui.chat_notice", "当前只进行普通交谈；说出口的话不会直接改变游戏状态。");
        AwakeLog.Write("npc_dialogue_action_mode_changed mode=" + mode);
    }

    private static string AppendStream(string current, string delta)
    {
        const int maximum = 20000;
        string combined = (current ?? string.Empty) + (delta ?? string.Empty);
        return AwakeRuntime.TruncateTextElementsFromEnd(combined, maximum);
    }

    private bool Set(ref string field, string value, string name)
    {
        value ??= string.Empty;
        if (string.Equals(field, value, StringComparison.Ordinal)) return false;
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
