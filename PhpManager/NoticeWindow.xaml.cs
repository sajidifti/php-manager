using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace PhpManager;

public sealed partial class NoticeWindow : Window
{
    private readonly Action closed;
    private bool closing;

    public NoticeWindow(string heading, string message, Action closed)
    {
        InitializeComponent();
        this.closed = closed;
        HeadingText.Text = heading;
        MessageText.Text = message;
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(NoticeTitleBar);

        var window = AppWindow.GetFromWindowId(
            Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this)));
        window.Resize(new Windows.Graphics.SizeInt32(680, 360));
        window.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "php.ico"));
        window.Closing += (_, _) => CompleteClose();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        CompleteClose();
        Close();
    }

    private void CompleteClose()
    {
        if (closing) return;
        closing = true;
        closed();
    }
}
