namespace WatermarkRemover;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    private static void Main()
    {
        _mutex = new Mutex(true, "Global\\WatermarkRemover_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("이미 실행 중입니다.", "Watermark Remover",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // 앱이 실행되면 항상 차단 상태로 시작한다. (차단 해제는 해당 실행 동안만 유효)
        if (!Settings.BlockingEnabled)
        {
            Settings.BlockingEnabled = true;
            Logger.Info("BlockingEnabled forced to true on startup");
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
