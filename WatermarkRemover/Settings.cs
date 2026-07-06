using Microsoft.Win32;

namespace WatermarkRemover;

/// <summary>
/// 사용자 설정 저장소 (HKCU\SOFTWARE\WatermarkRemover).
/// 모든 사용자 조정 가능 값(타이머 주기, 자동 차단, 로그 기록 등)은 이 클래스를 통해 읽고 쓴다.
/// </summary>
internal static class Settings
{
    private const string KeyPath = @"SOFTWARE\WatermarkRemover";

    private const string KeyBlockingEnabled       = "BlockingEnabled";
    private const string KeyRefreshIntervalMin    = "RefreshIntervalMinutes";
    private const string KeyAutoEnableOnStart     = "AutoEnableOnStart";
    private const string KeyLogToFile             = "LogToFile";

    private const int DefaultRefreshIntervalMin = 5;

    /// <summary>
    /// 사용자 의도: 워터마크 차단 활성 여부. 재시작 후에도 유지.
    /// </summary>
    public static bool BlockingEnabled
    {
        get => ReadInt(KeyBlockingEnabled, 1) != 0;
        set => WriteInt(KeyBlockingEnabled, value ? 1 : 0);
    }

    /// <summary>
    /// 워터마크 차단 갱신 타이머 주기(분). 기본 5분.
    /// </summary>
    public static int RefreshIntervalMinutes
    {
        get
        {
            int v = ReadInt(KeyRefreshIntervalMin, DefaultRefreshIntervalMin);
            return v <= 0 ? DefaultRefreshIntervalMin : v;
        }
        set => WriteInt(KeyRefreshIntervalMin, value);
    }

    /// <summary>
    /// 앱 시작 시 사용자 의도와 무관하게 차단을 항상 켤지 여부.
    /// </summary>
    public static bool AutoEnableOnStart
    {
        get => ReadInt(KeyAutoEnableOnStart, 0) != 0;
        set => WriteInt(KeyAutoEnableOnStart, value ? 1 : 0);
    }

    /// <summary>
    /// 동작 로그를 %LOCALAPPDATA%\WatermarkRemover\log.txt에 기록할지 여부.
    /// </summary>
    public static bool LogToFile
    {
        get => ReadInt(KeyLogToFile, 0) != 0;
        set => WriteInt(KeyLogToFile, value ? 1 : 0);
    }

    private static int ReadInt(string name, int defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, false);
            return key?.GetValue(name) is int v ? v : defaultValue;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Settings.ReadInt({name}) failed: {ex.Message}");
            return defaultValue;
        }
    }

    private static void WriteInt(string name, int value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
            key.SetValue(name, value, RegistryValueKind.DWord);
            Logger.Info($"Settings.WriteInt({name}={value}) by:\n{new System.Diagnostics.StackTrace(2, false)}");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Settings.WriteInt({name}) failed: {ex.Message}");
        }
    }
}
