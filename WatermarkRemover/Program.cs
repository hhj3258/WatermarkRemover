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

        // 사용자가 설정 메뉴에서 "시작 시 자동 차단"을 켰다면 부팅 후 BlockingEnabled를 강제로 켠다.
        if (Settings.AutoEnableOnStart && !Settings.BlockingEnabled)
        {
            Settings.BlockingEnabled = true;
            Logger.Info("BlockingEnabled forced to true on startup (AutoEnableOnStart=on)");
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
