using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LidKeep;

/// <summary>
/// 「不限时 / 30 分钟 / 1 小时 …」的胶囊形分段选择器。
/// </summary>
internal sealed class PillBar : Control
{
    public string[] Items = Array.Empty<string>();
    public int[] Values = Array.Empty<int>();
    public int Selected;
    public bool EnabledLook = true;
    public float UiScale = 1f;

    private int _hoverIndex = -1;
    private int[] _widths = Array.Empty<int>();
    private int _gap = 6;

    public event EventHandler SelectedChanged;

    public PillBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Default;
    }

    private void MeasureAll()
    {
        if (Items == null) return;
        _widths = new int[Items.Length];
        _gap = (int)Math.Round(6f * UiScale);
        int padX = (int)Math.Round(12f * UiScale);

        for (int i = 0; i < Items.Length; i++)
        {
            Size sz = TextRenderer.MeasureText(Items[i], Font, new Size(1000, 100), TextFormatFlags.NoPadding);
            _widths[i] = sz.Width + padX * 2;
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        MeasureAll();
        Invalidate();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        MeasureAll();
        Invalidate();
    }

    private int HitTest(Point pt)
    {
        int x = 0;
        for (int i = 0; i < _widths.Length; i++)
        {
            if (new Rectangle(x, 0, _widths[i], Height).Contains(pt))
                return i;
            x += _widths[i] + _gap;
        }
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int hit = EnabledLook ? HitTest(e.Location) : -1;
        if (hit != _hoverIndex)
        {
            _hoverIndex = hit;
            Cursor = hit >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hoverIndex = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (EnabledLook && e.Button == MouseButtons.Left)
        {
            int hit = HitTest(e.Location);
            if (hit >= 0 && hit != Selected)
            {
                Selected = hit;
                Invalidate();
                SelectedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_widths.Length != Items.Length)
            MeasureAll();

        int x = 0;
        for (int i = 0; i < Items.Length; i++)
        {
            var r = new RectangleF(x + 0.5f, 0.5f, _widths[i] - 1f, Height - 1f);
            bool isSelected = i == Selected;
            Color fill = isSelected ? Ui.Accent : ((i == _hoverIndex && EnabledLook) ? Ui.PillHover : Ui.PillBg);
            Color fore = isSelected ? Color.White : Ui.TextSub;

            using (GraphicsPath path = Ui.RoundRect(r, (Height - 1f) / 2f))
            using (var brush = new SolidBrush(fill))
            {
                g.FillPath(brush, path);
            }

            TextRenderer.DrawText(g, Items[i], Font, new Rectangle(x, 0, _widths[i], Height), fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            x += _widths[i] + _gap;
        }

        // 保活进行中时整条选择器压暗，表示不可点
        if (!EnabledLook)
        {
            using var veil = new SolidBrush(Color.FromArgb(150, Ui.Bg));
            g.FillRectangle(veil, ClientRectangle);
        }
    }
}
