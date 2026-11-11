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
using WeChatMulti.Core;

namespace WeChatMulti.Ui;

/// <summary>数字选择器：1~10 个胶囊按钮，选中态高亮。</summary>
public sealed class ChipSelector : Control
{
    private const int PerRow = 5;
    private const int Gap = 10;
    private const int ChipHeight = 42;

    private readonly List<(Rectangle Rect, int Value)> _chips = [];
    private int _hoverValue = -1;

    private Color _chipBg = Color.FromArgb(233, 236, 239);
    private Color _chipBgHover = Color.FromArgb(221, 225, 230);
    private Color _chipSelected = Color.FromArgb(7, 193, 96);
    private Color _textIdle = Color.FromArgb(90, 96, 104);
    private Color _textSelected = Color.White;

    private int _selected = 3;
    private int _max = 10;

    /// <summary>DPI 缩放系数，由主窗体按当前屏幕 DPI 设置。</summary>
    public float UiScale { get; set; } = 1f;

    private int S(int v) => (int)Math.Round(v * UiScale);

    public event EventHandler? SelectionChanged;

    /// <summary>当前选中的窗口数。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Selected
    {
        get => _selected;
        set
        {
            int v = Num.Clamp(value, 1, _max);
            if (v == _selected) return;
            _selected = v;
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>最大可选数量。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum
    {
        get => _max;
        set
        {
            _max = Num.Clamp(value, 1, 10);
            if (_selected > _max) Selected = _max;
            Invalidate();
        }
    }

    public ChipSelector()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        Font = new Font("Microsoft YaHei UI", 11f);
        Cursor = Cursors.Hand;
    }

    private int RowCount => (int)Math.Ceiling(_max / (double)PerRow);

    /// <summary>两行胶囊需要的总高度，供外部布局使用。</summary>
    public int PreferredHeight => RowCount * S(ChipHeight) + (RowCount - 1) * S(Gap);

    private void BuildChips()
    {
        _chips.Clear();
        int gap = S(Gap);
        int chipW = (Width - gap * (PerRow - 1)) / PerRow;
        if (chipW <= 0) return;

        int chipH = S(ChipHeight);
        for (int i = 0; i < _max; i++)
        {
            int col = i % PerRow;
            int row = i / PerRow;
            _chips.Add((
                new Rectangle(col * (chipW + gap), row * (chipH + gap), chipW, chipH),
                i + 1));
        }
    }

    private int HitTest(Point p)
    {
        foreach (var (rect, value) in _chips)
            if (rect.Contains(p)) return value;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int hit = HitTest(e.Location);
        if (hit != _hoverValue) { _hoverValue = hit; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hoverValue = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int hit = HitTest(e.Location);
        if (hit > 0) Selected = hit;
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        BuildChips();

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        foreach (var (rect, value) in _chips)
        {
            bool selected = value == _selected;
            bool hover = value == _hoverValue;

            Color fill = selected ? _chipSelected : hover ? _chipBgHover : _chipBg;
            Color fg = selected ? _textSelected : _textIdle;

            using (var path = RoundPath.Build(rect, rect.Height / 2))
            {
                using var brush = new SolidBrush(fill);
                g.FillPath(brush, path);
            }

            TextRenderer.DrawText(
                g, value.ToString(), Font, rect, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}

