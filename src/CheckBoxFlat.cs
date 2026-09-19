using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LidKeep;

/// <summary>
/// 自绘的勾选框（方框 + 对勾）。
/// </summary>
internal sealed class CheckBoxFlat : Control
{
    private bool _checked;
    private bool _hover;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler CheckedChanged;

    public CheckBoxFlat()
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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            Checked = !Checked;
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int box = Math.Min(Height, 16);
        float radius = 4f;
        // 方框顶到控件上沿再垂直居中
        float top = (Height - box) / 2f;
        var boxRect = new RectangleF(0.5f, top + 0.5f, box - 1f, box - 1f);

        using (GraphicsPath path = Ui.RoundRect(boxRect, radius))
        {
            using var fill = new SolidBrush(_checked ? Ui.Accent : (_hover ? Ui.PillHover : Ui.PillBg));
            g.FillPath(fill, path);
            using var border = new Pen(_checked ? Ui.Accent : Ui.CardBorder, 1f);
            g.DrawPath(border, path);
        }

        if (_checked)
        {
            // 对勾
            using var pen = new Pen(Color.White, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };
            float cx = boxRect.X;
            float cy = boxRect.Y;
            g.DrawLines(pen, new[]
            {
                new PointF(cx + box * 0.24f, cy + box * 0.52f),
                new PointF(cx + box * 0.43f, cy + box * 0.71f),
                new PointF(cx + box * 0.77f, cy + box * 0.29f),
            });
        }

        // 文字
        int textLeft = box + 8;
        Color fore = Enabled ? Ui.TextSub : Ui.TextMuted;
        TextRenderer.DrawText(g, Text, Font,
            new Rectangle(textLeft, 0, Width - textLeft, Height), fore,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
