using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;

namespace WatermarkRemover;

/// <summary>차단 해제 시도 결과. UI 계층이 이 값을 사용자 메시지로 변환한다.</summary>
internal enum UnblockResult
{
    Done,            // 즉시 해제 완료
    RestartRequired, // 재시작 후 반영
    NeedAdmin,       // 관리자 권한 없음
}

/// <summary>
/// Windows 정품 인증 워터마크 차단기.
/// 두 가지 전략을 병행한다:
///   1) 보호 서비스(sppsvc/sppamsvc/svsvc) 비활성화로 워터마크 렌더링을 막는다.
///   2) "Worker Window" 클래스에 그려진 워터마크 윈도우를 주기적으로 숨긴다.
/// 두 주기는 Settings에서 조정 가능.
/// </summary>
internal sealed class WatermarkBlocker : IDisposable
{
    private static readonly string[] ProtectionServices = { "sppsvc", "sppamsvc", "svsvc" };

    private static readonly TimeSpan ServiceRecheckInterval = TimeSpan.FromHours(1);

    private System.Windows.Forms.Timer? _watermarkRefreshTimer;
    private System.Windows.Forms.Timer? _serviceRecheckTimer;

    // SetWinEventHook 관련 — 풀스크린 앱(예: LoL) 전환 시 Worker Window가 재생성될 때 즉시 숨기기 위함.
    private IntPtr _winEventHook = IntPtr.Zero;
    private NativeMethods.WinEventDelegate? _winEventDelegate;
    private const string WatermarkWindowClass = "Worker Window";

    /// <summary>
    /// 워터마크 차단 갱신 타이머의 다음 실행 예정 시각. 트레이 메뉴 카운트다운 표시용.
    /// </summary>
    public DateTime NextRefreshAt { get; private set; }

    public static bool IsAdmin =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>
    /// 사용자 의도(차단 활성 여부). 위임: Settings.BlockingEnabled.
    /// </summary>
    public bool BlockingEnabled
    {
        get => Settings.BlockingEnabled;
        set => Settings.BlockingEnabled = value;
    }

    /// <summary>
    /// sppsvc 서비스가 정지 상태인지 확인. "차단되어 있는지"가 아니라 "서비스가 멈춰 있는지"를 본다
    /// (사용자 의도와 다를 수 있으므로 BlockingEnabled와 별도로 다뤄야 함).
    /// </summary>
    public bool IsServiceStopped()
    {
        try
        {
            using var sc = new ServiceController("sppsvc");
            return sc.Status != ServiceControllerStatus.Running;
        }
        catch (Exception ex)
        {
            Logger.Warn($"IsServiceStopped query failed: {ex.Message}");
            return true;
        }
    }

    public void Start()
    {
        ApplyBlockingOnce();

        // 서비스 재차단 점검 타이머 (Windows Update 등이 서비스를 다시 살릴 수 있음)
        _serviceRecheckTimer = new System.Windows.Forms.Timer
        {
            Interval = (int)ServiceRecheckInterval.TotalMilliseconds,
        };
        _serviceRecheckTimer.Tick += (_, _) => ApplyBlockingOnce();
        _serviceRecheckTimer.Start();

        // 워터마크 차단 갱신 타이머 (백업용 폴링)
        _watermarkRefreshTimer = new System.Windows.Forms.Timer();
        _watermarkRefreshTimer.Tick += (_, _) =>
        {
            TryHideWatermarkWindow();
            ScheduleNextRefresh();
        };
        ScheduleNextRefresh();
        _watermarkRefreshTimer.Start();

        // 실시간 후킹: Worker Window가 생성/표시될 때 즉시 숨김
        InstallWindowEventHook();
    }

    /// <summary>
    /// SetWinEventHook으로 EVENT_OBJECT_CREATE/SHOW를 후킹.
    /// 풀스크린 앱 전환 등으로 Worker Window가 재생성되는 즉시 숨길 수 있다.
    /// </summary>
    private void InstallWindowEventHook()
    {
        _winEventDelegate = OnWindowEvent;
        _winEventHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_CREATE,
            NativeMethods.EVENT_OBJECT_SHOW,
            IntPtr.Zero,
            _winEventDelegate,
            0, 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        if (_winEventHook == IntPtr.Zero)
            Logger.Warn("SetWinEventHook failed");
        else
            Logger.Info("WinEvent hook installed for Worker Window auto-hide");
    }

    private void OnWindowEvent(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (!BlockingEnabled) return;
        if (idObject != NativeMethods.OBJID_WINDOW) return;
        if (hwnd == IntPtr.Zero) return;

        // 클래스명 확인은 비용이 큰 작업이므로 최소 호출만 한다.
        var sb = new StringBuilder(64);
        int len = NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        if (len == 0) return;

        if (sb.ToString() != WatermarkWindowClass) return;

        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
        Logger.Info($"Watermark window hidden via WinEvent (hwnd=0x{hwnd.ToInt64():X})");
    }

