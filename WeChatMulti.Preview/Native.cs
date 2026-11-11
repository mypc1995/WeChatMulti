using System.Runtime.InteropServices;

namespace WeChatMulti.Preview;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    public static int GetWindowRectLeft(IntPtr h) => R(h).Left;
    public static int GetWindowRectTop(IntPtr h) => R(h).Top;
    public static int GetWindowRectWidth(IntPtr h) => R(h).Right - R(h).Left;
    public static int GetWindowRectHeight(IntPtr h) => R(h).Bottom - R(h).Top;

    private static RECT R(IntPtr h)
    {
        GetWindowRect(h, out var r);
        return r;
    }
}
