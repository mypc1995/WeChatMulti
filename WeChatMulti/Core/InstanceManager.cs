using System.Diagnostics;

namespace WeChatMulti.Core;

/// <summary>一次"启动到 N 个实例"的结果。</summary>
public sealed class LaunchReport
{
    public required int Requested { get; init; }
    public required int Alive { get; init; }
    public required int Rounds { get; init; }
    public bool Success => Alive >= Requested;
}

/// <summary>
/// 微信多开管理器。
///
/// 原理：微信 4.x 的单实例检查与互斥体创建之间存在竞态窗口 —— 当多个进程在同一瞬间
/// 启动时，它们会在彼此获取到互斥体之前全部通过检查，从而各自成为一个独立实例。
/// 微信随后会自动为每个实例分配 net、net_1 … net_9 这样的独立数据目录，
/// 登录态与聊天记录天然隔离。
/// </summary>
public sealed class InstanceManager
{
    public const int MaxInstances = 10;

    /// <summary>启动后等待进程落定的毫秒数：被拦截的进程会在这段时间内自行退出。</summary>
    private const int SettleMs = 5000;

    /// <summary>补开轮次上限。</summary>
    private const int MaxRounds = 3;

    /// <summary>杀完进程后等待互斥体彻底释放的时间。</summary>
    private const int SettleAfterKillMs = 2000;

    private readonly string _exePath;
    private readonly string _workingDir;

    public InstanceManager(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
            throw new ArgumentException("微信程序路径不能为空", nameof(exePath));
        if (!File.Exists(exePath))
            throw new FileNotFoundException("找不到微信程序", exePath);

        _exePath = Path.GetFullPath(exePath);
        _workingDir = Path.GetDirectoryName(_exePath)!;
    }

    public string ProcessName => Path.GetFileNameWithoutExtension(_exePath);

    private Process[] Running()
    {
        try
        {
            return Process.GetProcessesByName(ProcessName)
                           .Where(p => { try { return !p.HasExited; } catch { return false; } })
                           .ToArray();
        }
        catch
        {
            return [];
        }
    }

    public int[] RunningPids() => Running().Select(p => p.Id).ToArray();

    public int RunningCount() => Running().Length;

    /// <summary>
    /// 把运行中的实例拉到 desired 个。
    ///
    /// 注意：已运行的实例会一直占着互斥体，后来者根本挤不进竞争窗口，
    /// 所以数量不够时是「先全部清空、再让 N 个实例同时起跑」，
    /// 而不是单纯补开缺口——那样补出来的进程必然被挡回去。
    /// </summary>
    public LaunchReport LaunchTo(int desired, Action<string>? log = null, CancellationToken ct = default)
    {
        desired = Num.Clamp(desired, 1, MaxInstances);

        if (RunningCount() == desired)
        {
            log?.Invoke($"已经是 {desired} 个实例，无需操作。");
            return new LaunchReport { Requested = desired, Alive = desired, Rounds = 0 };
        }

        int rounds = 0;
        for (rounds = 1; rounds <= MaxRounds; rounds++)
        {
            ct.ThrowIfCancellationRequested();

            int existing = RunningCount();
            if (existing > 0)
            {
                log?.Invoke($"清空已有的 {existing} 个实例…");
                EnsureClean(ct);
            }

            log?.Invoke($"第 {rounds} 轮：并发拉起 {desired} 个…");
            Burst(desired, ct);

            ct.WaitHandle.WaitOne(SettleMs);

            int alive = RunningCount();
            if (alive == desired) break;

            log?.Invoke($"第 {rounds} 轮只开出 {alive} 个，再试一次…");
        }

        int finalCount = RunningCount();
        return new LaunchReport { Requested = desired, Alive = finalCount, Rounds = Math.Min(rounds, MaxRounds) };
    }

    /// <summary>
    /// 确保没有残留实例：杀干净之后，还要连续两次读到 0 才算干净。
    /// 否则刚被杀掉的进程可能还被算进下一轮的核对里，导致数量对不上。
    /// </summary>
    private void EnsureClean(CancellationToken ct)
    {
        if (RunningCount() > 0) KillAll();

        int zeros = 0;
        for (int i = 0; i < 40 && zeros < 2; i++)
        {
            if (ct.WaitHandle.WaitOne(400)) ct.ThrowIfCancellationRequested();
            zeros = RunningCount() == 0 ? zeros + 1 : 0;
        }
    }

    /// <summary>
    /// 紧凑地连续拉起 count 个进程。
    ///
    /// 关键在于"够快"：所有 CreateProcess 必须挤在微信完成单实例检查之前发出。
    /// 用原生 CreateProcessW（见 FastLauncher）而不是 Process.Start，
    /// 把单个实例的间隔从十几毫秒压到两三毫秒，首轮命中率明显提高。
    /// </summary>
    private int Burst(int count, CancellationToken ct)
    {
        var launcher = new FastLauncher(_exePath, _workingDir);

        // 预热一次 JIT 与系统调用路径。注意绝不能拿微信本身预热，
        // 那会真的多启动一个实例，导致实际数量比请求的多一个。
        FastLauncher.WarmUp();

        int started = 0;
        for (int i = 0; i < count; i++)
        {
            if (ct.IsCancellationRequested) break;
            if (launcher.LaunchOne()) started++;
        }

        return started;
    }

    /// <summary>关闭全部微信进程（含子进程），返回关闭数量。</summary>
    public int KillAll()
    {
        var procs = Running();
        var ids = procs.Select(p => p.Id).ToArray();
        foreach (var p in procs) p.Dispose();

        foreach (var id in ids) ProcUtil.KillTree(id);

        for (int i = 0; i < 50 && RunningCount() > 0; i++)
            Thread.Sleep(200);

        // 进程虽然退出了，但内核里的互斥体/共享对象释放有延迟。
        // 实测立刻重拉会全部被挡回去，必须留一点沉降时间。
        Thread.Sleep(SettleAfterKillMs);

        return ids.Length;
    }

    /// <summary>关闭指定进程 ID。</summary>
    public int KillByPid(int pid)
    {
        ProcUtil.KillTree(pid);
        return 1;
    }

    /// <summary>取运行中实例的 (pid, 内存MB, 窗口标题)。</summary>
    public (int Pid, long MemoryMb, string Title)[] Snapshot()
    {
        var result = new List<(int Pid, long MemoryMb, string Title)>();

        foreach (var p in Running())
        {
            using (p)
            {
                long mem = 0;
                try { mem = p.WorkingSet64 / (1024 * 1024); } catch { }

                string title = string.Empty;
                try { title = p.MainWindowTitle ?? string.Empty; } catch { }

                result.Add((p.Id, mem, title));
            }
        }

        result.Sort((a, b) => a.Pid.CompareTo(b.Pid));
        return [.. result];
    }
}
