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
using System.Text;

namespace WeChatMulti.Core;

public enum ArrangeMode
{
    不处理 = 0,
    网格平铺 = 1,
    层叠排列 = 2,
}

/// <summary>按进程 ID 把微信窗口排列好，方便多开时区分。</summary>
public static class WindowArranger
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const uint GWL_OWNER = 4;
    private const int SW_RESTORE = 9;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_TOP = IntPtr.Zero;

    /// <summary>返回成功排列的窗口数量。</summary>
    public static int Arrange(IReadOnlyCollection<int> pids, ArrangeMode mode)
    {
        if (mode == ArrangeMode.不处理 || pids.Count == 0) return 0;

        var handles = FindMainWindows(pids);
        if (handles.Count == 0) return 0;

        return mode switch
        {
            ArrangeMode.网格平铺 => Tile(handles),
            ArrangeMode.层叠排列 => Cascade(handles),
            _ => 0,
        };
    }

    /// <summary>找出属于指定进程、且是主窗口（可见、有标题、非被拥有窗口）的句柄。</summary>
    private static List<IntPtr> FindMainWindows(IReadOnlyCollection<int> pids)
    {
        var wanted = pids.ToHashSet();
        var found = new List<IntPtr>();

        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (!wanted.Contains((int)pid)) return true;

            if (!IsWindowVisible(hWnd)) return true;
            if (GetWindow(hWnd, GWL_OWNER) != IntPtr.Zero) return true;   // 排除托盘气泡等附属窗口

            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            if (sb.Length == 0) return true;

            found.Add(hWnd);
            return true;
        }, IntPtr.Zero);

        return found;
    }

    private static int Tile(List<IntPtr> handles)
    {
        var area = Screen.FromHandle(handles[0]).WorkingArea;

        int n = handles.Count;
        int cols = (int)Math.Ceiling(Math.Sqrt(n));
        int rows = (int)Math.Ceiling(n / (double)cols);

        int cellW = area.Width / cols;
        int cellH = area.Height / rows;

        for (int i = 0; i < n; i++)
        {
            int col = i % cols;
            int row = i / cols;

            ShowWindow(handles[i], SW_RESTORE);
            SetWindowPos(handles[i], HWND_TOP,
                area.Left + col * cellW,
                area.Top + row * cellH,
                cellW, cellH,
                SWP_SHOWWINDOW);
        }

        return n;
    }

    private static int Cascade(List<IntPtr> handles)
    {
        var area = Screen.FromHandle(handles[0]).WorkingArea;

        int width = (int)(area.Width * 0.62);
        int height = (int)(area.Height * 0.82);
        const int step = 30;

        // 层叠时反序处理，保证 1 号窗口在最上层
        for (int i = handles.Count - 1; i >= 0; i--)
        {
            int offset = i * step;
            ShowWindow(handles[i], SW_RESTORE);
            SetWindowPos(handles[i], HWND_TOP,
                area.Left + offset,
                area.Top + offset,
                width, height,
                SWP_SHOWWINDOW);
        }

        return handles.Count;
    }
}
