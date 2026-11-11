using System.Drawing.Drawing2D;
using WeChatMulti.Core;
using WeChatMulti.Ui;

namespace WeChatMulti;

public sealed class MainForm : Form
{
    public const string AppName = "微信多开工具";
    public const string AppVersion = "V6";
    public const string Author = "Evil_Pig";

    private static readonly Color Bg = Color.FromArgb(244, 246, 248);
    private static readonly Color Green = Color.FromArgb(7, 193, 96);
    private static readonly Color GreenHover = Color.FromArgb(6, 173, 86);
    private static readonly Color GreenPressed = Color.FromArgb(5, 155, 78);
    private static readonly Color TextMain = Color.FromArgb(31, 35, 41);
    private static readonly Color TextSub = Color.FromArgb(138, 144, 153);
    private static readonly Color Divider = Color.FromArgb(232, 235, 238);
    private static readonly Color Danger = Color.FromArgb(224, 92, 82);
    private static readonly Color PathLink = Color.FromArgb(7, 150, 80);

    private readonly ChipSelector _chips = new();
    private readonly FlatButton _btnStart = new();
    private readonly FlatButton _btnStop = new();
    private readonly FlatButton _btnChange = new();
    private readonly CheckBox _chkArrange = new() { Text = "启动后自动排列窗口", AutoSize = true, Checked = true };
    private readonly Label _lblSubtitle = new();
    private readonly Label _lblStatus = new();
    private readonly Label _lblPath = new();
    private readonly Label _lblAuthor = new();
    private readonly Label _lblVersion = new();

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1500 };

    private WeChatInstall? _install;
    private InstanceManager? _manager;
    private CancellationTokenSource? _cts;
    private bool _busy;
    private DateTime _messageUntil = DateTime.MinValue;

    public MainForm()
    {
        Text = $"{AppName} {AppVersion}";
        Icon = LoadAppIcon();
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Bg;
        Font = new Font("Microsoft YaHei UI", 9f);
        AllowDrop = true;

        // 布局由代码按 DPI 手工计算，关闭 WinForms 自动缩放避免二次放大
        AutoScaleMode = AutoScaleMode.None;
        AutoScaleDimensions = new SizeF(96f, 96f);

        BuildUi();

        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();

        DetectWeChat();
        RefreshStatus();
    }

    /// <summary>取程序自身的图标（标题栏、任务栏、Alt+Tab）。</summary>
    private static Icon? LoadAppIcon()
    {
        try
        {
            // .NET Framework 上 Environment.ExecutablePath 不存在，用 WinForms 的
            string exe = Application.ExecutablePath;
            return Icon.ExtractAssociatedIcon(exe);
        }
        catch
        {
            return null;   // 取不到就用系统默认图标，不影响功能
        }
    }

    /// <summary>按 96 DPI 为基准，把设计稿像素换算成当前屏幕的实际像素。</summary>
    private int S(int v) => (int)Math.Round(v * DeviceDpi / 96f);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        float scale = DeviceDpi / 96f;
        _chips.UiScale = scale;
        _btnStart.UiScale = scale;
        _btnStop.UiScale = scale;
        _btnChange.UiScale = scale;

        ClientSize = new Size(S(470), S(440));
        LayoutUi();
    }

    private void BuildUi()
    {
        _lblSubtitle.Text = "选择要开启的窗口数";
        _lblSubtitle.ForeColor = TextSub;
        _lblSubtitle.Font = new Font("Microsoft YaHei UI", 9.5f);
        _lblSubtitle.TextAlign = ContentAlignment.MiddleCenter;

        _lblStatus.TextAlign = ContentAlignment.MiddleCenter;
        _lblStatus.Font = new Font("Microsoft YaHei UI", 9f);

        _btnStart.Text = "一键启动微信";
        _btnStart.Font = new Font("Microsoft YaHei UI", 11f);
        _btnStart.Fill = Green;
        _btnStart.FillHover = GreenHover;
        _btnStart.FillPressed = GreenPressed;
        _btnStart.Outline = Color.Empty;
        _btnStart.TextColor = Color.White;
        _btnStart.BackColor = Bg;
        _btnStart.Click += async (_, _) => await StartAsync();

        _btnStop.Text = "关闭全部微信";
        _btnStop.Fill = Color.White;
        _btnStop.FillHover = Color.FromArgb(247, 248, 249);
        _btnStop.FillPressed = Color.FromArgb(238, 240, 243);
        _btnStop.Outline = Color.FromArgb(224, 227, 231);
        _btnStop.TextColor = TextMain;
        _btnStop.BackColor = Bg;
        _btnStop.Click += (_, _) => StopAll();

        _btnChange.Text = "更换";
        _btnChange.Font = new Font("Microsoft YaHei UI", 8.5f);
        _btnChange.Radius = 8;
        _btnChange.Fill = Color.White;
        _btnChange.FillHover = Color.FromArgb(238, 250, 243);
        _btnChange.FillPressed = Color.FromArgb(228, 246, 236);
        _btnChange.Outline = Color.FromArgb(206, 226, 215);
        _btnChange.TextColor = PathLink;
        _btnChange.BackColor = Bg;
        _btnChange.Click += (_, _) => BrowseForPath();

        _lblPath.AutoEllipsis = true;
        _lblPath.TextAlign = ContentAlignment.MiddleLeft;
        _lblPath.ForeColor = TextSub;
        _lblPath.Font = new Font("Microsoft YaHei UI", 8.5f);
        _lblPath.Cursor = Cursors.Hand;
        _lblPath.Click += (_, _) => BrowseForPath();
        _lblPath.ContextMenuStrip = BuildPathMenu();

        _chkArrange.ForeColor = TextSub;
        _chkArrange.BackColor = Bg;
        _chkArrange.Cursor = Cursors.Hand;

        _lblAuthor.Text = $"作者：{Author}";
        _lblVersion.Text = AppVersion;
        _lblAuthor.ForeColor = TextSub;
        _lblVersion.ForeColor = TextSub;
        _lblAuthor.Font = new Font("Microsoft YaHei UI", 8.5f);
        _lblVersion.Font = new Font("Microsoft YaHei UI", 8.5f);
        _lblVersion.TextAlign = ContentAlignment.MiddleRight;

        Controls.AddRange([_lblSubtitle, _chips, _btnStart, _btnStop, _chkArrange, _lblStatus,
                            _lblPath, _btnChange, _lblAuthor, _lblVersion]);
        Resize += (_, _) => LayoutUi();

        // ClientSize 在订阅 Resize 之前就已设定，该事件不会再触发，必须主动排一次
        LayoutUi();

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
    }

    private ContextMenuStrip BuildPathMenu()
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        menu.Items.Add("重新自动检测", null, (_, _) => { WeChatLocator.ClearCustomPath(); DetectWeChat(); });
        menu.Items.Add("打开所在文件夹", null, (_, _) => OpenInstallFolder());
        return menu;
    }

    private void LayoutUi()
    {
        int pad = S(32);
        int w = ClientSize.Width - pad * 2;
        int y = S(24);

        _lblSubtitle.SetBounds(pad, y, w, S(22));
        y += S(30);

        _chips.SetBounds(pad, y, w, _chips.PreferredHeight);
        y += _chips.PreferredHeight + S(20);

        _btnStart.SetBounds(pad, y, w, S(52));
        y += S(62);

        _btnStop.SetBounds(pad, y, w, S(42));
        y += S(54);

        _chkArrange.SetBounds(pad + S(2), y, w - S(4), S(22));
        y += S(30);

        _lblStatus.SetBounds(pad, y, w, S(24));
        y += S(34);

        int btnW = S(58);
        _lblPath.SetBounds(pad, y, w - btnW - S(8), S(22));
        _btnChange.SetBounds(pad + w - btnW, y - S(1), btnW, S(24));

        int footerY = ClientSize.Height - S(42);
        _lblAuthor.SetBounds(pad, footerY, w / 2, S(20));
        _lblVersion.SetBounds(ClientSize.Width - pad - w / 2, footerY, w / 2, S(20));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Divider);
        int y = ClientSize.Height - S(54);
        e.Graphics.DrawLine(pen, S(32), y, ClientSize.Width - S(32), y);
    }

    // ---------- 路径 ----------

    private void DetectWeChat()
    {
        var install = WeChatLocator.Resolve();
        if (install is null)
        {
            _install = null;
            _manager = null;
            _btnStart.Enabled = false;
            UpdatePathLabel();
            SetStatus("未找到微信，点击下方「更换」指定路径", true, 0);
            return;
        }

        _install = install;
        _manager = new InstanceManager(install.ExePath);
        _btnStart.Enabled = !_busy;
        UpdatePathLabel();
    }

    private void UpdatePathLabel()
    {
        _lblPath.Text = _install is null
            ? "微信：未指定（点击「更换」选择）"
            : "微信：" + _install.ExePath;
    }

    private void BrowseForPath()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "选择微信主程序，或直接输入安装文件夹路径",
            Filter = "微信主程序 (Weixin.exe;WeChat.exe)|Weixin.exe;WeChat.exe|可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            // 允许用户直接键入文件夹路径
            CheckFileExists = false,
            ValidateNames = false,
        };

        if (_install is not null)
        {
            try { dlg.InitialDirectory = _install.InstallDir; } catch { /* 路径可能已不存在 */ }
        }

        if (dlg.ShowDialog(this) == DialogResult.OK)
            ApplyUserPath(dlg.FileName);
    }

    private void ApplyUserPath(string input)
    {
        var install = WeChatLocator.ResolveInstall(input);
        if (install is null)
        {
            SetStatus($"认不出这个路径：{input}", true, 6);
            return;
        }

        _install = install;
        _manager = new InstanceManager(install.ExePath);
        WeChatLocator.SaveCustomPath(install.ExePath);

        _btnStart.Enabled = !_busy;
        UpdatePathLabel();
        SetStatus($"已指定：{install.ExePath}", false, 5);
    }

    private void OpenInstallFolder()
    {
        if (_install is null) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", _install.InstallDir)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            SetStatus("打不开：" + ex.Message, true, 5);
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;

        // 拖进来的可能是 Weixin.exe 本体，也可能是整个安装目录
        var install = WeChatLocator.ResolveInstall(paths[0]);
        if (install is null)
        {
            SetStatus("拖进来的不是微信程序", true, 5);
            return;
        }

        ApplyUserPath(install.ExePath);
    }

    // ---------- 多开 ----------

    private async Task StartAsync()
    {
        if (_busy) return;

        if (_manager is null) DetectWeChat();
        if (_manager is null)
        {
            SetStatus("还没有指定微信路径", true, 4);
            return;
        }

        int count = _chips.Selected;
        var manager = _manager;

        _cts = new CancellationTokenSource();
        SetBusy(true, $"正在并发启动 {count} 个窗口…");

        try
        {
            var report = await Task.Run(() => manager.LaunchTo(count, Post, _cts.Token));

            if (report.Success)
            {
                SetStatus($"已开启 {report.Alive} 个窗口，逐个扫码登录即可", false, 6);

                if (_chkArrange.Checked)
                {
                    // 等窗口真正创建出来再排布
                    await Task.Delay(2500);
                    WindowArranger.Arrange(manager.RunningPids(), ArrangeMode.网格平铺);
                }
            }
            else
            {
                SetStatus($"只开启了 {report.Alive} 个，请稍后再试一次", true, 8);
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("已取消", false, 3);
        }
        catch (Exception ex)
        {
            SetStatus("出错：" + ex.Message, true, 8);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    private void StopAll()
    {
        if (_manager is null || _busy) return;

        int killed = _manager.KillAll();
        SetStatus(killed > 0 ? $"已关闭 {killed} 个窗口" : "当前没有运行中的微信", false, 4);
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _busy = busy;
        _btnStart.Enabled = !busy && _manager is not null;
        _btnStop.Enabled = !busy;
        _chips.Enabled = !busy;
        _chkArrange.Enabled = !busy;
        _btnChange.Enabled = !busy;
        _lblPath.Enabled = !busy;
        _btnStart.Text = busy ? "正在启动…" : "一键启动微信";
        UseWaitCursor = busy;
        if (status is not null) SetStatus(status, busy, busy ? 0 : 3);
    }

    private void SetStatus(string text, bool error, double seconds)
    {
        if (InvokeRequired) { BeginInvoke(() => SetStatus(text, error, seconds)); return; }
        _lblStatus.Text = text;
        _lblStatus.ForeColor = error ? Danger : TextSub;
        _messageUntil = seconds > 0 ? DateTime.Now.AddSeconds(seconds) : DateTime.MaxValue;
    }

    private void Post(string message) => SetStatus(message, false, 4);

    private void RefreshStatus()
    {
        if (_busy || DateTime.Now < _messageUntil) return;

        int running = _manager?.RunningCount() ?? 0;
        _lblStatus.Text = running > 0
            ? $"当前运行中 {running} / {InstanceManager.MaxInstances} 个窗口"
            : "点击上方按钮即可多开微信";
        _lblStatus.ForeColor = TextSub;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        _cts?.Cancel();
        base.OnFormClosed(e);
    }
}
