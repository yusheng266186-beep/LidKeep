using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LidKeep;

internal static class Program
{
    private static Mutex _guiMutex;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [STAThread]
    private static int Main(string[] args)
    {
        // WinExe 默认没有控制台；带参数运行时借用父进程控制台以便看到输出
        if (args.Length > 0)
        {
            AttachConsole(-1); // ATTACH_PARENT_PROCESS
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        }

        string mode = "gui";
        int keepSeconds = -1;
        bool forceGui = false;
        bool? displayOverride = null;

        Settings.Load();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--status":
                    mode = "status";
                    break;
                case "--restore":
                    mode = "restore";
                    break;
                case "--keep":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int sec))
                    {
                        keepSeconds = sec;
                        i++;
                    }
                    break;
                case "--gui":
                    forceGui = true;
                    break;
                // 命令行临时覆盖「保持屏幕常亮」，不写回配置文件
                case "--display":
                    displayOverride = true;
                    break;
                case "--no-display":
                    displayOverride = false;
                    break;
                case "--help":
                case "-h":
                case "/?":
                    mode = "help";
                    break;
                case "--version":
                    mode = "version";
                    break;
            }
        }

        if (displayOverride.HasValue)
            Settings.KeepDisplayOn = displayOverride.Value;

        // 启动自检：清掉「显示正在保活但原值已丢失」的误导性记录
        if (AppState.Repair())
            Console.WriteLine("提示: 检测到上一轮保活异常中断且原值已丢失，已清理残留状态。");

        // 显式给了时长又没要求 GUI，就走无界面模式，方便脚本调用
        if (mode == "gui" && keepSeconds >= 0 && !forceGui)
            mode = "headless";

        switch (mode)
        {
            case "status": return RunStatus();
            case "restore": return RunRestore();
            case "help": PrintHelp(); return 0;
            case "version": Console.WriteLine("LidKeep 1.0.0"); return 0;
            case "headless": return RunHeadless(keepSeconds);
            default: return RunGui(keepSeconds);
        }
    }

    private static int RunStatus()
    {
        if (!LidPower.ReadLidAction(out int ac, out int dc, out string err))
        {
            Console.WriteLine("读取合盖设置失败: " + err);
            return 1;
        }

        Console.WriteLine($"LidAction: AC={ac} ({LidPower.ActionName(ac)}), DC={dc} ({LidPower.ActionName(dc)})");

        if (AppState.Load(out int savedAc, out int savedDc))
        {
            Console.WriteLine($"SavedOriginal: AC={savedAc}, DC={savedDc}");
            Console.WriteLine("KeepActive: yes" + ((ac == 0 && dc == 0) ? "" : " (状态与设置不一致)"));
        }
        else
        {
            Console.WriteLine("KeepActive: no");
        }

        // 受保护备份才是还原时的权威来源
        if (AppState.LoadValidBackup(out int bakAc, out int bakDc))
            Console.WriteLine($"ProtectedOriginal: AC={bakAc}, DC={bakDc}  ({AppState.BackupPath})");

        return 0;
    }

    private static int RunRestore()
    {
        // 优先用受保护备份；没有备份时退回会话状态
        int ac, dc;
        bool haveBackup = AppState.LoadValidBackup(out ac, out dc);
        if (!haveBackup && !AppState.Load(out ac, out dc))
        {
            Console.WriteLine("没有找到保活记录，无需恢复。");
            return 0;
        }

        // 用户自己改过合盖设置就不要覆盖他的选择
        if (LidPower.ReadLidAction(out int curAc, out int curDc, out _) && (curAc != 0 || curDc != 0))
        {
            Console.WriteLine($"合盖设置已被手动改过 (AC={curAc}, DC={curDc})，保持不变。");
            AppState.ClearAll();
            return 0;
        }

        if (!LidPower.WriteLidAction(ac, dc, out string err))
        {
            Console.WriteLine("恢复失败: " + err);
            return 1;
        }

        AppState.ClearAll();
        Console.WriteLine($"已恢复合盖动作: AC={ac} ({LidPower.ActionName(ac)}), DC={dc} ({LidPower.ActionName(dc)})" +
                          (haveBackup ? "（来自原值备份）" : ""));
        return 0;
    }

    private static int RunHeadless(int seconds)
    {
        if (!LidPower.ReadLidAction(out int ac, out int dc, out string err))
        {
            Console.WriteLine("读取合盖设置失败: " + err);
            return 1;
        }

        // 原值的确定顺序，与 GUI 版一致：
        //   1) 受保护备份 —— 绝不覆盖；
        //   2) 会话状态里的有效原值 —— 提升为备份；
        //   3) 现场读到的非 0/0 值 —— 真正的原始设置；
        //   4) 都不成立 —— 回落「睡眠」，不写备份。
        int origAc, origDc;
        bool haveOrig = AppState.LoadValidBackup(out origAc, out origDc);

        if (!haveOrig && AppState.Load(out int sessionAc, out int sessionDc)
                      && sessionAc >= 1 && sessionAc <= 3
                      && sessionDc >= 1 && sessionDc <= 3)
        {
            origAc = sessionAc;
            origDc = sessionDc;
            haveOrig = true;
        }

        if (!haveOrig && ac >= 1 && dc >= 1)
        {
            origAc = ac;
            origDc = dc;
            haveOrig = true;
        }

        if (haveOrig)
            AppState.SaveBackup(origAc, origDc);
        else
        {
            origAc = 1;
            origDc = 1;
        }

        AppState.Save(origAc, origDc);

        if (!LidPower.WriteLidAction(0, 0, out err))
        {
            Console.WriteLine("写入合盖设置失败: " + err);
            return 1;
        }

        LidPower.KeepAwake(true, Settings.KeepDisplayOn);
        Console.WriteLine("保活已开启 (" + (seconds > 0 ? seconds + " 秒后自动恢复" : "按 Ctrl+C 停止") +
                          (Settings.KeepDisplayOn ? "，保持屏幕常亮" : "，屏幕仍会熄灭") +
                          ")。原设置: AC=" + origAc + ", DC=" + origDc);

        bool stop = false;
        Console.CancelKeyPress += (o, ce) =>
        {
            ce.Cancel = true; // 自己要负责收尾，不能直接被杀掉
            stop = true;
        };

        if (seconds > 0)
        {
            for (int i = 0; i < seconds * 2 && !stop; i++)
                Thread.Sleep(500);
        }
        else
        {
            while (!stop)
                Thread.Sleep(500);
        }

        LidPower.KeepAwake(false);
        LidPower.WriteLidAction(origAc, origDc, out err);
        AppState.ClearAll();
        Console.WriteLine($"已恢复合盖动作: AC={origAc} ({LidPower.ActionName(origAc)}), DC={origDc} ({LidPower.ActionName(origDc)})");
        return 0;
    }

    private static int RunGui(int keepSec)
    {
        _guiMutex = new Mutex(true, "LidKeep_SingleInstance_Mutex", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("合盖保活已经在运行了（请看系统托盘）。", "合盖保活",
                MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            return 0;
        }

        try { SetProcessDPIAware(); } catch { }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var form = new MainForm();
        if (keepSec >= 0)
            form.SetPendingKeep(keepSec);

        Application.Run(form);

        GC.KeepAlive(_guiMutex);
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("合盖保活 LidKeep 1.0");
        Console.WriteLine("用法:");
        Console.WriteLine("  LidKeep.exe                        打开图形界面（双击同理）");
        Console.WriteLine("  LidKeep.exe --keep 1800            后台保活 1800 秒后自动恢复（Ctrl+C 提前结束）");
        Console.WriteLine("  LidKeep.exe --gui --keep 1800      打开界面并立即开始限时保活");
        Console.WriteLine("  LidKeep.exe --status               查看当前合盖动作设置");
        Console.WriteLine("  LidKeep.exe --restore              恢复保活前的合盖动作");
        Console.WriteLine("  LidKeep.exe --version / --help");
        Console.WriteLine("");
        Console.WriteLine("屏幕选项（仅本次运行生效，不写回配置）:");
        Console.WriteLine("  --display                          保活时保持屏幕常亮（默认，不会锁屏）");
        Console.WriteLine("  --no-display                       只保系统不睡眠，允许屏幕熄灭并锁屏");
    }
}
