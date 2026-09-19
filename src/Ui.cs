using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace LidKeep;

/// <summary>
/// 全局配色与绘图辅助。
/// </summary>
internal static class Ui
{
    public static readonly Color Bg = Color.FromArgb(14, 16, 21);
    public static readonly Color WindowBorder = Color.FromArgb(38, 44, 58);
    public static readonly Color Card = Color.FromArgb(22, 26, 35);
    public static readonly Color CardBorder = Color.FromArgb(35, 41, 54);

    public static readonly Color TextMain = Color.FromArgb(233, 236, 243);
    public static readonly Color TextSub = Color.FromArgb(152, 161, 179);
    public static readonly Color TextMuted = Color.FromArgb(110, 120, 139);

    public static readonly Color Accent = Color.FromArgb(92, 124, 250);
    public static readonly Color AccentHover = Color.FromArgb(112, 140, 255);
    public static readonly Color AccentPress = Color.FromArgb(76, 106, 226);

    public static readonly Color Danger = Color.FromArgb(214, 69, 74);
    public static readonly Color DangerHover = Color.FromArgb(232, 86, 91);
    public static readonly Color DangerPress = Color.FromArgb(186, 55, 60);

    public static readonly Color Success = Color.FromArgb(52, 211, 153);
    public static readonly Color Warning = Color.FromArgb(240, 180, 65);

    public static readonly Color PillBg = Color.FromArgb(27, 31, 41);
    public static readonly Color PillHover = Color.FromArgb(36, 42, 56);

    public static readonly Color Divider = Color.FromArgb(27, 32, 43);

    /// <summary>构造一个圆角矩形路径；半径会自动收敛到矩形尺寸以内。</summary>
    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2f;
        if (d > r.Height) d = r.Height;
        if (d > r.Width) d = r.Width;

        if (d < 0.2f)
        {
            path.AddRectangle(r);
            return path;
        }

        path.AddArc(r.X, r.Y, d, d, 180f, 90f);
        path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
        path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    /// <summary>按优先级挑选一个可用的中文字体。</summary>
    public static Font MakeFont(float pt, FontStyle style)
    {
        string[] preferred = { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" };
        foreach (string name in preferred)
        {
            try
            {
                var font = new Font(name, pt, style);
                if (string.Equals(font.FontFamily.Name, name, StringComparison.OrdinalIgnoreCase))
                    return font;
                font.Dispose();
            }
            catch
            {
                // 该字体不存在，继续尝试下一个
            }
        }
        return new Font(FontFamily.GenericSansSerif, pt, style);
    }
}
