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
