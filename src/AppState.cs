using System;
using System.Globalization;
using System.IO;

namespace LidKeep;

/// <summary>
/// 保活前的原始合盖动作记录，用两个文件分开存：
///
/// <list type="bullet">
/// <item><description><b>会话状态</b> <c>%TEMP%\lidkeep-state.txt</c> —— 本次保活的记录，
/// 正常停止时删除。旧版本只有这一个文件。</description></item>
/// <item><description><b>原值备份</b> <c>%TEMP%\lidkeep-original.txt</c> —— 保护真正的原值，
/// 只有在确认拿到有效原值时才会写入，<b>并且已经有有效备份时不覆盖</b>；
/// 只有「正常停止保活」或「恢复成功」才删除。</description></item>
/// </list>
///
/// 为什么要两个文件：旧版本只靠单一文件，一旦保活被强杀（任务管理器结束进程、
/// 断电、蓝屏），下一次保活会因为「当前已经是 0/0」而把 0/0 当成原值存回去，
/// 于是真正的原始设置被永久覆盖，之后再也还原不回来 —— 实测中确实踩到过。
///
/// 现在的规则是：<b>0/0 永远不是有效的原值</b>（「不采取任何操作」只可能是保活
/// 自己写进去的），因此它既不会被接受为原值，也不会覆盖已有的备份。
/// </summary>
internal static class AppState
{
    /// <summary>本次保活的状态记录。</summary>
    public static string StatePath => Path.Combine(Path.GetTempPath(), "lidkeep-state.txt");

    /// <summary>原始合盖动作的受保护备份。</summary>
    public static string BackupPath => Path.Combine(Path.GetTempPath(), "lidkeep-original.txt");

    /// <summary>合盖动作的合法取值：0 不操作、1 睡眠、2 休眠、3 关机。</summary>
    private static bool IsValidOriginal(int ac, int dc)
        => ac >= 1 && ac <= 3 && dc >= 1 && dc <= 3;

    // ————————————————————————————————— 会话状态

    public static void Save(int ac, int dc) => Write(StatePath, ac, dc);

    public static bool Load(out int ac, out int dc)
    {
        ac = 1; // 默认回落到「睡眠」
        dc = 1;
        return Read(StatePath, ref ac, ref dc);
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(StatePath))
                File.Delete(StatePath);
        }
        catch
        {
        }
    }

    // ————————————————————————————————— 原值备份

    /// <summary>
    /// 写入原值备份。若已存在有效备份则不覆盖 —— 这是防止原值被覆盖的关键。
    /// 传入无效值（0/0）时直接忽略。
    /// </summary>
    public static void SaveBackup(int ac, int dc)
    {
        if (!IsValidOriginal(ac, dc))
            return;

        if (LoadValidBackup(out _, out _))
            return;

        Write(BackupPath, ac, dc);
    }

    /// <summary>读取备份，并且只在值合法时返回 true。</summary>
    public static bool LoadValidBackup(out int ac, out int dc)
    {
        ac = 0;
        dc = 0;
        if (!Read(BackupPath, ref ac, ref dc))
            return false;

        if (!IsValidOriginal(ac, dc))
            return false;

        return true;
    }

    public static void ClearBackup()
    {
        try
        {
            if (File.Exists(BackupPath))
                File.Delete(BackupPath);
        }
        catch
        {
        }
    }

    // ————————————————————————————————— 组合操作

    /// <summary>正常收尾：两个文件都清掉。</summary>
    public static void ClearAll()
    {
        Clear();
        ClearBackup();
    }

    /// <summary>
    /// 启动时的自检：如果会话状态显示「正在保活」（0/0）却拿不到有效原值，
    /// 说明上一轮保活异常中断且原值已不可恢复，此时清掉误导性的记录。
    /// </summary>
    public static bool Repair()
    {
        if (!Load(out int ac, out int dc))
            return false;

        bool activeLooking = ac == 0 && dc == 0;
        if (!activeLooking)
            return false;

        if (LoadValidBackup(out _, out _))
            return false;

        Clear();
        return true;
    }

    // ————————————————————————————————— 底层读写

    private static void Write(string path, int ac, int dc)
    {
        try
        {
            File.WriteAllText(path,
                "ac=" + ac.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
                "dc=" + dc.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
                "savedAt=" + DateTime.Now.ToString("s", CultureInfo.InvariantCulture) + Environment.NewLine);
        }
        catch
        {
            // 状态文件写不进去也不该影响保活本身
        }
    }

    private static bool Read(string path, ref int ac, ref int dc)
    {
        try
        {
            if (!File.Exists(path))
                return false;

            foreach (string line in File.ReadAllLines(path))
            {
                string[] kv = line.Split('=');
                if (kv.Length != 2) continue;

                if (kv[0] == "ac")
                    ac = int.Parse(kv[1], CultureInfo.InvariantCulture);
                else if (kv[0] == "dc")
                    dc = int.Parse(kv[1], CultureInfo.InvariantCulture);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
