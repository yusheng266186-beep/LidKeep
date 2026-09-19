using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace LidKeep;

/// <summary>
/// 通过 powercfg 读写「合盖时采取的操作」，并用 SetThreadExecutionState 申请系统保持唤醒。
///
/// 合盖动作存放在电源方案「按钮和盖子」子组下的 LidAction 设置里：
///   0 = 不采取任何操作   1 = 睡眠   2 = 休眠   3 = 关机
/// 保活期间把它改成 0（不睡眠），退出时再写回原值。
/// </summary>
internal static class LidPower
{
    /// <summary>电源设置子组 GUID：按钮和盖子。</summary>
    public const string SubButtonsGuid = "4f971e89-eebd-4455-a8de-9e59040e7347";

    /// <summary>电源设置 GUID：合盖时采取的操作。</summary>
    public const string LidActionGuid = "5ca83367-6e45-459f-a27b-476b1d01c936";

    private const uint EsContinuous = 0x80000000;  // 让系统保持唤醒
    private const uint EsSystemRequired = 0x00000001; // 连屏幕一起保持点亮

    private static int _lastExitCode;
    private static string _lastOutput = "";

    private static string RunPowerCfg(string args)
    {
        var psi = new ProcessStartInfo("powercfg.exe", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // powercfg 输出跟随系统 ANSI 代码页（中文系统为 GBK）
            StandardOutputEncoding = Encoding.Default,
            StandardErrorEncoding = Encoding.Default,
        };

        using Process p = Process.Start(psi);
        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        _lastExitCode = p.ExitCode;
        _lastOutput = (stdout + stderr).Trim();
        return _lastOutput;
    }

    /// <summary>把输出里所有 0x… 形式的十六进制数抠出来。</summary>
    private static List<string> ExtractHex(string text)
    {
        var list = new List<string>();
        foreach (Match m in Regex.Matches(text, "0x([0-9A-Fa-f]{1,16})"))
            list.Add(m.Groups[1].Value);
        return list;
    }

    /// <summary>
    /// 读取当前合盖动作。powercfg -q 会分别给出「交流」和「直流」两行，
    /// 因此取最后两个十六进制值；若当前方案查不到则退回查询隐藏设置。
    /// </summary>
    public static bool ReadLidAction(out int ac, out int dc, out string err)
    {
        string output = RunPowerCfg($"-q SCHEME_CURRENT {SubButtonsGuid} {LidActionGuid}");
        List<string> hex = ExtractHex(output);

        if (hex.Count < 2)
        {
            output = RunPowerCfg($"-qh SCHEME_CURRENT {SubButtonsGuid} {LidActionGuid}");
            hex = ExtractHex(output);
        }

        if (hex.Count < 2)
        {
            ac = 0;
            dc = 0;
            err = output;
            return false;
        }

        ac = int.Parse(hex[hex.Count - 2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        dc = int.Parse(hex[hex.Count - 1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        err = null;
        return true;
    }

    /// <summary>写入合盖动作并立即激活当前方案。</summary>
    public static bool WriteLidAction(int ac, int dc, out string err)
    {
        RunPowerCfg($"-setacvalueindex SCHEME_CURRENT {SubButtonsGuid} {LidActionGuid} " +
                    ac.ToString(CultureInfo.InvariantCulture));
        if (_lastExitCode != 0)
        {
            err = _lastOutput;
            return false;
        }

        RunPowerCfg($"-setdcvalueindex SCHEME_CURRENT {SubButtonsGuid} {LidActionGuid} " +
                    dc.ToString(CultureInfo.InvariantCulture));
        if (_lastExitCode != 0)
        {
            err = _lastOutput;
            return false;
        }

        // 必须重新激活方案，改动才会生效
        RunPowerCfg("-setactive SCHEME_CURRENT");
        if (_lastExitCode != 0)
        {
            err = _lastOutput;
            return false;
        }

        err = null;
        return true;
    }

    /// <summary>把动作数值翻译成中文说明。</summary>
    public static string ActionName(int v) => v switch
    {
        0 => "不采取任何操作",
        1 => "睡眠",
        2 => "休眠",
        3 => "关机",
        _ => "未知(" + v.ToString(CultureInfo.InvariantCulture) + ")",
    };

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    /// <summary>开启 / 关闭系统级保持唤醒。</summary>
    public static void KeepAwake(bool on)
    {
        SetThreadExecutionState(on ? EsContinuous | EsSystemRequired : EsContinuous);
    }
}