    /// <summary>
    /// 사용자가 설정 메뉴에서 갱신 주기를 변경했을 때 TrayApp이 호출.
    /// </summary>
    public void OnRefreshIntervalChanged()
    {
        if (_watermarkRefreshTimer == null) return;

        _watermarkRefreshTimer.Stop();
        ScheduleNextRefresh();
        _watermarkRefreshTimer.Start();
    }

    public void ApplyBlockingOnce()
    {
        // 사용자가 차단 해제를 선택한 경우 서비스를 건드리지 않음
        if (!BlockingEnabled)
        {
            Logger.Info("ApplyBlockingOnce skipped (BlockingEnabled=false)");
            return;
        }

        var svcMsg = TryDisableProtectionServices();
        if (svcMsg != null)
        {
            Logger.Info(svcMsg);
            RestartExplorer();
            Task.Delay(3000).Wait(); // Explorer 재시작 후 윈도우 재생성 대기
        }

        TryHideWatermarkWindow();
    }

    public UnblockResult DisableBlocking()
    {
        if (!IsAdmin) return UnblockResult.NeedAdmin;

        // 사용자 의도 저장: 차단 해제
        BlockingEnabled = false;
        Logger.Info("DisableBlocking requested");

        // 레지스트리 자동 시작으로 복원
        foreach (var svc in ProtectionServices)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{svc}", true);
                key?.SetValue("Start", 2, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Restore Start key failed ({svc}): {ex.Message}");
            }
        }

        // 1차: ServiceController로 시작 시도
        try
        {
            using var sc = new ServiceController("sppsvc");
            if (sc.Status != ServiceControllerStatus.Running)
                sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
            sc.Refresh();
            if (sc.Status == ServiceControllerStatus.Running)
                return UnblockResult.Done;
        }
        catch (Exception ex)
        {
            Logger.Warn($"ServiceController.Start failed: {ex.Message}");
        }

        // 2차: sc.exe 명령으로 시도
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName        = "sc.exe",
                Arguments       = "start sppsvc",
                UseShellExecute = false,
                CreateNoWindow  = true,
            });
            proc?.WaitForExit(5000);

            using var sc2 = new ServiceController("sppsvc");
            sc2.Refresh();
            if (sc2.Status == ServiceControllerStatus.Running)
                return UnblockResult.Done;
        }
        catch (Exception ex)
        {
            Logger.Warn($"sc.exe start failed: {ex.Message}");
        }

        return UnblockResult.RestartRequired;
    }

    public void EnableBlocking()
    {
        BlockingEnabled = true;
        Logger.Info("EnableBlocking requested");
        ApplyBlockingOnce();
    }

    private void ScheduleNextRefresh()
    {
        int minutes = Settings.RefreshIntervalMinutes;
        var interval = TimeSpan.FromMinutes(minutes);
        if (_watermarkRefreshTimer != null)
            _watermarkRefreshTimer.Interval = (int)interval.TotalMilliseconds;
        NextRefreshAt = DateTime.Now + interval;
    }

    #region 보호 서비스 비활성화
    private static string? TryDisableProtectionServices()
    {
        if (!IsAdmin) return null;

        var stopped = false;
        foreach (var svc in ProtectionServices)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{svc}", true);
                key?.SetValue("Start", 4, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Set Start=4 failed ({svc}): {ex.Message}");
            }

            try
            {
                using var sc = new ServiceController(svc);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                    stopped = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Stop service failed ({svc}): {ex.Message}");
            }
        }
        return stopped ? "보호 서비스 정지 완료" : null;
    }
    #endregion

    #region 워터마크 윈도우 숨기기
    private static bool TryHideWatermarkWindow()
    {
        var hwnd = NativeMethods.FindWindow("Worker Window", null);
        if (hwnd == IntPtr.Zero) return false;

        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
        Logger.Info("Watermark window hidden");
        return true;
    }
    #endregion

    #region Explorer 재시작
    private static void RestartExplorer()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("explorer"))
            {
                p.Kill();
                p.WaitForExit(5000);
            }
            Task.Delay(1500).Wait();

            Process.Start(new ProcessStartInfo
            {
                FileName        = "explorer.exe",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Warn($"RestartExplorer failed: {ex.Message}");
        }
    }
    #endregion

    public void Dispose()
    {
        _watermarkRefreshTimer?.Stop();
        _watermarkRefreshTimer?.Dispose();
        _serviceRecheckTimer?.Stop();
        _serviceRecheckTimer?.Dispose();

        if (_winEventHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }
    }
}
