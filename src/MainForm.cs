using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LidKeep;

/// <summary>
/// 无边框深色主窗口。窗口本身就是一块圆角卡片，标题栏按钮和
/// 「保活时长」选择器都是自绘控件。
/// </summary>
internal sealed partial class MainForm : Form
{
    private const int DesignW = 400;
    private const int DesignH = 376;

    /// <summary>可选时长（秒）。0 表示不限时。</summary>
    private static readonly int[] Durations = { 0, 1800, 3600, 7200, 14400 };
    private static readonly string[] DurationNames = { "不限时", "30 分钟", "1 小时", "2 小时", "4 小时" };

    /// <summary>DPI 缩放系数（96 DPI 时为 1.0）。</summary>
    private float _scale = 1f;

    private Font _fTitle, _fH1, _fBody, _fSmall, _fPill, _fBtn, _fTiny;

    private FlatBtn _btnPrimary;
    private PillBar _pills;
    private CaptionBtn _btnMin, _btnClose;

    private NotifyIcon _tray;
    private ContextMenuStrip _trayMenu;
    private ToolStripMenuItem _miShow, _miStart, _miStop, _miExit;
    private Timer _timer;
    private Icon _appIcon;

    // —— 状态 ——
    private bool _active;          // 保活是否进行中
    private bool _exiting;         // 真正要退出（区别于关闭到托盘）
    private bool _balloonShown;    // 托盘气泡只提示一次
    private bool _onBattery;
    private DateTime _keepStart = DateTime.MinValue;
    private DateTime _deadline = DateTime.MinValue;
    private int _deadlineTotalSec;
    private int _origAc = 1;
    private int _origDc = 1;
    private string _curActionText = "";
    private string _lastError = "";
    private int _tick;
    private double _pulse;
    private int _pendingKeepSeconds = -1;

    // —— 拖动窗口 ——
    private bool _dragging;
    private Point _dragStartScreen;
    private Point _dragStartLoc;

    /// <summary>中间那张状态卡片的矩形。</summary>
    private Rectangle CardRect
    {
        get
        {
            int pad = S(18f);
            return new Rectangle(pad, S(64f), Width - 2 * pad, S(104f));
        }
    }

