using System.Runtime.InteropServices;
using System.Text;

namespace WeChatMulti.Core;

/// <summary>
/// 直接调用 Win32 CreateProcessW 拉起进程。
///
/// 为什么不用 Process.Start：它在每次调用前要拼 ProcessStartInfo、解析路径、
/// 再构造 Process 对象并打开句柄，单次开销十几毫秒。N 个实例的突发间隔被拉长后，
/// 先启动的进程会抢先拿到单实例互斥体，后面的全部被挡回去。
/// 直接 CreateProcessW 后单次开销约 2~3 毫秒，突发足够紧凑。
/// </summary>
internal sealed class FastLauncher
{
    private readonly string _exePath;
    private readonly string _workingDir;

    public FastLauncher(string exePath, string workingDir)
    {
        _exePath = exePath;
        _workingDir = workingDir;
    }

    /// <summary>用一次无害进程预热 JIT 与系统调用路径，绝不碰目标程序。</summary>
    public static void WarmUp()
    {
        try
        {
            string cmd = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            if (!File.Exists(cmd)) return;

            var si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
            si.dwFlags = 0x00000004 | 0x08000000;   // CREATE_NO_WINDOW | CREATE_NO_WINDOW 兼容位

            var line = new StringBuilder(64);
            line.Append('"').Append(cmd).Append("\" /c exit");

            if (CreateProcessW(cmd, line, IntPtr.Zero, IntPtr.Zero, false, 0,
                               IntPtr.Zero, null, ref si, out var pi))
            {
                CloseHandle(pi.hThread);
                CloseHandle(pi.hProcess);
            }
        }
        catch
        {
            // 预热失败无关紧要
        }
    }

    /// <summary>启动一个进程，成功返回 true。失败会退回 Process.Start。</summary>
    public bool LaunchOne()
    {
        var si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(typeof(STARTUPINFO));

        // 命令行必须带引号，路径里有空格
        var cmd = new StringBuilder(260);
        cmd.Append('"').Append(_exePath).Append('"');

        if (CreateProcessW(_exePath, cmd, IntPtr.Zero, IntPtr.Zero, false, 0,
                           IntPtr.Zero, _workingDir, ref si, out var pi))
        {
            // 不保留句柄，否则会一直占着
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            return true;
        }

        return LaunchViaFramework();
    }

    /// <summary>原生调用失败时的兜底路径。</summary>
    private bool LaunchViaFramework()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _exePath,
                WorkingDirectory = _workingDir,
                UseShellExecute = false,
            };

            return System.Diagnostics.Process.Start(psi) is not null;
        }
        catch
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessW(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}
