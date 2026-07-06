namespace WatermarkRemover;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    private static void Main()
    {
        // 사용자 문자열을 쓰기 전에 언어를 먼저 로드한다.
        Localization.Load(Settings.Language);

        _mutex = new Mutex(true, "Global\\WatermarkRemover_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show(Localization.T("msg.alreadyRunning"), Localization.T("app.name"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // 앱이 실행되면 항상 차단 상태로 시작한다. (차단 해제는 해당 실행 동안만 유효)
        if (!Settings.BlockingEnabled)
        {
            Settings.BlockingEnabled = true;
            Logger.Info("BlockingEnabled forced to true on startup");
        }

        // 최초 실행 시 자동 시작을 기본값(켜짐)으로 등록한다.
        // 이후 사용자가 직접 끈 경우에는 다시 켜지 않는다.
        if (!Settings.AutoStartInitialized)
        {
            AutoStartManager.Enable();
            Settings.AutoStartInitialized = true;
            Logger.Info("Auto-start enabled by default on first run");
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
