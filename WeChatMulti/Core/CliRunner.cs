using System.Runtime.InteropServices;

namespace WeChatMulti.Core;

/// <summary>
/// 命令行模式：无需图形界面，直接拉起 N 个实例。
/// 用途是给桌面快捷方式加参数，实现"双击一下开 5 个微信"。
/// </summary>
public static class CliRunner
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    private const int AttachParentProcess = -1;

    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;

        int count = -1;
        ArrangeMode arrange = ArrangeMode.不处理;
        string? customPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--launch" when i + 1 < args.Length && int.TryParse(args[i + 1], out int n):
                    count = n;
                    i++;
                    break;

                case "--arrange" when i + 1 < args.Length:
                    arrange = args[i + 1].ToLowerInvariant() switch
                    {
                        "grid" => ArrangeMode.网格平铺,
                        "cascade" => ArrangeMode.层叠排列,
                        _ => ArrangeMode.不处理,
                    };
                    i++;
                    break;

                case "--path" when i + 1 < args.Length:
                    customPath = args[i + 1];
                    i++;
                    break;
            }
        }

        if (count < 0) return false;

        AttachConsole(AttachParentProcess);

        // 手动指定路径优先，指定了就记住
        WeChatInstall? install = null;
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            install = WeChatLocator.ResolveInstall(customPath);
            if (install is null)
            {
                Write($"指定的路径无法识别为微信主程序：{customPath}");
                exitCode = 2;
                return true;
            }
            WeChatLocator.SaveCustomPath(install.ExePath);
        }
        else
        {
            install = WeChatLocator.Resolve();
        }

        if (install is null)
        {
            Write("未检测到微信安装。请用 --path 指定微信主程序的完整路径。");
            exitCode = 2;
            return true;
        }

        Write($"微信路径：{install.ExePath}（{install.Version}）");

        var manager = new InstanceManager(install.ExePath);
        var report = manager.LaunchTo(count, Write);

        if (arrange != ArrangeMode.不处理)
        {
            int moved = WindowArranger.Arrange(manager.RunningPids(), arrange);
            Write($"已排列 {moved} 个窗口。");
        }

        Write(report.Success
            ? $"完成，当前运行 {report.Alive} 个实例。"
            : $"未完全成功，目标 {report.Requested} 个，实际 {report.Alive} 个。");

        exitCode = report.Success ? 0 : 1;
        return true;
    }

    private static void Write(string message) => Console.WriteLine(message);
}
