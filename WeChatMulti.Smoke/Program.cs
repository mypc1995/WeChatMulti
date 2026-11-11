using WeChatMulti.Core;

namespace WeChatMulti.Smoke;

/// <summary>
/// 冒烟测试：直接跑真实的实例管理逻辑，验证 1 开 / 5 开 / 10 开。
/// 用法：dotnet run --project WeChatMulti.Smoke -- [3 5 10]
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var targets = args.Length > 0
            ? args.Select(int.Parse).ToArray()
            : new[] { 3, 5, 10 };

        var install = WeChatLocator.Resolve();
        if (install is null)
        {
            Console.Error.WriteLine("未检测到微信安装。");
            return 2;
        }

        Console.WriteLine($"程序路径 : {install.ExePath}");
        Console.WriteLine($"版本     : {install.Version}");
        Console.WriteLine($"数据目录 : {WeChatLocator.DetectChatDataRoot()}");
        Console.WriteLine();

        var manager = new InstanceManager(install.ExePath);
        int failures = 0;

        foreach (int target in targets)
        {
            Console.WriteLine($"=== 测试 {target} 开 ===");

            manager.KillAll();
            Console.WriteLine($"  清理后运行中: {manager.RunningCount()}");

            var report = manager.LaunchTo(target, Console.WriteLine);

            var snapshot = manager.Snapshot();
            bool windowsOk = snapshot.Length >= target;

            Console.WriteLine($"  报告   : 请求 {report.Requested}，存活 {report.Alive}，轮次 {report.Rounds}");
            Console.WriteLine($"  进程   : {snapshot.Length} 个");
            Console.WriteLine($"  有窗口 : {(windowsOk ? "是" : "否")}");

            foreach (var (pid, mem, title) in snapshot)
                Console.WriteLine($"    - pid {pid,6}  {mem,4} MB  {title}");

            if (report.Success && windowsOk)
            {
                Console.WriteLine($"  结果   : 通过");
            }
            else
            {
                Console.WriteLine($"  结果   : 失败");
                failures++;
            }

            Console.WriteLine();
        }

        Console.WriteLine("清理中…");
        manager.KillAll();
        Console.WriteLine($"结束，剩余实例 {manager.RunningCount()} 个。");

        return failures == 0 ? 0 : 1;
    }
}
