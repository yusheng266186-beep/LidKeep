using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace LidKeep;

internal sealed partial class MainForm
{
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var bg = new SolidBrush(Ui.Bg))
            g.FillRectangle(bg, ClientRectangle);

        // 1px 描边
        using (GraphicsPath border = Ui.RoundRect(
            new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), S(12f)))
        using (var pen = new Pen(Ui.WindowBorder, 1f))
            g.DrawPath(pen, border);

        // 左上角的小方块 + 标题
        using (GraphicsPath dot = Ui.RoundRect(new RectangleF(S(20f), S(20f), S(12f), S(12f)), S(3.5f)))
        using (var accent = new SolidBrush(Ui.Accent))
            g.FillPath(accent, dot);

        TextRenderer.DrawText(g, "合盖保活", _fTitle,
            new Rectangle(S(40f), S(14f), S(200f), S(26f)), Ui.TextMain,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        // 版本号紧跟在标题右边
        int titleW = TextRenderer.MeasureText("合盖保活", _fTitle,
            new Size(1000, 100), TextFormatFlags.NoPadding).Width;
        TextRenderer.DrawText(g, "LidKeep v1.0", _fTiny,
            new Rectangle(S(40f) + titleW + S(8f), S(14f), S(140f), S(26f)), Ui.TextMuted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        DrawStatusCard(g);
        DrawFooter(g);
    }

    /// <summary>中间那张卡片：状态灯、主文案、副文案、倒计时进度条。</summary>
    private void DrawStatusCard(Graphics g)
    {
        Rectangle card = CardRect;

        using (GraphicsPath path = Ui.RoundRect(card, S(12f)))
        {
            using (var fill = new SolidBrush(Ui.Card))
                g.FillPath(fill, path);
            using var border = new Pen(Ui.CardBorder, 1f);
            g.DrawPath(border, path);
        }

        int dotX = card.X + S(24f);
        int dotY = card.Y + S(30f);

        if (_active)
        {
            // 呼吸光晕 + 实心点
            double wave = 0.5 + 0.5 * Math.Sin(_pulse);
            int alpha = (int)(46.0 + 62.0 * wave);

            using (var halo = new SolidBrush(Color.FromArgb(alpha, Ui.Success)))
                g.FillEllipse(halo, dotX - S(9f), dotY - S(9f), S(18f), S(18f));
            using (var core = new SolidBrush(Ui.Success))
                g.FillEllipse(core, dotX - S(5f), dotY - S(5f), S(10f), S(10f));
        }
        else
        {
            using var idle = new SolidBrush(Ui.TextMuted);
            g.FillEllipse(idle, dotX - S(4f), dotY - S(4f), S(8f), S(8f));
        }

        string headline = _active ? "保活已开启 · 可以合盖了" : "保活未开启";
        TextRenderer.DrawText(g, headline, _fH1,
            new Rectangle(card.X + S(44f), card.Y + S(15f), card.Width - S(62f), S(24f)), Ui.TextMain,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        TextRenderer.DrawText(g, SubText(), _fBody,
            new Rectangle(card.X + S(44f), card.Y + S(43f), card.Width - S(62f), S(22f)),
            _lastError.Length > 0 ? Ui.Danger : Ui.TextSub,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        // 限时保活时画一条倒计时进度条
        if (!_active || _deadline == DateTime.MinValue || _deadlineTotalSec <= 0)
            return;

        double leftSec = (_deadline - DateTime.Now).TotalSeconds;
        double ratio = leftSec / _deadlineTotalSec;
        if (ratio < 0.0) ratio = 0.0;
        if (ratio > 1.0) ratio = 1.0;

        var track = new RectangleF(card.X + S(22f), card.Bottom - S(16f), card.Width - S(44f), S(4f));
        using (GraphicsPath trackPath = Ui.RoundRect(track, S(2f)))
        using (var trackBrush = new SolidBrush(Ui.PillBg))
            g.FillPath(trackBrush, trackPath);

        float filled = (float)(track.Width * ratio);
        if (filled <= S(3f)) return; // 太短就不画，免得圆角糊成一团

        var bar = new RectangleF(track.X, track.Y, filled, track.Height);
        using GraphicsPath barPath = Ui.RoundRect(bar, S(2f));
        using var grad = new LinearGradientBrush(bar, Ui.Accent, Color.FromArgb(140, 175, 255), 0f);
        g.FillPath(grad, barPath);
    }

    /// <summary>底部：分组标题、分隔线、几行说明。</summary>
    private void DrawFooter(Graphics g)
    {
        TextRenderer.DrawText(g, "保活时长", _fSmall,
            new Rectangle(S(20f), S(184f), S(120f), S(18f)), Ui.TextMuted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        using (var pen = new Pen(Ui.Divider, 1f))
            g.DrawLine(pen, S(20f), S(304f) + 0.5f, Width - S(20f), S(304f) + 0.5f);

        int y = S(312f);
        TextRenderer.DrawText(g, "合盖后屏幕熄灭属正常，机器仍会继续运行。", _fSmall,
            new Rectangle(S(20f), y, Width - S(40f), S(18f)), Ui.TextMuted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        y += S(18f);

        if (_onBattery)
        {
            TextRenderer.DrawText(g, "当前使用电池供电，合盖运行会持续耗电发热。", _fSmall,
                new Rectangle(S(20f), y, Width - S(40f), S(18f)), Ui.Warning,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            y += S(18f);
        }

        TextRenderer.DrawText(g, "停止保活或退出程序时，会自动恢复原合盖设置。", _fSmall,
            new Rectangle(S(20f), y, Width - S(40f), S(18f)), Ui.TextMuted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    /// <summary>卡片里的第二行文案，按状态切换。</summary>
    private string SubText()
    {
        if (_lastError.Length > 0)
            return _lastError;

        if (_active)
        {
            if (_deadline != DateTime.MinValue)
            {
                TimeSpan left = _deadline - DateTime.Now;
                if (left < TimeSpan.Zero) left = TimeSpan.Zero;
                return string.Format(CultureInfo.InvariantCulture,
                    "剩余 {0:00}:{1:00}:{2:00} · 到时自动恢复",
                    (int)left.TotalHours, left.Minutes, left.Seconds);
            }
            return "合盖后保持运行 · 停止时自动恢复原设置";
        }

        if (_curActionText.Length == 0)
            return "正在读取当前合盖设置…";

        return "当前合盖动作: " + _curActionText;
    }
}
