## 微信多开工具 v6.0.1

一键同时启动多个微信 4.x Windows 客户端实例，每个实例拥有独立的登录态与数据目录。

### 下载

| 文件 | 说明 |
| --- | --- |
| `WeChatMulti-<tag>.zip` | **推荐**。解压后直接运行 `WeChatMulti.exe` |
| `WeChatMulti.exe` | 裸 exe，需与同目录下的 `WeChatMulti.exe.config` 一起使用 |

### 运行环境

- Windows 10 / 11 x64
- .NET Framework 4.8（系统自带，**无需安装运行时**）
- 本机需已安装微信 4.x

### 使用方法

**图形界面**：选择要开启的窗口数（1~10）→ 点「一键启动微信」。若探测不到微信安装位置，点底部路径右侧「更换」手动指定 `Weixin.exe`。

**命令行**：

```cmd
WeChatMulti.exe --launch 5                           REM 拉起 5 个实例
WeChatMulti.exe --launch 5 --arrange grid            REM 启动后网格排列
WeChatMulti.exe --launch 5 --arrange cascade         REM 启动后层叠排列
WeChatMulti.exe --launch 3 --path "D:\...\Weixin.exe" REM 手动指定微信路径
```

退出码：`0` 成功 / `1` 未达到目标实例数 / `2` 未检测到微信安装。

### 本次更新

- 采用 **GPL-3.0** 许可证发布，源码与每个源文件均已附上 GPL 声明
- 新增 GitHub Actions：`ci.yml` 负责每次推送的构建校验，`release.yml` 在打标签时自动构建并发布 Release
- 修复 `WeChatMulti.Smoke` 中过期的 API 调用（`WeChatLocator.Detect()` → `Resolve()`），整个解决方案现在可零错误构建
- 修复 `WeChatMulti.Preview` 的一个可空性警告（CS8600），构建输出已无警告

### 原理简述

微信 4.x 的单实例检查与互斥体创建之间存在竞态窗口——多个进程在同一瞬间启动时，会在彼此拿到互斥体之前全部通过检查，从而各自成为一个独立实例。微信随后自动为每个实例分配 `net`、`net_1` … `net_9` 这样的独立数据目录，登录态与聊天记录天然隔离。

因此这个工具的关键不是反复重试启动，而是**把启动间隔压到足够小**，让所有进程都挤进同一个竞争窗口。

### 许可与源码

本项目基于 **GNU 通用公共许可证第 3 版（GPL-3.0）** 发布，完整条款见仓库中的 `LICENSE`。

按 GPL 要求，分发二进制产物时须提供对应源码。对应版本的完整源代码即本仓库内容：

https://github.com/mypc1995/WeChatMulti/tree/v6.0.1

### 声明

本工具利用的是微信自身单实例检查中的竞态窗口，属于对客户端行为的技术性调用。多开可能违反微信用户服务协议，请仅在个人学习与测试场景下使用，风险自负。

作者：Evil_Pig
