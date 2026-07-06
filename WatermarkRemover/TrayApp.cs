using System.Diagnostics;

namespace WatermarkRemover;

internal sealed class TrayApp : ApplicationContext
{
    private static readonly int[] RefreshIntervalPresetsMinutes = { 1, 5, 10, 30, 60 };

    private readonly NotifyIcon _trayIcon;
    private readonly WatermarkBlocker _blocker;

    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _nextRefreshItem;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _refreshIntervalItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly ToolStripMenuItem _autoEnableOnStartItem;
    private readonly ToolStripMenuItem _logToFileItem;
    private readonly List<ToolStripMenuItem> _refreshIntervalChoices = new();

    /// <summary>
    /// 메뉴가 열려 있는 동안 카운트다운 텍스트를 실시간으로 갱신하기 위한 타이머.
    /// </summary>
    private readonly System.Windows.Forms.Timer _countdownTimer;

    public TrayApp()
    {
        _blocker = new WatermarkBlocker();

        _statusItem      = new ToolStripMenuItem("상태 확인 중...")  { Enabled = false };
        _nextRefreshItem = new ToolStripMenuItem("다음 갱신: 계산 중") { Enabled = false };
        _toggleItem      = new ToolStripMenuItem("", null, OnToggleClick);

        _refreshIntervalItem = BuildRefreshIntervalMenu();
        _autoStartItem = new ToolStripMenuItem("Windows 시작 시 자동 실행", null, OnAutoStartClick)
        {
            Checked = AutoStartManager.IsEnabled(),
        };
        _autoEnableOnStartItem = new ToolStripMenuItem("시작 시 자동 차단", null, OnToggleAutoEnableOnStart)
        {
            Checked     = Settings.AutoEnableOnStart,
            ToolTipText = "켜져 있으면 부팅 후 항상 차단 상태로 시작합니다.",
        };
        _logToFileItem = new ToolStripMenuItem("동작 로그 파일 기록", null, OnToggleLogToFile)
        {
            Checked     = Settings.LogToFile,
            ToolTipText = "%LOCALAPPDATA%\\WatermarkRemover\\log.txt 에 기록합니다.",
        };

        _settingsItem = new ToolStripMenuItem("⚙ 설정");
        ApplyDropDownAppearance(_settingsItem);
        _settingsItem.DropDownItems.Add(_refreshIntervalItem);
        _settingsItem.DropDownItems.Add(new ToolStripSeparator());
        _settingsItem.DropDownItems.Add(_autoStartItem);
        _settingsItem.DropDownItems.Add(_autoEnableOnStartItem);
        _settingsItem.DropDownItems.Add(_logToFileItem);

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
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("종료", null, OnExitClick));

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
            Text             = "Watermark Remover",
            ContextMenuStrip = menu,
            Visible          = true,
        };

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
        var root = new ToolStripMenuItem("갱신 주기");
        int current = Settings.RefreshIntervalMinutes;

        foreach (int minutes in RefreshIntervalPresetsMinutes)
        {
            int captured = minutes;
            var item = new ToolStripMenuItem(FormatMinutes(minutes), null, (_, _) => OnRefreshIntervalSelected(captured))
            {
                Checked = (minutes == current),
            };
            _refreshIntervalChoices.Add(item);
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
            return $"{minutes / 60}시간";
        return $"{minutes}분";
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

    private void RefreshMenuState()
    {
        var svcStopped = _blocker.IsServiceStopped();
        var wantsBlock = _blocker.BlockingEnabled;

        if (svcStopped && wantsBlock)
        {
            _statusItem.Text = "✅ 워터마크 차단 중";
            _statusItem.Tag  = ModernMenuRenderer.StatusGreen;
            _toggleItem.Text = "워터마크 차단 해제";
        }
        else if (svcStopped && !wantsBlock)
        {
            _statusItem.Text = "⏳ 재시작 후 차단 해제 예정";
            _statusItem.Tag  = ModernMenuRenderer.StatusYellow;
            _toggleItem.Text = "워터마크 다시 차단";
        }
        else
        {
            _statusItem.Text = "⚠ 차단 해제됨";
            _statusItem.Tag  = ModernMenuRenderer.StatusOrange;
            _toggleItem.Text = "워터마크 다시 차단";
        }

        UpdateCountdownText();
        _autoStartItem.Checked = AutoStartManager.IsEnabled();

        // 설정 항목 체크 상태 동기화 (외부에서 레지스트리가 변경됐을 가능성 대비)
        _autoEnableOnStartItem.Checked = Settings.AutoEnableOnStart;
        _logToFileItem.Checked         = Settings.LogToFile;
        SyncRefreshIntervalChecks();
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
        _nextRefreshItem.Text = $"다음 갱신: {minutes}분 {seconds}초 후";
    }

    private void SyncRefreshIntervalChecks()
    {
        int current = Settings.RefreshIntervalMinutes;
        for (int i = 0; i < RefreshIntervalPresetsMinutes.Length; i++)
            _refreshIntervalChoices[i].Checked = (RefreshIntervalPresetsMinutes[i] == current);
    }

    private void OnRefreshIntervalSelected(int minutes)
    {
        Settings.RefreshIntervalMinutes = minutes;
        _blocker.OnRefreshIntervalChanged();
        SyncRefreshIntervalChecks();
    }

    private void OnToggleAutoEnableOnStart(object? sender, EventArgs e)
    {
        Settings.AutoEnableOnStart = !Settings.AutoEnableOnStart;
        _autoEnableOnStartItem.Checked = Settings.AutoEnableOnStart;
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
            if (result == "RESTART_REQUIRED")
            {
                var answer = MessageBox.Show(
                    "차단 해제는 재시작 후 적용됩니다.\n지금 재시작하시겠습니까?",
                    "Watermark Remover",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = false });
            }
            else
            {
                MessageBox.Show(result, "Watermark Remover", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return;
        }

        _blocker.EnableBlocking();
        MessageBox.Show("워터마크 차단이 다시 활성화됐습니다.",
            "Watermark Remover", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    private static Icon CreateIcon()
    {
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(30, 120, 220));
            using var font = new Font("Arial", 8f, FontStyle.Bold);
            g.DrawString("W", font, Brushes.White, 1f, 1f);
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
