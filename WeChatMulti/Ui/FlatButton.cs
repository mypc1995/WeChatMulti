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
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace WeChatMulti.Ui;

/// <summary>圆角矩形路径生成。</summary>
internal static class RoundPath
{
    public static GraphicsPath Build(Rectangle rect, int radius)
    {
        int d = Math.Max(2, radius * 2);
        d = Math.Min(d, Math.Min(rect.Width, rect.Height));
        int r = d / 2;

        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>扁平化圆角按钮，自绘填充与悬停/按下态。</summary>
public sealed class FlatButton : Control
{
    private bool _hover;
    private bool _pressed;

    /// <summary>DPI 缩放系数，由主窗体按当前屏幕 DPI 设置。</summary>
    public float UiScale { get; set; } = 1f;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Fill { get; set; } = Color.White;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillHover { get; set; } = Color.FromArgb(245, 246, 248);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillPressed { get; set; } = Color.FromArgb(232, 234, 237);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Outline { get; set; } = Color.FromArgb(226, 228, 232);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color TextColor { get; set; } = Color.FromArgb(31, 35, 41);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 10;

    public FlatButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        Font = new Font("Microsoft YaHei UI", 10.5f);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); Focus(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        Color fill = !Enabled ? Blend(Fill, BackColor, 0.55f)
                    : _pressed ? FillPressed
                    : _hover ? FillHover
                    : Fill;

        using (var path = RoundPath.Build(rect, (int)Math.Round(Radius * UiScale)))
        {
            using var brush = new SolidBrush(fill);
            g.FillPath(brush, path);

            if (Outline.A > 0)
            {
                using var pen = new Pen(!Enabled ? Blend(Outline, BackColor, 0.55f) : Outline);
                g.DrawPath(pen, path);
            }
        }

        Color fg = !Enabled ? Blend(TextColor, BackColor, 0.55f) : TextColor;
        TextRenderer.DrawText(
            g, Text, Font, rect, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    internal static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));
}

