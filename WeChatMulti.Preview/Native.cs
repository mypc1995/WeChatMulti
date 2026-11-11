/*
 * 微信多开工具 (WeChatMulti) — Copyright (C) 2026 Evil_Pig
 *
 * 本程序依据 GNU 通用公共许可证第 3 版 (GPL-3.0) 发布。
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * 本程序以「按原样提供」的方式发布，不附带任何担保。
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * 你应当已经收到 GNU 通用公共许可证的副本。
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
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
