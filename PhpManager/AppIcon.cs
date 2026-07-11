using System.Reflection;

namespace PhpManager;

internal static class AppIcon
{
    public static Icon LoadTrayIcon()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("Assets.php.png", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The embedded PHP icon could not be loaded.");
        using var source = new Bitmap(stream);
        using var trayBitmap = new Bitmap(source, new Size(32, 32));
        var iconHandle = trayBitmap.GetHicon();

        try
        {
            using var temporaryIcon = Icon.FromHandle(iconHandle);
            return (Icon)temporaryIcon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(iconHandle);
        }
    }
}
