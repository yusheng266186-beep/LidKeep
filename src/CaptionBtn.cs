using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LidKeep;

/// <summary>
/// 自绘的无边框窗口标题栏按钮：最小化（一条横线）与关闭（一个叉）。
/// </summary>
internal sealed class CaptionBtn : Control
{
    public bool IsClose;

    private bool _hover;

    public CaptionBtn()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_hover)
        {
            using GraphicsPath path = Ui.RoundRect(new RectangleF(0f, 0f, Width, Height), Width / 2f);
            using var brush = new SolidBrush(IsClose ? Ui.Danger : Ui.PillHover);
            g.FillPath(brush, path);
        }

        Color color = !_hover ? Ui.TextMuted : (IsClose ? Color.White : Ui.TextMain);
        using var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        float cx = Width / 2f;
        float cy = Height / 2f;
        float r = Width * 0.21f;

        if (IsClose)
        {
            g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
            g.DrawLine(pen, cx - r, cy + r, cx + r, cy - r);
        }
        else
        {
            g.DrawLine(pen, cx - r, cy + 0.5f, cx + r, cy + 0.5f);
        }
    }
}
