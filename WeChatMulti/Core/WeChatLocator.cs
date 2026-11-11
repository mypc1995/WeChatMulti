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
using Microsoft.Win32;

namespace WeChatMulti.Core;

/// <summary>一次探测到的微信安装信息。</summary>
public sealed record WeChatInstall(string ExePath, string InstallDir, string Version)
{
    public string FileName => Path.GetFileName(ExePath);
    public string ProcessName => Path.GetFileNameWithoutExtension(ExePath);
}

/// <summary>定位微信安装位置与数据目录。</summary>
public static class WeChatLocator
{
    /// <summary>微信主程序的几种可能命名。</summary>
    private static readonly string[] ExeNames = { "Weixin.exe", "WeChat.exe" };

    private static readonly (RegistryHive Hive, string SubKey)[] RegistryCandidates =
    {
        (RegistryHive.CurrentUser,  @"Software\Tencent\Weixin"),   // 微信 4.x
        (RegistryHive.CurrentUser,  @"Software\Tencent\WeChat"),
        (RegistryHive.LocalMachine,  @"SOFTWARE\WOW6432Node\Tencent\Weixin"),
        (RegistryHive.LocalMachine,  @"SOFTWARE\Tencent\Weixin"),
        (RegistryHive.LocalMachine,  @"SOFTWARE\WOW6432Node\Tencent\WeChat"),
        (RegistryHive.LocalMachine,  @"SOFTWARE\Tencent\WeChat"),
    };

    private static readonly string[] CommonDirs =
    {
        @"D:\Program Files\Tencent\Weixin",
        @"C:\Program Files\Tencent\Weixin",
        @"C:\Program Files (x86)\Tencent\Weixin",
        @"D:\Program Files\Tencent\WeChat",
        @"C:\Program Files\Tencent\WeChat",
        @"C:\Program Files (x86)\Tencent\WeChat",
    };

    /// <summary>用户手动指定的路径，记住后下次启动直接用。</summary>
    private static string SettingsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WeChatMulti", "wechat-path.txt");

    // ---------- 对外入口 ----------

    /// <summary>
    /// 优先使用用户手动指定的路径；没有或失效时回退到自动探测。
    /// </summary>
    public static WeChatInstall? Resolve()
    {
        var custom = ResolveInstall(LoadCustomPath());
        if (custom is not null) return custom;

        foreach (var dir in EnumerateCandidateDirs())
        {
            var install = BuildFromDir(dir);
            if (install is not null) return install;
        }

        return null;
    }

    /// <summary>
    /// 把用户给的东西（exe 路径 / 文件夹路径）解析成可用的安装信息，
    /// 无法识别时返回 null。
    /// </summary>
    public static WeChatInstall? ResolveInstall(string? userInput)
    {
        string? exe = ResolveExe(userInput);
        return exe is null ? null : BuildFromExe(exe);
    }

