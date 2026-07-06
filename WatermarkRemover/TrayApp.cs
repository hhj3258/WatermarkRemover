using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Reflection;

namespace WatermarkRemover;

internal sealed class TrayApp : ApplicationContext
{
    private const string RepositoryUrl = "https://github.com/hhj3258/WatermarkRemover";

    private static readonly int[] RefreshIntervalPresetsMinutes = { 1, 5, 10, 30, 60 };

    private readonly NotifyIcon _trayIcon;
    private readonly WatermarkBlocker _blocker;

    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _nextRefreshItem;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _refreshIntervalItem;
    private readonly ToolStripMenuItem _languageItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly ToolStripMenuItem _logToFileItem;
    private readonly ToolStripMenuItem _aboutItem;
    private readonly ToolStripMenuItem _versionItem;
    private readonly ToolStripMenuItem _repoItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly List<ToolStripMenuItem> _refreshIntervalChoices = new();
    private readonly List<ToolStripMenuItem> _languageChoices = new();

    /// <summary>
    /// 메뉴가 열려 있는 동안 카운트다운 텍스트를 실시간으로 갱신하기 위한 타이머.
    /// </summary>
    private readonly System.Windows.Forms.Timer _countdownTimer;

    public TrayApp()
    {
        _blocker = new WatermarkBlocker();

        _statusItem      = new ToolStripMenuItem { Enabled = false };
        _nextRefreshItem = new ToolStripMenuItem { Enabled = false };
        _toggleItem      = new ToolStripMenuItem("", null, OnToggleClick);

        _refreshIntervalItem = BuildRefreshIntervalMenu();
        _languageItem        = BuildLanguageMenu();
        _autoStartItem       = new ToolStripMenuItem("", null, OnAutoStartClick)
        {
            Checked = AutoStartManager.IsEnabled(),
        };
        _logToFileItem = new ToolStripMenuItem("", null, OnToggleLogToFile)
        {
            Checked = Settings.LogToFile,
        };
        _exitItem = new ToolStripMenuItem("", null, OnExitClick);

        _settingsItem = new ToolStripMenuItem();
        ApplyDropDownAppearance(_settingsItem);
        _settingsItem.DropDownItems.Add(_refreshIntervalItem);
        _settingsItem.DropDownItems.Add(_languageItem);
        _settingsItem.DropDownItems.Add(new ToolStripSeparator());
        _settingsItem.DropDownItems.Add(_autoStartItem);
        _settingsItem.DropDownItems.Add(_logToFileItem);

        // About: 버전 표시(비활성) + 저장소 링크(클릭 시 브라우저)
        _versionItem = new ToolStripMenuItem { Enabled = false };
        _repoItem    = new ToolStripMenuItem("", null, (_, _) => OpenRepository());
        _aboutItem   = new ToolStripMenuItem();
        ApplyDropDownAppearance(_aboutItem);
        _aboutItem.DropDownItems.Add(_versionItem);
        _aboutItem.DropDownItems.Add(_repoItem);

        var menu = new ContextMenuStrip
        {
            Renderer  = new ModernMenuRenderer(),
            BackColor = Color.FromArgb(0x2D, 0x2D, 0x30), // 시스템 기본 흰색 픽셀 노출 방지
            Padding   = new Padding(0),
        };
        menu.Items.Add(_statusItem);
        menu.Items.Add(_nextRefreshItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_settingsItem);
        menu.Items.Add(_aboutItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);

        // 아이템 패딩
        ApplyMenuItemPadding(menu);

        // Windows 11 라운드 코너
        menu.HandleCreated += (_, _) =>
        {
            int pref = NativeMethods.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(menu.Handle,
                NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        };

        _countdownTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _countdownTimer.Tick += (_, _) => UpdateCountdownText();

        menu.Opening += (_, _) =>
        {
            RefreshMenuState();
            _countdownTimer.Start();
        };
        menu.Closed += (_, _) => _countdownTimer.Stop();

        _trayIcon = new NotifyIcon
        {
            Icon             = CreateIcon(),
            Text             = Localization.T("app.name"),
            ContextMenuStrip = menu,
            Visible          = true,
        };

        ApplyStaticTexts();

        // 생성자 직후 곧바로 무거운 작업을 시키지 않기 위한 지연 시작.
        var startTimer = new System.Windows.Forms.Timer { Interval = 100 };
        startTimer.Tick += (_, _) =>
        {
            startTimer.Stop();
            startTimer.Dispose();
            _blocker.Start();
        };
        startTimer.Start();
    }

    private ToolStripMenuItem BuildRefreshIntervalMenu()
    {
        var root = new ToolStripMenuItem();

        foreach (int minutes in RefreshIntervalPresetsMinutes)
        {
            int captured = minutes;
            var item = new ToolStripMenuItem(FormatMinutes(minutes), null, (_, _) => OnRefreshIntervalSelected(captured));
            _refreshIntervalChoices.Add(item);
            root.DropDownItems.Add(item);
        }
        ApplyDropDownAppearance(root);
        return root;
    }

    private ToolStripMenuItem BuildLanguageMenu()
    {
        // 지구본 아이콘: 영어를 못 읽어도 언어 설정을 바로 찾을 수 있도록.
        var root = new ToolStripMenuItem { Image = CreateGlobeImage() };

        foreach (var (code, name) in Localization.Available)
        {
            string captured = code;
            // 언어 이름은 각 언어 고유 표기라 번역하지 않는다.
            var item = new ToolStripMenuItem(name, null, (_, _) => OnLanguageSelected(captured));
            _languageChoices.Add(item);
            root.DropDownItems.Add(item);
        }
        ApplyDropDownAppearance(root);
        return root;
    }

    /// <summary>
    /// 서브메뉴(DropDown)에도 다크 BackColor / Padding(0) / ModernMenuRenderer / DWM 라운드 코너 적용.
    /// 미적용 시 시스템 기본 흰색이 1~2px 노출됨.
    /// </summary>
    private static void ApplyDropDownAppearance(ToolStripMenuItem item)
    {
        var dd = item.DropDown;
        dd.Renderer  = new ModernMenuRenderer();
        dd.BackColor = Color.FromArgb(0x2D, 0x2D, 0x30);
        dd.Padding   = new Padding(0);
        dd.HandleCreated += (_, _) =>
        {
            int pref = NativeMethods.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(dd.Handle,
                NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        };
    }

    private static string FormatMinutes(int minutes)
    {
        if (minutes >= 60)
            return Localization.T("unit.hours", minutes / 60);
        return Localization.T("unit.minutes", minutes);
    }

    private static void ApplyMenuItemPadding(ToolStrip menu)
    {
        foreach (ToolStripItem item in menu.Items)
        {
            if (item is ToolStripMenuItem mi)
            {
                mi.Padding = new Padding(8, 6, 8, 6);
                ApplyMenuItemPadding(mi.DropDown);
            }
        }
    }

    /// <summary>
    /// 언어에 따라 바뀌는 정적 라벨/툴팁을 일괄 적용. 생성자 및 언어 변경 시 호출.
    /// </summary>
    private void ApplyStaticTexts()
    {
        _statusItem.Text      = Localization.T("status.loading");
        _nextRefreshItem.Text = Localization.T("nextRefresh.calculating");

        _settingsItem.Text        = Localization.T("menu.settings");
        _refreshIntervalItem.Text = Localization.T("menu.refreshInterval");
        _languageItem.Text        = Localization.T("menu.language");

        _autoStartItem.Text        = Localization.T("menu.autoStart");
        _autoStartItem.ToolTipText = Localization.T("menu.autoStart.tooltip");
        _logToFileItem.Text        = Localization.T("menu.logToFile");
        _logToFileItem.ToolTipText = Localization.T("menu.logToFile.tooltip");

        _aboutItem.Text   = Localization.T("menu.about");
        _versionItem.Text = $"{Localization.T("app.name")}  v{AppVersion}";
        _repoItem.Text    = Localization.T("about.repository");

        _exitItem.Text = Localization.T("menu.exit");

        for (int i = 0; i < RefreshIntervalPresetsMinutes.Length; i++)
            _refreshIntervalChoices[i].Text = FormatMinutes(RefreshIntervalPresetsMinutes[i]);

        _trayIcon.Text = Localization.T("app.name");
    }

    private void RefreshMenuState()
    {
        var svcStopped = _blocker.IsServiceStopped();
        var wantsBlock = _blocker.BlockingEnabled;

        if (svcStopped && wantsBlock)
        {
            _statusItem.Text = Localization.T("status.blocking");
            _statusItem.Tag  = ModernMenuRenderer.StatusGreen;
            _toggleItem.Text = Localization.T("toggle.unblock");
        }
        else if (svcStopped && !wantsBlock)
        {
            _statusItem.Text = Localization.T("status.unblockPending");
            _statusItem.Tag  = ModernMenuRenderer.StatusYellow;
            _toggleItem.Text = Localization.T("toggle.reblock");
        }
        else
        {
            _statusItem.Text = Localization.T("status.unblocked");
            _statusItem.Tag  = ModernMenuRenderer.StatusOrange;
            _toggleItem.Text = Localization.T("toggle.reblock");
        }

        UpdateCountdownText();
        _autoStartItem.Checked = AutoStartManager.IsEnabled();

        // 설정 항목 체크 상태 동기화 (외부에서 레지스트리가 변경됐을 가능성 대비)
        _logToFileItem.Checked = Settings.LogToFile;
        SyncRefreshIntervalChecks();
        SyncLanguageChecks();
    }

    /// <summary>
    /// 카운트다운 텍스트를 현재 시각 기준으로 계산해 메뉴 항목에 반영.
    /// 메뉴가 열려 있는 동안 _countdownTimer가 주기적으로 호출.
    /// </summary>
    private void UpdateCountdownText()
    {
        var remaining = _blocker.NextRefreshAt - DateTime.Now;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

        int minutes = (int)remaining.TotalMinutes;
        int seconds = remaining.Seconds;
        _nextRefreshItem.Text = Localization.T("nextRefresh.format", minutes, seconds);
    }

    private void SyncRefreshIntervalChecks()
    {
        int current = Settings.RefreshIntervalMinutes;
        for (int i = 0; i < RefreshIntervalPresetsMinutes.Length; i++)
            _refreshIntervalChoices[i].Checked = (RefreshIntervalPresetsMinutes[i] == current);
    }

    private void SyncLanguageChecks()
    {
        for (int i = 0; i < Localization.Available.Length; i++)
            _languageChoices[i].Checked = (Localization.Available[i].Code == Localization.CurrentLanguage);
    }

    private void OnRefreshIntervalSelected(int minutes)
    {
        Settings.RefreshIntervalMinutes = minutes;
        _blocker.OnRefreshIntervalChanged();
        SyncRefreshIntervalChecks();
    }

    private void OnLanguageSelected(string code)
    {
        Settings.Language = code;
        Localization.Load(code);
        SyncLanguageChecks();
        ApplyStaticTexts();
    }

    private void OnToggleLogToFile(object? sender, EventArgs e)
    {
        Settings.LogToFile = !Settings.LogToFile;
        _logToFileItem.Checked = Settings.LogToFile;
    }

    private void OnToggleClick(object? sender, EventArgs e)
    {
        if (_blocker.BlockingEnabled)
        {
            var result = _blocker.DisableBlocking();
            switch (result)
            {
                case UnblockResult.RestartRequired:
                    var answer = MessageBox.Show(
                        Localization.T("msg.restartPrompt"),
                        Localization.T("app.name"),
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (answer == DialogResult.Yes)
                        Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = false });
                    break;

                case UnblockResult.NeedAdmin:
                    MessageBox.Show(Localization.T("msg.needAdmin"), Localization.T("app.name"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;

                case UnblockResult.Done:
                    MessageBox.Show(Localization.T("msg.unblockDone"), Localization.T("app.name"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;

                default:
                    Logger.Error($"Unhandled UnblockResult: {result}");
                    break;
            }
            return;
        }

        _blocker.EnableBlocking();
        MessageBox.Show(Localization.T("msg.reblocked"), Localization.T("app.name"),
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnAutoStartClick(object? sender, EventArgs e)
    {
        AutoStartManager.Toggle();
        _autoStartItem.Checked = AutoStartManager.IsEnabled();
    }

    private void OnExitClick(object? sender, EventArgs e)
    {
        _trayIcon.Visible = false;
        _blocker.Dispose();
        Application.Exit();
    }

    private static void OpenRepository()
    {
        try
        {
            Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Warn($"Open repository failed: {ex.Message}");
        }
    }

    /// <summary>어셈블리 버전을 "메이저.마이너.패치" 형식으로 반환.</summary>
    private static string AppVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "?" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <summary>언어 메뉴용 지구본 아이콘(흰색 선). 메뉴 폰트가 이모지를 흑백 두부로 그려서 직접 그린다.</summary>
    private static Image CreateGlobeImage()
    {
        const int s = 16;
        var bmp = new Bitmap(s, s);
        var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var pen = new Pen(Color.White, s / 13f);

        float m = s * 0.10f;
        float d = s - 2 * m;
        g.DrawEllipse(pen, m, m, d, d);                        // 외곽 원
        g.DrawLine(pen, m, s / 2f, s - m, s / 2f);             // 적도

        float meridianW = d * 0.42f;
        g.DrawEllipse(pen, (s - meridianW) / 2f, m, meridianW, d); // 세로 자오선

        float y1 = s * 0.32f, y2 = s * 0.68f, x1 = s * 0.24f, x2 = s * 0.76f;
        g.DrawLine(pen, x1, y1, x2, y1);                       // 위도선
        g.DrawLine(pen, x1, y2, x2, y2);

        g.Dispose();
        return bmp;
    }

    private static Icon CreateIcon()
    {
        const int size = 16;
        var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(30, 120, 220));

            // 글자의 실제 경계(bounding box) 기준으로 정확히 중앙 정렬
            using var path = new GraphicsPath();
            using var family = new FontFamily("Arial");
            path.AddString("W", family, (int)FontStyle.Bold, size * 0.72f,
                new PointF(0, 0), StringFormat.GenericDefault);

            var b = path.GetBounds();
            float mx = (size - b.Width) / 2f - b.X;
            float my = (size - b.Height) / 2f - b.Y;
            using var mat = new Matrix();
            mat.Translate(mx, my);
            path.Transform(mat);

            g.FillPath(Brushes.White, path);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _countdownTimer.Stop();
            _countdownTimer.Dispose();
            _trayIcon.Dispose();
            _blocker.Dispose();
        }
        base.Dispose(disposing);
    }
}
