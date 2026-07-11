namespace PhpManager;

static class Program
{
    private static Mutex? instanceMutex;

    [STAThread]
    static void Main()
    {
        instanceMutex = new Mutex(initiallyOwned: true, @"Local\PhpManager.TrayApp", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("PHP Manager is already running in the system tray.", "PHP Manager");
            instanceMutex.Dispose();
            return;
        }

        ApplicationConfiguration.Initialize();
        try
        {
            Application.Run(new TrayApplicationContext());
        }
        finally
        {
            instanceMutex.ReleaseMutex();
            instanceMutex.Dispose();
        }
    }
}
