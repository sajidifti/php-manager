using System.Runtime.InteropServices;

namespace PhpManager;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }
    public static readonly IntPtr HwndBroadcast = new(0xffff);
    public const uint WmSettingChange = 0x001A;
    public static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

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

    [DllImport("user32.dll")]
    public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out Point point);
}
