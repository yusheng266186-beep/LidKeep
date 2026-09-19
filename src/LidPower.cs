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

    private const uint EsContinuous = 0x80000000;      // 让下面的请求持续有效，直到显式清除
    private const uint EsSystemRequired = 0x00000001;  // 阻止系统进入睡眠（任务不会中断）
    private const uint EsDisplayRequired = 0x00000002; // 阻止显示器关闭（屏幕保持点亮）

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

    /// <summary>
    /// 开启 / 关闭系统级保持唤醒。
    ///
    /// <paramref name="keepDisplayOn"/> 决定是否连屏幕一起保持点亮：
    ///
    /// - <c>false</c>（只带 ES_SYSTEM_REQUIRED）：系统不睡，跑着的任务不会中断，
    ///   但显示器仍会按电源计划关闭。合盖后屏幕熄灭 → Windows 进入锁屏界面。
    ///   这也是原程序的行为。
    /// - <c>true</c>（加上 ES_DISPLAY_REQUIRED）：显示器也不关，因此不会因为
    ///   “显示器关闭”而触发锁屏。代价是更耗电、屏幕一直亮着。
    ///
    /// 注意：这两个标志只能压制**空闲**触发的睡眠与息屏。合盖、按电源键这类
    /// 硬件事件由固件 / 驱动上报，应用层无法拦截；所以本程序的做法是把合盖动作
    /// 改成「不采取任何操作」（见 <see cref="WriteLidAction"/>）来回避睡眠，
    /// 再用这里的请求兜底。
    /// </summary>
    public static void KeepAwake(bool on, bool keepDisplayOn = false)
    {
        uint flags = EsContinuous;
        if (on)
        {
            flags |= EsSystemRequired;
            if (keepDisplayOn)
                flags |= EsDisplayRequired;
        }
        SetThreadExecutionState(flags);
    }
}
