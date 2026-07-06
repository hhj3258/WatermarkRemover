using Microsoft.Win32;

namespace WatermarkRemover;

/// <summary>
/// 파일 기반 로거. 사용자가 설정 메뉴에서 켰을 때만 기록.
/// Settings 클래스에 의존하지 않고 레지스트리를 직접 읽어 순환 의존을 방지한다
/// (Settings도 실패 시 Logger를 호출하기 때문).
/// </summary>
internal static class Logger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WatermarkRemover",
        "log.txt");

    public static void Info(string msg)  => Write("INFO ", msg);
    public static void Warn(string msg)  => Write("WARN ", msg);
    public static void Error(string msg) => Write("ERROR", msg);

    private static void Write(string level, string msg)
    {
        if (!IsEnabled()) return;

        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (dir != null) Directory.CreateDirectory(dir);

            File.AppendAllText(LogPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}{Environment.NewLine}");
        }
        catch
        {
            // 로그 자체가 실패하면 무시 (앱 동작에 영향 없도록).
        }
    }

    private static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WatermarkRemover", false);
            return key?.GetValue("LogToFile") is int v && v != 0;
        }
        catch
        {
            return false;
        }
    }
}
