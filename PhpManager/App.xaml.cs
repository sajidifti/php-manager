using Microsoft.UI.Xaml;

namespace PhpManager;

public partial class App : Application
{
    private Mutex? instanceMutex;
    private TrayApplicationContext? tray;
    private NoticeWindow? noticeWindow;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            noticeWindow = new NoticeWindow("PHP Manager encountered a problem", e.Exception.Message, Exit);
            noticeWindow.Activate();
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        instanceMutex = new Mutex(true, @"Local\PhpManager.TrayApp", out var createdNew);
        if (!createdNew)
        {
            instanceMutex.Dispose();
            instanceMutex = null;
            noticeWindow = new NoticeWindow(
                "PHP Manager is already running",
                "The app is available from the system tray. Close this message and use the existing PHP Manager instance.",
                Exit);
            noticeWindow.Activate();
            return;
        }

        var showManager = Environment.GetCommandLineArgs()
            .Skip(1)
            .Any(argument => string.Equals(argument, "--show", StringComparison.OrdinalIgnoreCase));
        tray = new TrayApplicationContext(showManager);
    }

    public void Shutdown()
    {
        tray?.Dispose();
        tray = null;
        if (instanceMutex is not null)
        {
            instanceMutex.ReleaseMutex();
            instanceMutex.Dispose();
            instanceMutex = null;
        }
        Exit();
    }
}
