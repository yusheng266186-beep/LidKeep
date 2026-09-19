using System;
using System.Globalization;
using System.IO;

namespace LidKeep;

/// <summary>
/// 保活前的原始合盖动作记录。放在 %TEMP% 下，进程意外退出后
/// 下一次启动（或 --restore）还能把设置还原回去。
/// </summary>
internal static class AppState
{
    public static string StatePath => Path.Combine(Path.GetTempPath(), "lidkeep-state.txt");

    public static void Save(int ac, int dc)
    {
        try
        {
            File.WriteAllText(StatePath,
                "ac=" + ac.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
                "dc=" + dc.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
                "savedAt=" + DateTime.Now.ToString("s", CultureInfo.InvariantCulture) + Environment.NewLine);
        }
        catch
        {
            // 状态文件写不进去也不该影响保活本身
        }
    }

    public static bool Load(out int ac, out int dc)
    {
        ac = 1; // 默认回落到「睡眠」
        dc = 1;
        try
        {
            if (!File.Exists(StatePath))
                return false;

            foreach (string line in File.ReadAllLines(StatePath))
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
}
