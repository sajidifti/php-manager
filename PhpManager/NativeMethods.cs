using System.Runtime.InteropServices;

namespace PhpManager;

internal static class NativeMethods
{
    public static readonly IntPtr HwndBroadcast = new(0xffff);
    public const uint WmSettingChange = 0x001A;

    [Flags]
    public enum SendMessageTimeoutFlags : uint
    {
        AbortIfHung = 0x0002
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        UIntPtr wParam,
        string lParam,
        SendMessageTimeoutFlags flags,
        uint timeout,
        out UIntPtr result);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);
}