    /// <summary>
    /// 用户可以给 exe，也可以直接给安装文件夹，这里统一解析成 exe 全路径。
    /// 还会往下看一层，因为微信 4.x 的主程序旁边常有版本号子目录。
    /// </summary>
    public static string? ResolveExe(string? userInput)
    {
        if (userInput is null) return null;
        if (userInput.Trim().Length == 0) return null;

        string p = userInput.Trim().Trim('"');

        if (File.Exists(p)) return Path.GetFullPath(p);

        if (Directory.Exists(p))
        {
            foreach (var name in ExeNames)
            {
                string direct = Path.Combine(p, name);
                if (File.Exists(direct)) return direct;
            }

            // 一层子目录（兼容 D:\...\Weixin\4.1.15.53 这类结构）
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(p))
                {
                    foreach (var name in ExeNames)
                    {
                        string nested = Path.Combine(sub, name);
                        if (File.Exists(nested)) return nested;
                    }
                }
            }
            catch
            {
                // 无权限时忽略
            }
        }

        return null;
    }

    /// <summary>记住用户手动指定的路径。</summary>
    public static void SaveCustomPath(string exePath)
    {
        try
        {
            // 要建的是配置文件所在的目录，不是微信的安装目录
            string? dir = Path.GetDirectoryName(SettingsFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(SettingsFile, exePath.Trim());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存自定义路径失败：{ex.Message}");
        }
    }

    public static void ClearCustomPath()
    {
        try { if (File.Exists(SettingsFile)) File.Delete(SettingsFile); }
        catch { /* 忽略 */ }
    }

    private static string? LoadCustomPath()
    {
        try
        {
            return File.Exists(SettingsFile) ? File.ReadAllText(SettingsFile).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    // ---------- 自动探测 ----------

    private static IEnumerable<string> EnumerateCandidateDirs()
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Offer(string? dir)
        {
            if (dir is null) return;
            string candidate = dir.Trim();
            if (candidate.Length > 0 && seen.Add(candidate)) result.Add(candidate);
        }

        // 1) 注册表安装记录
        foreach (var (hive, subKey) in RegistryCandidates)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = baseKey.OpenSubKey(subKey);
                Offer(key?.GetValue("InstallPath") as string);
            }
            catch
            {
                // 注册表不可读时忽略
            }
        }

        foreach (var dir in result) yield return dir;

        // 2) 卸载列表里的安装目录（能覆盖装在任意盘的非标准位置）
        foreach (var dir in EnumerateUninstallDirs()) if (seen.Add(dir)) yield return dir;

        // 3) 常见目录
        foreach (var dir in CommonDirs) if (seen.Add(dir)) yield return dir;
    }

    /// <summary>从「应用和功能」的卸载项里找微信，兼容非标准安装位置。</summary>
    private static IEnumerable<string> EnumerateUninstallDirs()
    {
        var roots = new (RegistryHive Hive, string SubKey)[]
        {
            (RegistryHive.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.LocalMachine,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.LocalMachine,  @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        };

        foreach (var (hive, subKey) in roots)
        {
            string[] names;
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = baseKey.OpenSubKey(subKey);
                if (key is null) continue;
                names = key.GetSubKeyNames();
            }
            catch
            {
                continue;
            }

            foreach (var name in names)
            {
                string? display, location;
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                    using var sub = baseKey.OpenSubKey($@"{subKey}\{name}");
                    if (sub is null) continue;

                    display = sub.GetValue("DisplayName") as string;
                    location = sub.GetValue("InstallLocation") as string;
                }
                catch
                {
                    continue;
                }

                if (display is null || display.Trim().Length == 0) continue;
                if (display.IndexOf("微信", StringComparison.OrdinalIgnoreCase) < 0 &&
                    display.IndexOf("WeChat", StringComparison.OrdinalIgnoreCase) < 0 &&
                    display.IndexOf("Weixin", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (location is not null && location.Trim().Length > 0)
                    yield return location.Trim();
            }
        }
    }

    private static WeChatInstall? BuildFromDir(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return null;

        foreach (var name in ExeNames)
        {
            string exe = Path.Combine(dir, name);
            if (File.Exists(exe)) return BuildFromExe(exe);
        }

        return null;
    }

    private static WeChatInstall BuildFromExe(string exe)
    {
        string version = "未知";
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exe);
            version = string.IsNullOrWhiteSpace(info.FileVersion) ? "未知" : info.FileVersion;
        }
        catch
        {
            // 读不到版本号不影响使用
        }

        return new WeChatInstall(exe, Path.GetDirectoryName(exe) ?? string.Empty, version);
    }

    // ---------- 数据目录 ----------

    /// <summary>聊天数据根目录（含 xweChat_files），失败时回退到"我的文档"。</summary>
    public static string DetectChatDataRoot()
    {
        var documents = SafeReadDocumentsSetting() ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // 微信 4.x 放在 <MyDocument>\xweChat_files
        var fourX = Path.Combine(documents, "xweChat_files");
        if (Directory.Exists(fourX)) return fourX;

        // 微信 3.x 放在 <MyDocument>\WeChat Files
        var threeX = Path.Combine(documents, "WeChat Files");
        if (Directory.Exists(threeX)) return threeX;

        return fourX;
    }

    /// <summary>
    /// 微信把"我的文档"位置存成了一对哈希命名的 ini：
    /// 一个文件内容是键名（如 "MyDocument:"），另一个文件内容是目录路径。
    /// </summary>
    private static string? SafeReadDocumentsSetting()
    {
        try
        {
            var cfgDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Tencent", "xwechat", "config");

            if (!Directory.Exists(cfgDir)) return null;

            var files = Directory.GetFiles(cfgDir, "*.ini");
            string? keyFile = null;

            foreach (var f in files)
            {
                if (ReadTextSafe(f).Trim().Equals("MyDocument:", StringComparison.OrdinalIgnoreCase))
                {
                    keyFile = f;
                    break;
                }
            }

            if (keyFile is null) return null;

            foreach (var f in files)
            {
                if (string.Equals(f, keyFile, StringComparison.OrdinalIgnoreCase)) continue;

                var value = ReadTextSafe(f).Trim();
                if (value.Length > 1 && (value.Contains('\\') || value.Contains('/')) && Directory.Exists(value))
                    return value;
            }
        }
        catch
        {
            // 读不到就用系统默认值
        }

        return null;
    }

    private static string ReadTextSafe(string path)
    {
        try { return File.ReadAllText(path); }
        catch { return string.Empty; }
    }
}
