namespace WeChatMulti.Core;

/// <summary>.NET Framework 4.8 缺少的少量小工具。</summary>
internal static class Num
{
    /// <summary>Math.Clamp 的 4.8 版本实现。</summary>
    public static int Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;
}

/// <summary>进程终止工具（.NET Framework 的 Process.Kill 没有整树参数）。</summary>
internal static class ProcUtil
{
    /// <summary>连同子进程一起结束进程，等价于 .NET Core 的 Kill(entireProcessTree: true)。</summary>
    public static void KillTree(int pid)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("taskkill", $"/PID {pid} /T /F")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var tool = System.Diagnostics.Process.Start(psi);
            tool?.WaitForExit(5000);
        }
        catch
        {
            // taskkill 不可用时退回普通 Kill
            try
            {
                using var p = System.Diagnostics.Process.GetProcessById(pid);
                p.Kill();
            }
            catch
            {
                // 进程可能已退出
            }
        }
    }
}
