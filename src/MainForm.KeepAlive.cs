using System;
using System.Windows.Forms;

namespace LidKeep;

internal sealed partial class MainForm
{
    private void OnTick(object sender, EventArgs e)
    {
        _tick++;
        _pulse += 0.16; // 呼吸灯相位

        if (_active && _deadline != DateTime.MinValue && DateTime.Now >= _deadline)
            StopKeep(notify: true);

        // 每 5 秒刷新一次「当前合盖动作」和电源状态
        if (_tick % 10 == 0)
            RefreshActionText();

        if (_active || _tick % 10 == 0)
            Invalidate();
    }

    private void OnPrimaryClick()
    {
        if (_active)
            StopKeep(notify: false);
        else
            StartKeep(Durations[_pills.Selected]);
    }

    /// <summary>开始保活：记下原值 → 改成「不采取任何操作」→ 申请系统保持唤醒。</summary>
    private void StartKeep(int seconds)
    {
        if (_active) return;

        if (!LidPower.ReadLidAction(out int ac, out int dc, out string err))
        {
            _lastError = "读取合盖设置失败";
            Invalidate();
            MessageBox.Show(this, "读取当前合盖设置失败：\r\n" + err, "合盖保活",
                MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            return;
        }

        // 原值的确定顺序（先信任已保存的，再信任现场读到的）：
        //   1) 受保护备份里已有有效原值 —— 直接沿用，绝不覆盖；
        //   2) 会话状态里有有效原值 —— 提升为备份；
        //   3) 现场读到的不是 0/0 —— 那就是真正的原始设置，记入备份；
        //   4) 都不成立（说明上一轮被强杀且原值已丢失）—— 回落到「睡眠」，
        //      并且不写备份，避免把无效值固化下来。
        bool haveOrig = AppState.LoadValidBackup(out int origAc, out int origDc);

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
        {
            AppState.SaveBackup(origAc, origDc);
        }
        else
        {
            origAc = 1;
            origDc = 1;
        }

        _origAc = origAc;
        _origDc = origDc;
        AppState.Save(_origAc, _origDc);

        if (!LidPower.WriteLidAction(0, 0, out err))
        {
            AppState.ClearAll();
            _lastError = "修改合盖设置失败";
            Invalidate();
            MessageBox.Show(this, "修改合盖设置失败：\r\n" + err, "合盖保活",
                MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            return;
        }

        LidPower.KeepAwake(true, Settings.KeepDisplayOn);
        _active = true;
        _lastError = "";
        _keepStart = DateTime.Now;
        _deadlineTotalSec = seconds;
        _deadline = seconds > 0 ? DateTime.Now.AddSeconds(seconds) : DateTime.MinValue;

        UpdateTexts();
        RefreshActionText();
        UpdateTray();
        Invalidate();
    }

    /// <summary>停止保活并把合盖动作还原。</summary>
    private void StopKeep(bool notify)
    {
        if (!_active) return;

        // 只有当前仍是「不采取任何操作」时才还原，避免覆盖用户手动改过的设置
        if (LidPower.ReadLidAction(out int ac, out int dc, out string err) && ac == 0 && dc == 0)
        {
            if (RestoreValues(out int targetAc, out int targetDc))
                LidPower.WriteLidAction(targetAc, targetDc, out err);
        }

        // 正常收尾：会话状态和原值备份一起清掉
        AppState.ClearAll();
        LidPower.KeepAwake(false); // 关闭时传 false，清除全部请求
        _active = false;
        _deadline = DateTime.MinValue;
        _deadlineTotalSec = 0;

        UpdateTexts();
        RefreshActionText();
        UpdateTray();
        Invalidate();

        if (!notify || _tray == null) return;
        try
        {
            _tray.ShowBalloonTip(4000, "合盖保活", "保活已结束，已恢复原来的合盖设置。", ToolTipIcon.Info);
        }
        catch
        {
        }
    }

    /// <summary>
    /// 决定该把合盖动作还原成什么值。优先级：
    /// 受保护备份 → 会话状态 → 进程内记住的原值。
    /// 三者都拿不到时回落到「睡眠」，保证不会把 0/0 写回去。
    /// </summary>
    private bool RestoreValues(out int ac, out int dc)
    {
        if (AppState.LoadValidBackup(out ac, out dc))
            return true;

        if (AppState.Load(out ac, out dc) && ac >= 1 && ac <= 3 && dc >= 1 && dc <= 3)
            return true;

        if (_origAc >= 1 && _origAc <= 3 && _origDc >= 1 && _origDc <= 3)
        {
            ac = _origAc;
            dc = _origDc;
            return true;
        }

        ac = 1;
        dc = 1;
        return true;
    }

    /// <summary>刷新主按钮文案与配色，以及选择器的可用状态。</summary>
    private void UpdateTexts()
    {
        _btnPrimary.Text = _active ? "停止保活并恢复" : "开启保活";
        if (_active)
            _btnPrimary.SetPalette(Ui.Danger, Ui.DangerHover, Ui.DangerPress);
        else
            _btnPrimary.SetPalette(Ui.Accent, Ui.AccentHover, Ui.AccentPress);

        _pills.EnabledLook = !_active;
        _pills.Invalidate();

        // 保活期间不允许改这个开关，避免和已经生效的请求状态不一致
        _chkDisplay.Enabled = !_active;
        _chkDisplay.Invalidate();
    }

    /// <summary>重新读取合盖动作与是否在使用电池。</summary>
    private void RefreshActionText()
    {
        if (LidPower.ReadLidAction(out int ac, out int dc, out _))
            _curActionText = "交流 " + LidPower.ActionName(ac) + " · 电池 " + LidPower.ActionName(dc);

        try
        {
            _onBattery = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
        }
        catch
        {
        }
    }
}