    /// <summary>窗口无系统边框，但保留投影（CS_DROPSHADOW）。</summary>
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }

    public MainForm()
    {
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            _scale = g.DpiX / 96f;

        _fTitle = Ui.MakeFont(12.5f, FontStyle.Bold);
        _fH1 = Ui.MakeFont(11f, FontStyle.Bold);
        _fBody = Ui.MakeFont(9.5f, FontStyle.Regular);
        _fSmall = Ui.MakeFont(9f, FontStyle.Regular);
        _fPill = Ui.MakeFont(9f, FontStyle.Regular);
        _fBtn = Ui.MakeFont(10.5f, FontStyle.Bold);
        _fTiny = Ui.MakeFont(8f, FontStyle.Regular);

        Text = "合盖保活";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Ui.Bg;
        DoubleBuffered = true;
        KeyPreview = true;

        try
        {
            _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (_appIcon != null)
                Icon = _appIcon;
        }
        catch
        {
        }

        _btnPrimary = new FlatBtn
        {
            Font = _fBtn,
            Radius = S(10f),
        };
        _btnPrimary.Click += delegate { OnPrimaryClick(); };

        _pills = new PillBar
        {
            Font = _fPill,
            Items = DurationNames,
            Values = Durations,
            UiScale = _scale,
        };

        _btnMin = new CaptionBtn();
        _btnMin.Click += delegate { HideToTray(); };

        _btnClose = new CaptionBtn { IsClose = true };
        _btnClose.Click += delegate { HideToTray(); };

        Controls.Add(_btnPrimary);
        Controls.Add(_pills);
        Controls.Add(_btnMin);
        Controls.Add(_btnClose);

        BuildTray();
        ApplyLayout();
        UpdateTexts();
        RefreshActionText();

        try { SystemEvents.SessionEnding += OnSessionEnding; } catch { }

        _timer = new Timer { Interval = 500 };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>把设计稿坐标按 DPI 缩放换算成实际像素。</summary>
    private int S(float v) => (int)Math.Round(v * _scale);

    private void ApplyLayout()
    {
        ClientSize = new Size(S(DesignW), S(DesignH));

        int pad = S(20f);
        _btnMin.Bounds = new Rectangle(Width - S(72f), S(12f), S(28f), S(28f));
        _btnClose.Bounds = new Rectangle(Width - S(40f), S(12f), S(28f), S(28f));
        _pills.Bounds = new Rectangle(pad, S(206f), Width - 2 * pad, S(32f));
        _btnPrimary.Bounds = new Rectangle(pad, S(250f), Width - 2 * pad, S(44f));

        // 用圆角区域裁掉窗口四角
        using GraphicsPath path = Ui.RoundRect(new RectangleF(0f, 0f, Width, Height), S(12f));
        Region = new Region(path);
    }

    public void SetPendingKeep(int seconds) => _pendingKeepSeconds = seconds;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_pendingKeepSeconds >= 0)
        {
            int seconds = _pendingKeepSeconds;
            _pendingKeepSeconds = -1;
            StartKeep(seconds);
        }
    }

    // —————————————————————————————— 托盘

    private void BuildTray()
    {
        _tray = new NotifyIcon
        {
            Icon = _appIcon ?? SystemIcons.Application,
            Text = "合盖保活",
            Visible = true,
        };
        _tray.DoubleClick += delegate { ShowMain(); };

        _trayMenu = new ContextMenuStrip
        {
            Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()),
            ShowImageMargin = false,
            Font = _fSmall,
        };

        _miShow = new ToolStripMenuItem("显示主界面");
        _miShow.Click += delegate { ShowMain(); };

        _miStart = new ToolStripMenuItem("开启保活");
        _miStart.Click += delegate { StartKeep(Durations[_pills.Selected]); };

        _miStop = new ToolStripMenuItem("停止并恢复");
        _miStop.Click += delegate { StopKeep(notify: true); };

        _miExit = new ToolStripMenuItem("退出");
        _miExit.Click += delegate { ExitApp(); };

        _trayMenu.Items.Add(_miShow);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(_miStart);
        _trayMenu.Items.Add(_miStop);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(_miExit);

        foreach (ToolStripItem item in _trayMenu.Items)
            item.ForeColor = Ui.TextMain;

        _trayMenu.Opening += delegate
        {
            _miStart.Enabled = !_active;
            _miStop.Enabled = _active;
            _miStart.ForeColor = _active ? Ui.TextMuted : Ui.TextMain;
            _miStop.ForeColor = _active ? Ui.TextMain : Ui.TextMuted;
        };

        _tray.ContextMenuStrip = _trayMenu;
    }

    private void UpdateTray()
    {
        if (_tray == null) return;

        string text = _active ? "合盖保活 · 已开启" : "合盖保活 · 未开启";
        if (_active && _deadline != DateTime.MinValue)
        {
            TimeSpan left = _deadline - DateTime.Now;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            text += string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "（剩余 {0:00}:{1:00}）", (int)left.TotalHours, left.Minutes);
        }

        // NotifyIcon.Text 上限 63 个字符
        if (text.Length > 62)
            text = text.Substring(0, 62);

        _tray.Text = text;
    }

    private void HideToTray()
    {
        Hide();
        if (_balloonShown) return;

        _balloonShown = true;
        try
        {
            _tray.ShowBalloonTip(3000, "合盖保活",
                _active
                    ? "程序已最小化到托盘，保活继续运行；右键托盘图标可退出。"
                    : "程序已最小化到托盘；右键托盘图标可退出。",
                ToolTipIcon.Info);
        }
        catch
        {
        }
    }

    private void ShowMain()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private void ExitApp()
    {
        _exiting = true;
        try { StopKeep(notify: false); } catch { }
        try { _timer.Stop(); } catch { }
        try { SystemEvents.SessionEnding -= OnSessionEnding; } catch { }
        Close();
    }

    /// <summary>注销 / 关机时也要把合盖设置还回去。</summary>
    private void OnSessionEnding(object sender, SessionEndingEventArgs e)
    {
        try
        {
            if (_active)
                StopKeep(notify: false);
        }
        catch
        {
        }
    }

    // —————————————————————————————— 窗口事件

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // 用户点关闭只是收回托盘，退出要走托盘菜单
        if (!_exiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        if (_active)
        {
            try { StopKeep(notify: false); } catch { }
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        try
        {
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
        }
        catch
        {
        }
        base.OnFormClosed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            HideToTray();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _dragging = true;
            _dragStartScreen = Cursor.Position;
            _dragStartLoc = Location;
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging)
        {
            Point now = Cursor.Position;
            Location = new Point(
                _dragStartLoc.X + now.X - _dragStartScreen.X,
                _dragStartLoc.Y + now.Y - _dragStartScreen.Y);
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _dragging = false;
        base.OnMouseUp(e);
    }
}
