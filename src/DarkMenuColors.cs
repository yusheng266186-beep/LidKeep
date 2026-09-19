using System.Drawing;
using System.Windows.Forms;

namespace LidKeep;

/// <summary>
/// 深色托盘右键菜单的配色表。
/// </summary>
internal sealed class DarkMenuColors : ProfessionalColorTable
{
    private static readonly Color MenuBg = Color.FromArgb(24, 28, 38);
    private static readonly Color MenuSel = Color.FromArgb(42, 48, 63);
    private static readonly Color MenuBorderColor = Color.FromArgb(48, 55, 70);

    public override Color ToolStripDropDownBackground => MenuBg;
    public override Color MenuBorder => MenuBorderColor;
    public override Color MenuItemBorder => MenuSel;
    public override Color MenuItemSelected => MenuSel;
    public override Color ImageMarginGradientBegin => MenuBg;
    public override Color ImageMarginGradientMiddle => MenuBg;
    public override Color ImageMarginGradientEnd => MenuBg;
    public override Color MenuItemSelectedGradientBegin => MenuSel;
    public override Color MenuItemSelectedGradientEnd => MenuSel;
    public override Color MenuItemPressedGradientBegin => MenuSel;
    public override Color MenuItemPressedGradientEnd => MenuSel;
    public override Color SeparatorDark => MenuBorderColor;
    public override Color SeparatorLight => MenuBorderColor;
}
