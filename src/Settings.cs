using System;
using System.Globalization;
using System.IO;

namespace LidKeep;

/// <summary>
/// 用户偏好设置。放在 %APPDATA%\LidKeep\settings.txt，
/// 只存少量键值对，避免为一个开关引入 JSON 依赖。
/// </summary>
internal static class Settings
{
    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LidKeep");

    private static string FilePath => Path.Combine(Dir, "settings.txt");

    /// <summary>
    /// 保活期间是否连屏幕一起保持点亮。
    ///
    /// true  —— SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED)
    ///          屏幕不灭，也就不会因为“显示器关闭”而触发锁屏；代价是更耗电。
    /// false —— 只请求系统不睡眠（原程序行为）。
    ///          任务不会中断，但合盖后屏幕会熄灭，Windows 随即进入锁屏界面。
    /// </summary>
    public static bool KeepDisplayOn { get; set; } = true;

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return;

            foreach (string line in File.ReadAllLines(FilePath))
            {
                string[] kv = line.Split('=');
                if (kv.Length != 2) continue;

                if (kv[0].Trim() == "keepDisplayOn")
                    KeepDisplayOn = kv[1].Trim() == "1";
            }
        }
        catch
        {
            // 读不到就用默认值
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath,
                "keepDisplayOn=" + (KeepDisplayOn ? "1" : "0") + Environment.NewLine);
        }
        catch
        {
        }
    }
}
