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

        // 已经是 0/0 说明上一次保活没正常收尾，状态文件里才是真正的原值
        if (ac == 0 && dc == 0 && AppState.Load(out int savedAc, out int savedDc))
        {
            _origAc = savedAc;
            _origDc = savedDc;
        }
        else
        {
            _origAc = ac;
            _origDc = dc;
        }
        AppState.Save(_origAc, _origDc);

        if (!LidPower.WriteLidAction(0, 0, out err))
        {
            AppState.Clear();
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
            if (AppState.Load(out int savedAc, out int savedDc))
                LidPower.WriteLidAction(savedAc, savedDc, out err);
            else
                LidPower.WriteLidAction(_origAc, _origDc, out err);
        }

        AppState.Clear();
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
