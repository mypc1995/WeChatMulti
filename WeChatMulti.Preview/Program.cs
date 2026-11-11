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
using System.Diagnostics;
using System.Drawing.Imaging;

namespace WeChatMulti.Preview;

/// <summary>
/// 开发用：把界面按真实屏幕渲染截成 PNG，便于检查视觉效果。
/// 传入 "real" 时启动的是实际交付的 WeChatMulti.exe，可连标题栏图标一起验证。
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string outPath = args.Length > 0 ? args[0] : "preview.png";
        bool real = args.Length > 1 && args[1].Equals("real", StringComparison.OrdinalIgnoreCase);
        int selected = args.Length > 2 && int.TryParse(args[2], out int s) ? s : 0;

        // crop 模式：把已有截图左上角的标题栏图标裁出来放大，用于核对图标
        if (args.Length > 1 && args[1].Equals("crop", StringComparison.OrdinalIgnoreCase))
        {
            CropTitleBarIcon(outPath);
            return;
        }

        if (real)
        {
            CaptureRealBuild(outPath);
            return;
        }

        using var form = new MainForm();
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(80, 60);
        form.TopMost = true;
        form.Show();
        form.BringToFront();
        form.Refresh();

        if (selected > 0 && form.Controls.OfType<Ui.ChipSelector>().FirstOrDefault() is { } chip)
            chip.Selected = selected;

        Pump(1500);

        Save(form.Bounds, outPath);
        form.Hide();
    }

    /// <summary>启动真正打包出来的 exe 并截它的窗口。</summary>
    private static void CaptureRealBuild(string outPath)
    {
        string? exe = FindBuiltExe();
        if (exe is null)
        {
            Console.Error.WriteLine("找不到 WeChatMulti 构建产物");
            return;
        }

        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
        using var proc = Process.Start(psi);
        if (proc is null) return;

        Pump(3500);

        proc.Refresh();
        IntPtr hwnd = proc.MainWindowHandle;
        if (hwnd == IntPtr.Zero)
        {
            Console.Error.WriteLine("窗口未就绪");
            return;
        }

        var bounds = new Rectangle(
            Native.GetWindowRectLeft(hwnd), Native.GetWindowRectTop(hwnd),
            Native.GetWindowRectWidth(hwnd), Native.GetWindowRectHeight(hwnd));

        Save(bounds, outPath);

        proc.CloseMainWindow();
        proc.WaitForExit(4000);
        if (!proc.HasExited) proc.Kill();
    }

    private static void CropTitleBarIcon(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            Console.Error.WriteLine("找不到截图：" + sourcePath);
            return;
        }

        using var src = new Bitmap(sourcePath);
        int w = Math.Min(160, src.Width);
        int h = Math.Min(34, src.Height);

        using var region = src.Clone(new Rectangle(0, 0, w, h), src.PixelFormat);
        using var big = new Bitmap(w * 6, h * 6);
        using (var g = Graphics.FromImage(big))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(region, 0, 0, w * 6, h * 6);
        }

        string outPath = Path.ChangeExtension(sourcePath, "_icon.png");
        big.Save(outPath, ImageFormat.Png);
        Console.WriteLine("saved " + outPath);
    }

    /// <summary>从当前目录逐级向上，找到 WeChatMulti 项目的构建产物。</summary>
    private static string? FindBuiltExe()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName,
                "WeChatMulti", "bin", "Release", "net48", "WeChatMulti.exe");
            if (File.Exists(candidate)) return candidate;

            dir = dir.Parent;
        }

        return null;
    }

    private static void Pump(int ms)
    {
        int ticks = ms / 25;
        for (int i = 0; i < ticks; i++)
        {
            Application.DoEvents();
            Thread.Sleep(25);
        }
    }

    private static void Save(Rectangle bounds, string path)
    {
        using var bmp = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        bmp.Save(path, ImageFormat.Png);
    }
}
