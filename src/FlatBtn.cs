using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LidKeep;

/// <summary>
/// 无边框圆角按钮，支持悬停 / 按下三态配色。
/// </summary>
internal sealed class FlatBtn : Control
{
    public Color BaseColor = Ui.Accent;
    public Color HoverColor = Ui.AccentHover;
    public Color PressColor = Ui.AccentPress;
    public Color TextColor = Color.White;
    public float Radius = 10f;

    private bool _hover;
    private bool _press;

    public FlatBtn()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
    }

    public void SetPalette(Color baseColor, Color hoverColor, Color pressColor)
    {
        BaseColor = baseColor;
        HoverColor = hoverColor;
        PressColor = pressColor;
        Invalidate();
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
        _press = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _press = true;
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _press = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color fill = _press ? PressColor : (_hover ? HoverColor : BaseColor);
        using (GraphicsPath path = Ui.RoundRect(
            new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), Radius))
        using (var brush = new SolidBrush(fill))
        {
            g.FillPath(brush, path);
        }

        TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height - 1), TextColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
