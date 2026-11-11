using WeChatMulti.Core;

namespace WeChatMulti;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 命令行模式：WeChatMulti.exe --launch 5 [--arrange grid|cascade]
        if (CliRunner.TryRun(args, out int exitCode))
        {
            Environment.Exit(exitCode);
            return;
        }

        Application.Run(new MainForm());
    }
}
