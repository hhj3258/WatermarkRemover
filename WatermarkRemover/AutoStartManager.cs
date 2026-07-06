using System.Diagnostics;

namespace WatermarkRemover;

/// <summary>
/// 작업 스케줄러 기반 자동 시작 관리 (관리자 권한으로 실행하려면 Run 키 대신 스케줄러 필요)
/// </summary>
internal static class AutoStartManager
{
    private const string TaskName = "WatermarkRemover";

    public static bool IsEnabled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "schtasks.exe",
                Arguments              = $"/Query /TN \"{TaskName}\"",
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            using var proc = Process.Start(psi)!;
            proc.WaitForExit(3000);
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    public static void Enable()
    {
        var exePath = Environment.ProcessPath ?? "";
        var username = Environment.UserName;
        RunSchtasks(
            $"/Create /F /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\"\" " +
            $"/SC ONLOGON /RU \"{username}\" /RL HIGHEST /DELAY 0000:05");
    }

    public static void Disable()
    {
        RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
    }

    public static void Toggle()
    {
        if (IsEnabled()) Disable();
        else Enable();
    }

    private static void RunSchtasks(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName        = "schtasks.exe",
                Arguments       = args,
                UseShellExecute = true,
                Verb            = "runas",
                CreateNoWindow  = true,
                WindowStyle     = ProcessWindowStyle.Hidden,
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
        }
        catch { }
    }
}
