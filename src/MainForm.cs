using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AutoClicker
{
    public class MainForm : Form
    {
        private const int HotkeyIdStart = Hotkeys.IdStart;
        private const int HotkeyIdCapture = Hotkeys.IdCapture;
        private const int HotkeyIdStop = Hotkeys.IdStop;
        private const int HotkeyIdTest = Hotkeys.IdTest;
        private const int HotkeyIdRecord = Hotkeys.IdRecord;

        // 布局常量
        private const int PadX = 20;
        private const int ContentW = 660;

        private MacroSettings _settings = new MacroSettings();
        private CoordSpace _uiSpace = CoordSpace.Screen;
        private bool _dark = true;
        private bool _topMost;

        private IntPtr _boundHwnd = IntPtr.Zero;
        private string _boundTitle = string.Empty;
        private int _boundClientW;
        private int _boundClientH;
        private int _boundOriginX;
        private int _boundOriginY;

        private readonly Runner _runner = new Runner();
        private Recorder _recorder;
        private List<SavedSequence> _sequences = new List<SavedSequence>();
        private int _selectedSequence = -1;
        private ActionDialog _activeDialog;

        private int _bindCountdown;
        private int _recordCountdown;
        private bool _recordArmed;
        private int _lastLogCount = -1;
        private bool _inputOk = true;
        private string _inputCheckDetail = string.Empty;
        private int _hotRow = -1;

        // ---- 控件 ----
        private HeaderPanel _header;
        private Label _lblTitle;
        private Label _lblSubtitle;
        private ToggleSwitch _tglDark;
        private ToggleSwitch _tglTop;
        private Label _lblDark;
        private Label _lblTop;

        private ChoiceCard _cardScreen;
        private ChoiceCard _cardWindow;
        private RoundButton _btnBind;
        private Label _lblSpaceHint;

        private Segmented _tabBar;
        private Panel _pageHost;
        private Panel[] _pages;
        private int _tabIndex;

        private DataGridView _grid;
        private bool _rebuildingGrid;
        private Control _emptyHint;
        private Label _lblActionSummary;

        private NumericBox _numLoop;
        private NumericBox _numDelay;
        private NumericBox _numPosJitter;
        private NumericBox _numTimeJitter;
        private NumericBox _numHold;
        private NumericBox _numDoubleGap;
        private Segmented _segInputMethod;
        private ToggleSwitch _tglActivate;

        private DataGridView _gridHistory;
        private LogView _logPreview;

        private LogView _logDiag;
        private Label _lblDiagStatus;
        private ProbeForm _probe;

        private RoundButton _btnStart;
        private RoundButton _btnTest;
        private RoundButton _btnRecord;
        private StatusDot _dot;
        private Label _lblStatus;
        private Label _lblCount;
        private Label _lblDetail;
        private LogPanel _logPanel;
        private Label _lblLogTail;

        private Timer _uiTimer;
        private WheelRouter _wheelRouter;
        private Timer _bindTimer;
        private Timer _recordTimer;
        private ToolTip _tips;

        public MainForm()
        {
            Text = "鼠标连点器 v" + Program.ShortVersion;
            ClientSize = new Size(700, 820);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            DoubleBuffered = true;
            KeyPreview = true;

            _tips = new ToolTip();
            _tips.InitialDelay = 400;
            _tips.ReshowDelay = 150;

            LoadSettings();
            ApplyThemeStatic();
            BuildUi();
            RestoreWindowPosition();

            _runner.Finished += RunnerFinished;

            _wheelRouter = new WheelRouter();
            Application.AddMessageFilter(_wheelRouter);

            _uiTimer = new Timer();
            _uiTimer.Interval = 150;
            _uiTimer.Tick += UiTimerTick;
            _uiTimer.Start();

            _bindTimer = new Timer();
            _bindTimer.Interval = 1000;
            _bindTimer.Tick += BindTimerTick;

            _recordTimer = new Timer();
            _recordTimer.Interval = 1000;
            _recordTimer.Tick += RecordTimerTick;

            ApplyTheme();
            RefreshActions();
            RefreshHistory();
            UpdateUiState();

            AppLog.Write("程序启动，版本 " + Program.VersionText);
            AppLog.Write("管理员权限：" + (AppEnv.IsSelfElevated ? "是" : "否")
                + "；屏幕：" + Native.GetVirtualScreen().Width + "×" + Native.GetVirtualScreen().Height);
        }

        // ==================================================================
        // 构建界面
        // ==================================================================

        private void BuildUi()
        {
            BuildHeader();
            BuildLocationCard();
            BuildTabBar();
            BuildPages();
            BuildRunCard();

            Label footer = Ui.Label(
                "F6 开始/停止 · F7 抓坐标 · F8 急停 · F9 测试 · F10 录制",
                PadX,
                798,
                ContentW,
                16,
                Theme.Muted,
                Theme.TinyFont);
            Controls.Add(footer);

            Label footerRight = Ui.Label(
                "数据目录：" + (AppPaths.IsPortable ? "exe 同目录下的「数据」文件夹" : "%APPDATA%\\AutoClicker3"),
                PadX + 400,
                798,
                ContentW - 400,
                16,
                Theme.Muted,
                Theme.TinyFont);
            footerRight.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(footerRight);
        }

        private void BuildHeader()
        {
            _header = new HeaderPanel();
            _header.Location = new Point(0, 0);
            _header.Size = new Size(ClientSize.Width, 96);
            Controls.Add(_header);

            _lblTitle = Ui.Label("鼠标连点器", 26, 18, 320, 38, Theme.HeaderText, Theme.TitleFont);
            _header.Controls.Add(_lblTitle);

            _lblSubtitle = Ui.Label(
                "网页 / 桌面 · 动作序列 · v" + Program.ShortVersion,
                27,
                58,
                360,
                20,
                Theme.HeaderMuted,
                Theme.Subtitle);
            _header.Controls.Add(_lblSubtitle);

            _lblDark = Ui.Label("深色模式", 452, 40, 60, 20, Theme.HeaderMuted, Theme.SmallFont);
            _header.Controls.Add(_lblDark);

            _tglDark = new ToggleSwitch();
            _tglDark.Location = new Point(516, 38);
            _tglDark.Size = new Size(40, 22);
            _tglDark.Checked = _dark;
            _tglDark.CheckedChanged += delegate
            {
                _dark = _tglDark.Checked;
                ApplyTheme();
                AppLog.Write("切换主题：" + (_dark ? "深色" : "浅色"));
            };
            _header.Controls.Add(_tglDark);

            _lblTop = Ui.Label("窗口置顶", 570, 40, 60, 20, Theme.HeaderMuted, Theme.SmallFont);
            _header.Controls.Add(_lblTop);

            _tglTop = new ToggleSwitch();
            _tglTop.Location = new Point(634, 38);
            _tglTop.Size = new Size(40, 22);
            _tglTop.Checked = _topMost;
            _tglTop.CheckedChanged += delegate
            {
                _topMost = _tglTop.Checked;
                TopMost = _topMost;
            };
            _header.Controls.Add(_tglTop);
        }

        private void BuildLocationCard()
        {
            CardPanel card = new CardPanel();
            card.Location = new Point(PadX, 108);
            card.Size = new Size(ContentW, 128);
            Controls.Add(card);

            card.Controls.Add(Ui.Label("定位方式", 16, 10, 60, 16, Theme.Muted, Theme.TinyFont));

            _btnBind = MakeButton(card, "绑定窗口", "(3s)", 536, 3, 108, 30, ButtonVariant.Secondary, Glyph.Target, OnBindClick);

            _cardScreen = new ChoiceCard();
            _cardScreen.TitleText = "屏幕坐标";
            _cardScreen.DescText = "记录屏幕上的绝对位置";
            _cardScreen.Location = new Point(16, 34);
            _cardScreen.Size = new Size(304, 56);
            _cardScreen.Click += delegate { SetSpace(CoordSpace.Screen); };
            card.Controls.Add(_cardScreen);

            _cardWindow = new ChoiceCard();
            _cardWindow.TitleText = "窗口相对";
            _cardWindow.DescText = "记录目标窗口客户区里的相对位置";
            _cardWindow.Location = new Point(324, 34);
            _cardWindow.Size = new Size(320, 56);
            _cardWindow.Click += delegate { SetSpace(CoordSpace.WindowClient); };
            card.Controls.Add(_cardWindow);

            _lblSpaceHint = Ui.Label(string.Empty, 16, 94, 628, 30, Theme.Muted, Theme.TinyFont);
            card.Controls.Add(_lblSpaceHint);
            UpdateLocationStatus();
        }

        private void BuildTabBar()
        {
            _tabBar = new Segmented();
            _tabBar.SetItems("动作序列", "运行参数", "历史序列", "诊断");
            _tabBar.Location = new Point(PadX, 246);
            _tabBar.Size = new Size(ContentW, 36);
            _tabBar.Font = Theme.UiFont;
            _tabBar.SelectedIndex = 0;
            _tabBar.SelectedIndexChanged += delegate { SelectTab(_tabBar.SelectedIndex); };
            Controls.Add(_tabBar);
        }

        private void BuildPages()
        {
            _pageHost = new Panel();
            _pageHost.Location = new Point(PadX, 292);
            _pageHost.Size = new Size(ContentW, 308);
            _pageHost.BackColor = Theme.Background;
            Controls.Add(_pageHost);

            _pages = new Panel[4];
            for (int i = 0; i < _pages.Length; i++)
            {
                Panel p = new Panel();
                p.Location = new Point(0, 0);
                p.Size = _pageHost.Size;
                p.BackColor = Theme.Background;
                p.Visible = (i == 0);
                _pageHost.Controls.Add(p);
                _pages[i] = p;
            }

            BuildActionsPage(_pages[0]);
            BuildRunParamsPage(_pages[1]);
            BuildHistoryPage(_pages[2]);
            BuildDiagnosticsPage(_pages[3]);
        }

        private void BuildActionsPage(Panel page)
        {
            CardPanel card = new CardPanel();
            card.Location = new Point(0, 0);
            card.Size = new Size(ContentW, 216);
            page.Controls.Add(card);

            _grid = new DataGridView();
            _grid.Location = new Point(10, 10);
            _grid.Size = new Size(ContentW - 20, 196);
            GridStyle.Apply(_grid);
            BuildGridColumns();
            _grid.CellPainting += GridCellPainting;
            _grid.CellMouseClick += GridCellMouseClick;
            _grid.CellDoubleClick += GridCellDoubleClick;
            _grid.CellMouseEnter += GridCellMouseEnter;
            _grid.CellMouseLeave += GridCellMouseLeave;
            _grid.RowPostPaint += GridRowPostPaint;
            _grid.MouseDown += GridMouseDown;
            _grid.KeyDown += GridKeyDown;
            _grid.SelectionChanged += delegate { UpdateActionSummary(); };
            card.Controls.Add(_grid);

            _emptyHint = new EmptyHintPanel();
            _emptyHint.Location = new Point(10, 10);
            _emptyHint.Size = _grid.Size;
            _emptyHint.BackColor = Theme.CardAlt;
            _emptyHint.Visible = false;
            card.Controls.Add(_emptyHint);
            _emptyHint.BringToFront();

            RoundButton btnAdd = MakeButton(page, "添加", string.Empty, 0, 228, 110, 34, ButtonVariant.Primary, Glyph.Plus, delegate { ShowAddMenu(btnAddRef); });
            btnAddRef = btnAdd;
            MakeButton(page, "编辑", string.Empty, 118, 228, 68, 34, ButtonVariant.Secondary, Glyph.None, delegate { EditSelected(); });
            MakeButton(page, "复制", string.Empty, 194, 228, 68, 34, ButtonVariant.Secondary, Glyph.None, delegate { DuplicateSelected(); });
            MakeButton(page, "删除", string.Empty, 270, 228, 68, 34, ButtonVariant.Secondary, Glyph.None, delegate { DeleteSelected(); });

            RoundButton up = MakeButton(page, string.Empty, string.Empty, 352, 228, 38, 34, ButtonVariant.Secondary, Glyph.ArrowUp, delegate { MoveSelected(-1); });
            _tips.SetToolTip(up, "上移 (Alt+↑)");
            RoundButton down = MakeButton(page, string.Empty, string.Empty, 398, 228, 38, 34, ButtonVariant.Secondary, Glyph.ArrowDown, delegate { MoveSelected(1); });
            _tips.SetToolTip(down, "下移 (Alt+↓)");

            _lblActionSummary = Ui.Label(string.Empty, 448, 228, 212, 34, Theme.Muted, Theme.SmallFont);
            _lblActionSummary.TextAlign = ContentAlignment.MiddleRight;
            page.Controls.Add(_lblActionSummary);

            page.Controls.Add(Ui.Label(
                "双击动作编辑 · 点左侧圆点启用/停用 · Delete 删除 · F7 用鼠标当前位置更新坐标",
                0,
                272,
                ContentW,
                18,
                Theme.Muted,
                Theme.TinyFont));
        }

        private RoundButton btnAddRef;

        private void BuildGridColumns()
        {
            _grid.Columns.Clear();

            DataGridViewTextBoxColumn colNo = new DataGridViewTextBoxColumn();
            colNo.HeaderText = "#";
            colNo.Width = 44;
            colNo.ReadOnly = true;
            colNo.SortMode = DataGridViewColumnSortMode.NotSortable;
            colNo.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colNo.DefaultCellStyle.ForeColor = Theme.Muted;
            colNo.DefaultCellStyle.Font = Theme.TinyFont;

            DataGridViewTextBoxColumn colEnabled = new DataGridViewTextBoxColumn();
            colEnabled.HeaderText = string.Empty;
            colEnabled.Width = 56;
            colEnabled.ReadOnly = true;
            colEnabled.SortMode = DataGridViewColumnSortMode.NotSortable;
            colEnabled.DefaultCellStyle.Padding = new Padding(0);

            DataGridViewTextBoxColumn colText = new DataGridViewTextBoxColumn();
            colText.HeaderText = "动作";
            colText.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colText.ReadOnly = true;
            colText.SortMode = DataGridViewColumnSortMode.NotSortable;

            DataGridViewTextBoxColumn colWait = new DataGridViewTextBoxColumn();
            colWait.HeaderText = "等待 ms";
            colWait.Width = 86;
            colWait.ReadOnly = true;
            colWait.SortMode = DataGridViewColumnSortMode.NotSortable;
            colWait.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colWait.DefaultCellStyle.Font = Theme.MonoFont;
            colWait.DefaultCellStyle.Padding = new Padding(0, 0, 4, 0);
            colWait.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;

            DataGridViewTextBoxColumn colPreview = new DataGridViewTextBoxColumn();
            colPreview.HeaderText = "预览";
            colPreview.Width = 132;
            colPreview.ReadOnly = true;
            colPreview.SortMode = DataGridViewColumnSortMode.NotSortable;
            colPreview.DefaultCellStyle.ForeColor = Theme.Muted;
            colPreview.DefaultCellStyle.Font = Theme.TinyFont;

            _grid.Columns.Add(colNo);
            _grid.Columns.Add(colEnabled);
            _grid.Columns.Add(colText);
            _grid.Columns.Add(colWait);
            _grid.Columns.Add(colPreview);
        }

        private void BuildRunParamsPage(Panel page)
        {
            CardPanel card = new CardPanel();
            card.Location = new Point(0, 0);
            card.Size = new Size(ContentW, 250);
            page.Controls.Add(card);

            int col1 = 20;
            int col2 = 232;
            int col3 = 444;

            AddField(card, "循环次数", col1, 16, 150, 0, 1000000, 1, _settings.LoopCount, out _numLoop);
            _numLoop.TextChanged += delegate { UpdateActionSummary(); };
            card.Controls.Add(Ui.Label("0 = 无限", col1 + 158, 40, 60, 30, Theme.Muted, Theme.TinyFont));

            AddField(card, "启动延迟", col2, 16, 120, 0, 3600000, 100, _settings.StartDelayMs, out _numDelay);
            card.Controls.Add(Ui.Label("ms", col2 + 126, 40, 30, 30, Theme.Muted, Theme.TinyFont));

            AddField(card, "位置抖动", col3, 16, 120, 0, 500, 1, _settings.PositionJitter, out _numPosJitter);
            card.Controls.Add(Ui.Label("px", col3 + 126, 40, 30, 30, Theme.Muted, Theme.TinyFont));

            AddField(card, "时间抖动", col1, 88, 120, 0, 5000, 5, _settings.TimeJitter, out _numTimeJitter);
            card.Controls.Add(Ui.Label("ms", col1 + 126, 112, 30, 30, Theme.Muted, Theme.TinyFont));

            AddField(card, "点击按住", col2, 88, 120, 1, 2000, 5, _settings.ClickHoldMs, out _numHold);
            card.Controls.Add(Ui.Label("ms", col2 + 126, 112, 30, 30, Theme.Muted, Theme.TinyFont));

            AddField(card, "双击间隔", col3, 88, 120, 30, 1000, 10, _settings.DoubleClickGapMs, out _numDoubleGap);
            card.Controls.Add(Ui.Label("ms", col3 + 126, 112, 30, 30, Theme.Muted, Theme.TinyFont));

            card.Controls.Add(Ui.Label("点击方式", col1, 154, 120, 16, Theme.Muted, Theme.TinyFont));
            _segInputMethod = new Segmented();
            _segInputMethod.SetItems("SendInput", "mouse_event");
            _segInputMethod.Location = new Point(col1, 174);
            _segInputMethod.Size = new Size(280, 32);
            _segInputMethod.SelectedIndex = _settings.InputMethod == MouseInputMethod.MouseEvent ? 1 : 0;
            _segInputMethod.SelectedIndexChanged += delegate { UpdateInputMethodHint(); };
            card.Controls.Add(_segInputMethod);

            card.Controls.Add(Ui.Label("点击前先激活目标窗口", 396, 182, 160, 20, Theme.TextDim, Theme.SmallFont));
            _tglActivate = new ToggleSwitch();
            _tglActivate.Location = new Point(col3 + 118, 180);
            _tglActivate.Size = new Size(40, 22);
            _tglActivate.Checked = _settings.ActivateBeforeClick;
            card.Controls.Add(_tglActivate);

            _lblInputMethodHint = Ui.Label(string.Empty, col1, 212, ContentW - 40, 34, Theme.Muted, Theme.TinyFont);
            card.Controls.Add(_lblInputMethodHint);
            UpdateInputMethodHint();

            page.Controls.Add(Ui.Label(
                "· 循环次数填 0 表示一直重复，直到按 F8 停。\n"
                + "· 抖动是给每个动作加随机偏移，0 = 不抖；「点击按住」40ms 比较稳，按不住的程序可以调大。",
                0,
                262,
                ContentW,
                44,
                Theme.Muted,
                Theme.TinyFont));
        }

        private Label _lblInputMethodHint;

        /// <summary>两种点击方式的区别，写在选择器下面，省得猜。</summary>
        private void UpdateInputMethodHint()
        {
            if (_lblInputMethodHint == null || _segInputMethod == null)
            {
                return;
            }
            if (_segInputMethod.SelectedIndex == 1)
            {
                _lblInputMethodHint.Text =
                    "mouse_event：Windows 老接口。兼容性最广，个别老游戏/老程序只认它；"
                    + "\n用 SendInput 点了没反应时，换成这个再试一次。";
            }
            else
            {
                _lblInputMethodHint.Text =
                    "SendInput：Windows 现在的标准输入接口，绝大多数程序都认，优先用它；"
                    + "\n如果目标毫无反应，再换 mouse_event 试试。";
            }
            _lblInputMethodHint.ForeColor = Theme.Muted;
        }

        private void AddField(Control parent, string label, int x, int y, int w, int min, int max, int step, int value, out NumericBox box)
        {
            parent.Controls.Add(Ui.Label(label, x, y, 160, 16, Theme.Muted, Theme.TinyFont));
            NumericBox nb = new NumericBox();
            nb.Min = min;
            nb.Max = max;
            nb.Step = step;
            nb.Value = value;
            Ui.Host(parent, nb, x, y + 20, w, 32);
            box = nb;
        }

        private void BuildHistoryPage(Panel page)
        {
            CardPanel card = new CardPanel();
            card.Location = new Point(0, 0);
            card.Size = new Size(ContentW, 216);
            page.Controls.Add(card);

            _gridHistory = new DataGridView();
            _gridHistory.Location = new Point(10, 10);
            _gridHistory.Size = new Size(300, 196);
            GridStyle.Apply(_gridHistory);
            _gridHistory.Columns.Clear();

            DataGridViewTextBoxColumn c1 = new DataGridViewTextBoxColumn();
            c1.HeaderText = "名称";
            c1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            c1.ReadOnly = true;
            c1.SortMode = DataGridViewColumnSortMode.NotSortable;

            DataGridViewTextBoxColumn c2 = new DataGridViewTextBoxColumn();
            c2.HeaderText = "动作";
            c2.Width = 50;
            c2.ReadOnly = true;
            c2.SortMode = DataGridViewColumnSortMode.NotSortable;
            c2.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            c2.DefaultCellStyle.Font = Theme.MonoFont;

            DataGridViewTextBoxColumn c3 = new DataGridViewTextBoxColumn();
            c3.HeaderText = "保存时间";
            c3.Width = 84;
            c3.ReadOnly = true;
            c3.SortMode = DataGridViewColumnSortMode.NotSortable;
            c3.DefaultCellStyle.ForeColor = Theme.Muted;
            c3.DefaultCellStyle.Font = Theme.TinyFont;

            _gridHistory.Columns.Add(c1);
            _gridHistory.Columns.Add(c2);
            _gridHistory.Columns.Add(c3);
            _gridHistory.SelectionChanged += delegate { UpdateHistoryPreview(); };
            _gridHistory.CellMouseDoubleClick += delegate { LoadSelectedSequence(); };
            card.Controls.Add(_gridHistory);

            _logPreview = new LogView();
            _logPreview.Location = new Point(318, 10);
            _logPreview.Size = new Size(332, 196);
            _logPreview.ColorizeErrors = false;
            _logPreview.TextColor = Theme.Text;
            _logPreview.BackColor = Theme.Card;
            card.Controls.Add(_logPreview);

            MakeButton(page, "保存当前", string.Empty, 0, 224, 82, 34, ButtonVariant.Primary, Glyph.None, delegate { SaveCurrentSequence(); });
            MakeButton(page, "载入", string.Empty, 90, 224, 64, 34, ButtonVariant.Secondary, Glyph.None, delegate { LoadSelectedSequence(); });
            MakeButton(page, "测试", string.Empty, 162, 224, 64, 34, ButtonVariant.Secondary, Glyph.None, delegate { RunSelectedSequence(true); });
            MakeButton(page, "执行", string.Empty, 234, 224, 64, 34, ButtonVariant.Secondary, Glyph.None, delegate { RunSelectedSequence(false); });
            MakeButton(page, "删除", string.Empty, 306, 224, 64, 34, ButtonVariant.Secondary, Glyph.None, delegate { DeleteSelectedSequence(); });
            MakeButton(page, "重命名", string.Empty, 378, 224, 74, 34, ButtonVariant.Secondary, Glyph.None, delegate { RenameSelectedSequence(); });

            page.Controls.Add(Ui.Label(
                "历史序列会原样保存每个动作的坐标类型；双击一条可以直接载入。",
                0,
                272,
                ContentW,
                18,
                Theme.Muted,
                Theme.TinyFont));
        }

        private void BuildDiagnosticsPage(Panel page)
        {
            CardPanel card = new CardPanel();
            card.Location = new Point(0, 0);
            card.Size = new Size(ContentW, 196);
            page.Controls.Add(card);

            _logDiag = new LogView();
            _logDiag.Location = new Point(10, 10);
            _logDiag.Size = new Size(ContentW - 20, 166);
            _logDiag.ColorizeErrors = true;
            _logDiag.TextColor = Theme.TextDim;
            _logDiag.BackColor = Theme.Card;
            card.Controls.Add(_logDiag);

            MakeButton(page, "输入探针", string.Empty, 0, 196, 96, 34, ButtonVariant.Secondary, Glyph.None, delegate { OpenProbe(); });
            MakeButton(page, "探针自测", string.Empty, 104, 196, 96, 34, ButtonVariant.Secondary, Glyph.None, delegate { ProbeRoundTrip(); });
            MakeButton(page, "权限检查", string.Empty, 208, 196, 88, 34, ButtonVariant.Secondary, Glyph.None, delegate { CheckPermissions(); });
            MakeButton(page, "打开日志", string.Empty, 304, 196, 88, 34, ButtonVariant.Secondary, Glyph.None, delegate { OpenLogFile(); });
            MakeButton(page, "清空", string.Empty, 400, 196, 56, 34, ButtonVariant.Ghost, Glyph.None, delegate { ClearDiagLog(); });
            MakeButton(page, "以管理员重启", string.Empty, 466, 196, 124, 34, ButtonVariant.Secondary, Glyph.None, delegate { RestartAsAdmin(); });

            _lblDiagStatus = Ui.Label(string.Empty, 0, 238, ContentW, 66, Theme.Muted, Theme.TinyFont);
            page.Controls.Add(_lblDiagStatus);

            UpdateDiagStatus();
        }

        private void BuildRunCard()
        {
            CardPanel card = new CardPanel();
            card.Location = new Point(PadX, 610);
            card.Size = new Size(ContentW, 190);
            Controls.Add(card);

            _btnStart = MakeButton(card, "开始执行", "(F6)", 16, 16, 168, 56, ButtonVariant.Primary, Glyph.Play, delegate { ToggleStart(); });
            _btnTest = MakeButton(card, "测试", "(F9)", 192, 16, 104, 56, ButtonVariant.Secondary, Glyph.Target, delegate { ToggleTest(); });
            _btnRecord = MakeButton(card, "录制", "(F10)", 304, 16, 132, 56, ButtonVariant.Secondary, Glyph.Record, delegate { ToggleRecord(); });
            _tips.SetToolTip(_btnStart, "开始 / 停止执行（F6）");
            _tips.SetToolTip(_btnTest, "测试一次：1 轮、无启动延迟、不抖动（F9）");
            _tips.SetToolTip(_btnRecord, "录制鼠标点击和键盘按键（F10）");

            _dot = new StatusDot();
            _dot.Location = new Point(452, 24);
            _dot.Size = new Size(12, 12);
            _dot.DotColor = Theme.Muted;
            card.Controls.Add(_dot);

            _lblStatus = Ui.Label("就绪", 470, 16, 176, 26, Theme.Text, new Font("Microsoft YaHei UI", 11F, FontStyle.Bold));
            card.Controls.Add(_lblStatus);

            _lblCount = Ui.Label("共 0 个动作", 470, 42, 176, 18, Theme.Muted, Theme.TinyFont);
            card.Controls.Add(_lblCount);

            _lblDetail = Ui.Label("F8 紧急停止", 470, 60, 176, 18, Theme.Muted, Theme.TinyFont);
            card.Controls.Add(_lblDetail);

            card.Controls.Add(Ui.Label("操作记录", 16, 78, 80, 16, Theme.TextDim, Theme.TinyFont));
            _lblLogTail = Ui.Label("最近 3 条", 544, 78, 100, 16, Theme.Muted, Theme.TinyFont);
            _lblLogTail.TextAlign = ContentAlignment.MiddleRight;
            card.Controls.Add(_lblLogTail);

            _logPanel = new LogPanel();
            _logPanel.Location = new Point(16, 96);
            _logPanel.Size = new Size(628, 72);
            card.Controls.Add(_logPanel);
        }

        // ------------------------------------------------------------------
        // 小工具
        // ------------------------------------------------------------------

        private RoundButton MakeButton(Control parent, string text, string suffix, int x, int y, int w, int h, ButtonVariant variant, Glyph icon, EventHandler click)
        {
            RoundButton b = new RoundButton();
            b.Text = text;
            b.Suffix = suffix;
            b.Icon = icon;
            b.Variant = variant;
            b.Location = new Point(x, y);
            b.Size = new Size(w, h);
            b.BackColor = parent is CardPanel ? Theme.Card : Theme.Background;
            if (click != null)
            {
                b.Click += click;
            }
            parent.Controls.Add(b);
            return b;
        }

        /// <summary>空列表时的提示层。</summary>
        private class EmptyHintPanel : Control
        {
            public EmptyHintPanel()
            {
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw,
                    true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.Clear(Theme.CardAlt);
                Rectangle r = new Rectangle(0, Height / 2 - 34, Width, 24);
                TextRenderer.DrawText(
                    g,
                    "还没有动作",
                    new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
                    r,
                    Theme.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(
                    g,
                    "点左上角「添加」，或者按 F10 录一段你自己的操作",
                    Theme.SmallFont,
                    new Rectangle(0, Height / 2 + 2, Width, 20),
                    Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        // ==================================================================
        // 主题
        // ==================================================================

        private void ApplyTheme()
        {
            Theme.Apply(_dark);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;

            ApplyThemeTree(this, Theme.Background);

            _header.Invalidate();
            _lblTitle.ForeColor = Theme.HeaderText;
            _lblSubtitle.ForeColor = Theme.HeaderMuted;
            _lblDark.ForeColor = Theme.HeaderMuted;
            _lblTop.ForeColor = Theme.HeaderMuted;
            _tglDark.Invalidate();
            _tglTop.Invalidate();

            if (_grid != null)
            {
                GridStyle.Apply(_grid);
                _grid.Invalidate();
            }
            if (_gridHistory != null)
            {
                GridStyle.Apply(_gridHistory);
                _gridHistory.Invalidate();
            }
            if (_logPreview != null)
            {
                _logPreview.BackColor = Theme.Card;
                _logPreview.TextColor = Theme.Text;
                _logPreview.Invalidate();
            }
            if (_logDiag != null)
            {
                _logDiag.BackColor = Theme.Card;
                _logDiag.TextColor = Theme.TextDim;
                _logDiag.Invalidate();
            }
            Invalidate(true);
            Theme.ApplyTitleBar(this, _dark);
        }

        private void ApplyThemeTree(Control parent, Color back)
        {
            if (parent == null)
            {
                return;
            }
            foreach (Control c in parent.Controls)
            {
                if (c == _header)
                {
                    continue;
                }

                Color childBack = (c is CardPanel) ? Theme.Card : back;

                if (c is InputFrame)
                {
                    InputFrame f = (InputFrame)c;
                    f.BaseColor = back;
                    f.Invalidate();
                }
                else if (c is RoundButton)
                {
                    c.BackColor = childBack;
                    c.Invalidate();
                }
                else if (c is Panel)
                {
                    c.BackColor = childBack;
                    c.Invalidate();
                }
                else if (c is DataGridView)
                {
                    c.BackColor = Theme.CardAlt;
                    c.Invalidate();
                }
                else if (c is Label)
                {
                    Label l = (Label)c;
                    if (l.Font == Theme.BoldFont || l.Font.Size >= 10f)
                    {
                        l.ForeColor = Theme.Text;
                    }
                    else if (l.ForeColor != Theme.Accent && l.ForeColor != Theme.Danger
                        && l.ForeColor != Theme.Success && l.ForeColor != Theme.Warn)
                    {
                        l.ForeColor = Theme.Muted;
                    }
                }
                else if (c is TextBox)
                {
                    c.BackColor = Theme.InputBack;
                    c.ForeColor = Theme.Text;
                }
                else if (c is LogView)
                {
                    c.BackColor = childBack;
                    c.Invalidate();
                }
                else if (c is LogPanel)
                {
                    c.BackColor = Theme.Card;
                    c.Invalidate();
                }
                else if (c is ToggleSwitch || c is StatusDot || c is Segmented)
                {
                    c.Invalidate();
                }

                ApplyThemeTree(c, childBack);
            }
        }

        // ==================================================================
        // 标签页
        // ==================================================================

        private void SelectTab(int index)
        {
            _tabIndex = index;
            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i].Visible = (i == index);
            }
            if (index == 2)
            {
                RefreshHistory();
            }
            if (index == 3)
            {
                UpdateDiagLog();
            }
        }

        // ==================================================================
        // 动作列表
        // ==================================================================

        private void RefreshActions()
        {
            if (_grid == null)
            {
                return;
            }
            int selected = _grid.CurrentRow != null ? _grid.CurrentRow.Index : -1;
            _rebuildingGrid = true;
            _grid.Rows.Clear();
            for (int i = 0; i < _settings.Actions.Count; i++)
            {
                MacroAction a = _settings.Actions[i];
                int idx = _grid.Rows.Add(
                    (i + 1).ToString("00", CultureInfo.InvariantCulture),
                    string.Empty,
                    a.DescribeForList(),
                    a.WaitMs.ToString(CultureInfo.InvariantCulture),
                    Fmt.Wait(a.WaitMs));
                _grid.Rows[idx].Tag = a;
            }
            _rebuildingGrid = false;

            if (_grid.Rows.Count > 0)
            {
                int target = selected;
                if (target < 0 || target >= _grid.Rows.Count)
                {
                    target = _grid.Rows.Count - 1;
                }
                _grid.CurrentCell = _grid.Rows[target].Cells[2];
            }

            bool empty = _settings.Actions.Count == 0;
            if (_emptyHint != null)
            {
                _emptyHint.Visible = empty;
                _emptyHint.Invalidate();
            }
            if (_grid != null)
            {
                _grid.Invalidate();
            }
            UpdateActionSummary();
            UpdateUiState();
        }

        private void UpdateActionSummary()
        {
            if (_lblActionSummary == null)
            {
                return;
            }
            int enabled = 0;
            int total = 0;
            for (int i = 0; i < _settings.Actions.Count; i++)
            {
                total++;
                if (_settings.Actions[i].Enabled)
                {
                    enabled++;
                }
            }
            int round = EstimateRoundMs();
            string text = enabled + "/" + total + " 个启用";
            if (round > 0)
            {
                text += " · 整轮 ≈ " + (round / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + " 秒";
            }
            _lblActionSummary.Text = text;
        }

        /// <summary>粗略估算跑完一轮要多久（等待 + 每个动作的固定开销）。</summary>
        private int EstimateRoundMs()
        {
            int hold = _numHold != null ? _numHold.Value : _settings.ClickHoldMs;
            int total = 0;
            for (int i = 0; i < _settings.Actions.Count; i++)
            {
                MacroAction a = _settings.Actions[i];
                if (!a.Enabled)
                {
                    continue;
                }
                if (a.Kind == ActionKind.Click)
                {
                    total += 40 + a.WaitMs;
                    if (a.DoubleClick)
                    {
                        total += 60 + hold;
                    }
                }
                else if (a.Kind == ActionKind.Wheel)
                {
                    total += 45 + a.WaitMs;
                }
                else if (a.Kind == ActionKind.Key)
                {
                    total += 30 + a.WaitMs;
                }
                else
                {
                    total += a.WaitMs;
                }
            }
            return total;
        }

        private MacroAction SelectedAction()
        {
            if (_grid == null || _grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as MacroAction;
        }

        private int IndexOfAction(MacroAction a)
        {
            return _settings.Actions.IndexOf(a);
        }

        private void GridCellHighlight(DataGridViewCellPaintingEventArgs e)
        {
            MacroAction a = _grid.Rows[e.RowIndex].Tag as MacroAction;
            if (a != null && !a.Enabled)
            {
                e.CellStyle.ForeColor = Theme.Mix(Theme.Muted, Theme.CardAlt, 0.25);
                e.CellStyle.SelectionForeColor = Theme.Mix(Theme.Muted, Theme.Selection, 0.25);
            }
        }

        private void GridCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (e.ColumnIndex == 1)
            {
                e.PaintBackground(e.ClipBounds, true);
                MacroAction a = _grid.Rows[e.RowIndex].Tag as MacroAction;
                bool on = a != null && a.Enabled;
                GridStyle.DrawCheckbox(e.Graphics, e.CellBounds, on, _hotRow == e.RowIndex);
                if (_hotRow == e.RowIndex)
                {
                    using (Pen p = new Pen(Theme.Alpha(Theme.Accent, 70)))
                    {
                        Rectangle r = new Rectangle(e.CellBounds.X, e.CellBounds.Y + e.CellBounds.Height - 1, e.CellBounds.Width, 1);
                        e.Graphics.DrawLine(p, r.Left, r.Top, r.Right, r.Top);
                    }
                }
                e.Handled = true;
                return;
            }

            GridCellHighlight(e);
        }

        private void GridRowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            // 选中行左侧的强调条
            if (_grid.Rows[e.RowIndex].Selected && _grid.CurrentRow != null && _grid.CurrentRow.Index == e.RowIndex)
            {
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                {
                    e.Graphics.FillRectangle(b, new Rectangle(e.RowBounds.Left, e.RowBounds.Top, 3, e.RowBounds.Height - 1));
                }
            }
        }

        private void GridCellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            if (_hotRow != e.RowIndex)
            {
                int old = _hotRow;
                _hotRow = e.RowIndex;
                if (old >= 0 && old < _grid.Rows.Count)
                {
                    _grid.InvalidateRow(old);
                }
                _grid.InvalidateRow(e.RowIndex);
            }
        }

        private void GridCellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            if (_hotRow >= 0 && _hotRow < _grid.Rows.Count)
            {
                _grid.InvalidateRow(_hotRow);
            }
            _hotRow = -1;
        }

        private void GridCellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 1 || e.Button != MouseButtons.Left)
            {
                return;
            }
            MacroAction a = _grid.Rows[e.RowIndex].Tag as MacroAction;
            if (a == null)
            {
                return;
            }
            a.Enabled = !a.Enabled;
            _grid.InvalidateRow(e.RowIndex);
            UpdateActionSummary();
            UpdateLocationStatus();
            AppLog.Write((a.Enabled ? "启用动作：" : "禁用动作：") + a.DescribeForList());
        }

        private void GridCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex == 1)
            {
                return;
            }
            EditRow(e.RowIndex);
        }

        private void GridMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }
            DataGridView.HitTestInfo hit = _grid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0)
            {
                _grid.CurrentCell = _grid.Rows[hit.RowIndex].Cells[2];
                ShowRowMenu(hit.RowIndex, e.Location);
            }
        }

        private void GridKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                EditSelected();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.D)
            {
                DuplicateSelected();
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Up)
            {
                MoveSelected(-1);
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Down)
            {
                MoveSelected(1);
                e.Handled = true;
            }
        }

        private void ShowRowMenu(int rowIndex, Point at)
        {
            MacroAction a = _grid.Rows[rowIndex].Tag as MacroAction;
            if (a == null)
            {
                return;
            }
            MenuPopup menu = new MenuPopup();
            menu.Add("编辑", "Enter", Glyph.Edit, delegate { EditRow(rowIndex); });
            menu.Add("复制一份", "Ctrl+D", Glyph.Copy, delegate { DuplicateSelected(); });
            menu.Add(a.Enabled ? "临时停用" : "启用", string.Empty, a.Enabled ? Glyph.Stop : Glyph.Play, delegate
            {
                a.Enabled = !a.Enabled;
                _grid.InvalidateRow(rowIndex);
                UpdateActionSummary();
                UpdateLocationStatus();
            });
            menu.AddSeparator();
            menu.Add("上移", "Alt+↑", Glyph.ArrowUp, delegate { MoveSelected(-1); });
            menu.Add("下移", "Alt+↓", Glyph.ArrowDown, delegate { MoveSelected(1); });
            menu.AddSeparator();
            menu.Add("删除", "Delete", Glyph.Trash, delegate { DeleteSelected(); });
            Point sp = _grid.PointToScreen(at);
            menu.ShowAtPoint(sp, this, sp);
        }

        private void ShowAddMenu(Control anchor)
        {
            MenuPopup menu = new MenuPopup();
            menu.Add("鼠标点击", string.Empty, Glyph.Target, delegate { AddAction(ActionKind.Click); });
            menu.Add("鼠标滚轮", string.Empty, Glyph.ArrowUp, delegate { AddAction(ActionKind.Wheel); });
            menu.Add("键盘按键", string.Empty, Glyph.Keyboard, delegate { AddAction(ActionKind.Key); });
            menu.Add("等待", string.Empty, Glyph.Pause, delegate { AddAction(ActionKind.Wait); });
            menu.AddSeparator();
            menu.Add("录一段操作", "F10", Glyph.Record, delegate { ToggleRecord(); });
            menu.ShowAt(anchor);
        }

        private void AddAction(ActionKind kind)
        {
            MacroAction a = new MacroAction();
            a.Kind = kind;
            a.WaitMs = 100;

            if (kind == ActionKind.Click)
            {
                Native.POINT p;
                Native.GetCursorPos(out p);
                a.X = p.X;
                a.Y = p.Y;
                a.Space = CoordSpace.Screen;
            }
            else if (kind == ActionKind.Wheel)
            {
                Native.POINT p;
                Native.GetCursorPos(out p);
                a.X = p.X;
                a.Y = p.Y;
                a.Space = CoordSpace.Screen;
                a.WheelDelta = -3;
                a.WaitMs = 120;
            }
            else if (kind == ActionKind.Key)
            {
                a.KeyCode = Keys.Space;
            }
            else
            {
                a.WaitMs = 500;
            }

            ActionDialog dlg = new ActionDialog(a, true, _uiSpace, _boundHwnd, _boundTitle);
            _activeDialog = dlg;
            DialogResult r;
            try
            {
                r = dlg.ShowDialog(this);
            }
            finally
            {
                _activeDialog = null;
            }
            if (r != DialogResult.OK)
            {
                return;
            }

            MacroAction result = dlg.GetResult();
            int insertAt = _settings.Actions.Count;
            MacroAction sel = SelectedAction();
            if (sel != null)
            {
                int idx = IndexOfAction(sel);
                if (idx >= 0)
                {
                    insertAt = idx + 1;
                }
            }
            _settings.Actions.Insert(insertAt, result);
            RefreshActions();
            if (insertAt < _grid.Rows.Count)
            {
                _grid.CurrentCell = _grid.Rows[insertAt].Cells[2];
            }
            UpdateLocationStatus();
            AppLog.Write("添加动作：" + result.DescribeForList());
        }

        private void EditSelected()
        {
            if (_grid.CurrentRow == null)
            {
                return;
            }
            EditRow(_grid.CurrentRow.Index);
        }

        private void EditRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            {
                return;
            }
            MacroAction a = _grid.Rows[rowIndex].Tag as MacroAction;
            if (a == null)
            {
                return;
            }

            ActionDialog dlg = new ActionDialog(a, false, _uiSpace, _boundHwnd, _boundTitle);
            _activeDialog = dlg;
            DialogResult r;
            try
            {
                r = dlg.ShowDialog(this);
            }
            finally
            {
                _activeDialog = null;
            }
            if (r != DialogResult.OK)
            {
                return;
            }

            MacroAction updated = dlg.GetResult();
            int idx = IndexOfAction(a);
            if (idx >= 0)
            {
                _settings.Actions[idx] = updated;
            }
            RefreshActions();
            if (rowIndex < _grid.Rows.Count)
            {
                _grid.CurrentCell = _grid.Rows[rowIndex].Cells[2];
            }
            UpdateLocationStatus();
            AppLog.Write("修改动作：" + updated.DescribeForList());
        }

        private void DuplicateSelected()
        {
            MacroAction a = SelectedAction();
            if (a == null)
            {
                return;
            }
            int idx = IndexOfAction(a);
            MacroAction copy = a.Clone();
            _settings.Actions.Insert(idx + 1, copy);
            RefreshActions();
            if (idx + 1 < _grid.Rows.Count)
            {
                _grid.CurrentCell = _grid.Rows[idx + 1].Cells[2];
            }
            AppLog.Write("复制动作：" + copy.DescribeForList());
        }

        private void DeleteSelected()
        {
            MacroAction a = SelectedAction();
            if (a == null)
            {
                return;
            }
            int idx = IndexOfAction(a);
            if (idx >= 0)
            {
                _settings.Actions.RemoveAt(idx);
                RefreshActions();
                UpdateLocationStatus();
                AppLog.Write("删除动作");
            }
        }

        private void MoveSelected(int delta)
        {
            MacroAction a = SelectedAction();
            if (a == null)
            {
                return;
            }
            int idx = IndexOfAction(a);
            int target = idx + delta;
            if (idx < 0 || target < 0 || target >= _settings.Actions.Count)
            {
                return;
            }
            _settings.Actions.RemoveAt(idx);
            _settings.Actions.Insert(target, a);
            RefreshActions();
            if (target < _grid.Rows.Count)
            {
                _grid.CurrentCell = _grid.Rows[target].Cells[2];
                _grid.Rows[target].Selected = true;
            }
        }

        /// <summary>F7：用当前鼠标位置更新选中的点击动作（尊重动作自己的坐标类型）。</summary>
        private void CaptureIntoSelected()
        {
            MacroAction a = SelectedAction();
            if (a == null)
            {
                AppLog.WriteError("F7：没有选中动作");
                return;
            }
            if (a.Kind != ActionKind.Click && a.Kind != ActionKind.Wheel)
            {
                AppLog.WriteError("F7：这个动作没有坐标（只有点击和滚轮有）");
                return;
            }

            Native.POINT p;
            if (!Native.GetCursorPos(out p))
            {
                AppLog.WriteError("F7：取不到鼠标位置");
                return;
            }

            if (a.Space == CoordSpace.WindowClient && _boundHwnd != IntPtr.Zero && Native.IsWindow(_boundHwnd))
            {
                Native.POINT q = new Native.POINT(p.X, p.Y);
                if (Native.ScreenToClient(_boundHwnd, ref q))
                {
                    a.X = q.X;
                    a.Y = q.Y;
                    AppLog.Write(string.Format(
                        CultureInfo.InvariantCulture,
                        "F7：更新为窗口坐标 ({0}, {1})",
                        a.X,
                        a.Y));
                    RefreshActions();
                    return;
                }
            }

            a.X = p.X;
            a.Y = p.Y;
            if (a.Space == CoordSpace.WindowClient && _boundHwnd == IntPtr.Zero)
            {
                a.Space = CoordSpace.Screen;
            }
            AppLog.Write(string.Format(CultureInfo.InvariantCulture, "F7：更新为屏幕坐标 ({0}, {1})", a.X, a.Y));
            RefreshActions();
        }

        // ==================================================================
        // 定位方式
        // ==================================================================

        /// <summary>切换定位方式（只影响之后新加/编辑的动作）。</summary>
        private void SetSpace(CoordSpace space)
        {
            bool same = _uiSpace == space;
            _uiSpace = space;
            if (!same)
            {
                AppLog.Write("定位方式：" + (space == CoordSpace.Screen ? "屏幕坐标" : "窗口相对"));
            }
            UpdateLocationStatus();

            if (space == CoordSpace.WindowClient)
            {
                if (BoundOk)
                {
                    OfferConvertToWindow();
                }
                else
                {
                    _btnBind.Focus();
                }
            }
        }

        private bool BoundOk
        {
            get { return _boundHwnd != IntPtr.Zero && Native.IsWindow(_boundHwnd); }
        }

        /// <summary>把定位方式的两张卡、绑定按钮、说明文字刷成当前状态。</summary>
        private void UpdateLocationStatus()
        {
            if (_cardScreen == null || _lblSpaceHint == null)
            {
                return;
            }

            bool isWindow = _uiSpace == CoordSpace.WindowClient;
            bool bound = BoundOk;
            _cardScreen.Selected = !isWindow;
            _cardWindow.Selected = isWindow;

            // 绑定信息只放在「窗口相对」这张卡上，别去挤下面那两行说明
            _cardScreen.DescText = "记录屏幕上的绝对位置";
            _cardWindow.DescText = bound
                ? ("已绑定：" + ShortTitle(_boundTitle, 14))
                : "记录目标窗口客户区里的相对位置";

            if (isWindow)
            {
                _lblSpaceHint.Text =
                    "记录的是窗口客户区里的相对位置，执行时按窗口当前位置换算 —— 窗口拖动、重新打开都还能点准。\n"
                    + (bound
                        ? "要换目标窗口就点右上「重新绑定」，3 秒内切过去。"
                        : "先绑定窗口：点右上「绑定窗口」，3 秒内切到目标窗口。");
                _lblSpaceHint.ForeColor = bound ? Theme.Muted : Theme.Warn;
                _btnBind.Text = bound ? "重新绑定" : "绑定窗口";
                _btnBind.Variant = bound ? ButtonVariant.Secondary : ButtonVariant.Primary;
                _btnBind.Enabled = true;
            }
            else
            {
                _lblSpaceHint.Text =
                    "记录鼠标在屏幕上的绝对位置，最简单，但窗口一移动，之前抓的坐标就对不上了。\n"
                    + "这个模式不需要绑定窗口；只有列表里标了「· 窗口相对」的动作才用得到窗口。";
                _lblSpaceHint.ForeColor = Theme.Muted;
                _btnBind.Text = "绑定窗口";
                _btnBind.Variant = ButtonVariant.Secondary;
                _btnBind.Enabled = false;
            }

            _btnBind.Invalidate();
            _cardScreen.Invalidate();
            _cardWindow.Invalidate();
        }

        private static string ShortTitle(string title, int max)
        {
            if (string.IsNullOrEmpty(title))
            {
                return "(无标题)";
            }
            return title.Length > max ? title.Substring(0, max) + "…" : title;
        }

        /// <summary>窗口里已经有屏幕坐标的点击动作时，问一句要不要一起转成窗口坐标。</summary>
        private void OfferConvertToWindow()
        {
            if (!BoundOk)
            {
                return;
            }
            Native.RECT rc;
            Native.GetClientRect(_boundHwnd, out rc);

            List<MacroAction> inside = new List<MacroAction>();
            for (int i = 0; i < _settings.Actions.Count; i++)
            {
                MacroAction a = _settings.Actions[i];
                if ((a.Kind != ActionKind.Click && a.Kind != ActionKind.Wheel) || a.Space != CoordSpace.Screen)
                {
                    continue;
                }
                Native.POINT q = new Native.POINT(a.X, a.Y);
                if (Native.ScreenToClient(_boundHwnd, ref q)
                    && q.X >= 0 && q.Y >= 0 && q.X < rc.Width && q.Y < rc.Height)
                {
                    inside.Add(a);
                }
            }

            if (inside.Count == 0)
            {
                return;
            }

            DialogResult r = MessageBox.Show(
                this,
                "有 " + inside.Count + " 个点击/滚轮动作正好落在当前绑定的窗口里。\n\n"
                + "要把它们一起转成「窗口相对」坐标吗？\n"
                + "转换后窗口再移动，这些动作仍然点得准；窗口外的动作不受影响。",
                "顺便转换一下？",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes)
            {
                return;
            }

            int converted = 0;
            for (int i = 0; i < inside.Count; i++)
            {
                MacroAction a = inside[i];
                Native.POINT q = new Native.POINT(a.X, a.Y);
                if (Native.ScreenToClient(_boundHwnd, ref q))
                {
                    a.X = q.X;
                    a.Y = q.Y;
                    a.Space = CoordSpace.WindowClient;
                    converted++;
                }
            }
            RefreshActions();
            UpdateLocationStatus();
            AppLog.Write("已把 " + converted + " 个动作转成窗口相对坐标");
        }

        private void OnBindClick(object sender, EventArgs e)
        {
            if (_bindTimer.Enabled)
            {
                _bindTimer.Stop();
                _btnBind.Text = "绑定窗口";
                _btnBind.Suffix = "(3s)";
                UpdateLocationStatus();
                return;
            }

            _bindCountdown = 3;
            _btnBind.Text = "切到目标窗口";
            _btnBind.Suffix = "3";
            _lblSpaceHint.Text = "3 秒内切换到要绑定的窗口…";
            _lblSpaceHint.ForeColor = Theme.Accent;
            _bindTimer.Start();
        }

        private void BindTimerTick(object sender, EventArgs e)
        {
            _bindCountdown--;
            if (_bindCountdown > 0)
            {
                _btnBind.Suffix = _bindCountdown.ToString(CultureInfo.InvariantCulture);
                _lblSpaceHint.Text = _bindCountdown + " 秒内切换到要绑定的窗口…";
                return;
            }

            _bindTimer.Stop();
            _btnBind.Text = "绑定窗口";
            _btnBind.Suffix = "(3s)";

            IntPtr fg = Native.GetForegroundWindow();
            if (fg == IntPtr.Zero)
            {
                _lblSpaceHint.Text = "绑定失败：没有前台窗口";
                _lblSpaceHint.ForeColor = Theme.Danger;
                return;
            }
            IntPtr root = Native.GetAncestor(fg, Native.GA_ROOT);
            if (root != IntPtr.Zero)
            {
                fg = root;
            }
            int fgPid = AppEnv.PidOfWindow(fg);
            if (fg == Handle || (fgPid != 0 && fgPid == Process.GetCurrentProcess().Id))
            {
                _lblSpaceHint.Text = "绑定失败：绑到了本程序窗口，请切换目标窗口后重试";
                _lblSpaceHint.ForeColor = Theme.Danger;
                AppLog.WriteError("绑定失败：绑到本程序自己");
                return;
            }

            Native.RECT rc;
            Native.GetClientRect(fg, out rc);
            Native.POINT origin = new Native.POINT(0, 0);
            Native.ClientToScreen(fg, ref origin);

            _boundHwnd = fg;
            _boundTitle = Native.GetWindowTextSafe(fg);
            _boundClientW = rc.Width;
            _boundClientH = rc.Height;
            _boundOriginX = origin.X;
            _boundOriginY = origin.Y;

            UpdateLocationStatus();
            AppLog.Write("已绑定窗口：" + AppEnv.DescribeTarget(0, fg));
            AppLog.Write(string.Format(
                CultureInfo.InvariantCulture,
                "客户区 {0}×{1}，客户区原点 ({2}, {3})",
                _boundClientW,
                _boundClientH,
                _boundOriginX,
                _boundOriginY));

            if (_uiSpace == CoordSpace.WindowClient)
            {
                OfferConvertToWindow();
            }
        }

        // ==================================================================
        // 运行
        // ==================================================================

        private MacroSettings CollectSettings()
        {
            MacroSettings s = new MacroSettings();
            s.Actions = new List<MacroAction>();
            for (int i = 0; i < _settings.Actions.Count; i++)
            {
                s.Actions.Add(_settings.Actions[i].Clone());
            }
            s.LoopCount = _numLoop != null ? _numLoop.Value : _settings.LoopCount;
            s.StartDelayMs = _numDelay != null ? _numDelay.Value : _settings.StartDelayMs;
            s.PositionJitter = _numPosJitter != null ? _numPosJitter.Value : _settings.PositionJitter;
            s.TimeJitter = _numTimeJitter != null ? _numTimeJitter.Value : _settings.TimeJitter;
            s.ClickHoldMs = _numHold != null ? _numHold.Value : _settings.ClickHoldMs;
            s.DoubleClickGapMs = _numDoubleGap != null ? _numDoubleGap.Value : _settings.DoubleClickGapMs;
            s.ActivateBeforeClick = _tglActivate == null || _tglActivate.Checked;
            s.InputMethod = _segInputMethod != null && _segInputMethod.SelectedIndex == 1
                ? MouseInputMethod.MouseEvent
                : MouseInputMethod.SendInput;
            return s;
        }

        private void ToggleStart()
        {
            if (_runner.IsRunning)
            {
                _runner.Stop();
                return;
            }
            StartRun(CollectSettings(), false);
        }

        private void ToggleTest()
        {
            if (_runner.IsRunning)
            {
                _runner.Stop();
                return;
            }
            StartRun(CollectSettings(), true);
        }

        private void StartRun(MacroSettings s, bool isTest)
        {
            if (_recordArmed || (_recorder != null && _recorder.IsRecording))
            {
                StopRecording();
                return;
            }
            if (!RecheckInput(false))
            {
                DialogResult r = MessageBox.Show(
                    this,
                    "自检发现本进程现在无法控制鼠标。\n\n" + _inputCheckDetail + "\n\n可能的原因：\n"
                    + "· 安全软件（电脑管家 / 火绒 / 360 等）拦截了模拟输入\n"
                    + "· 程序放在被限制的目录里\n"
                    + "· 目标程序以管理员运行，而本工具没有提权\n\n仍然开始吗？（多半没有任何效果）",
                    "输入自检未通过",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (r != DialogResult.Yes)
                {
                    return;
                }
            }
            if (s.EnabledInputCount() == 0)
            {
                MessageBox.Show(this, "至少需要一个启用的点击或按键动作。", "无法开始", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (s.HasWindowRelativeAction() && (_boundHwnd == IntPtr.Zero || !Native.IsWindow(_boundHwnd)))
            {
                MessageBox.Show(
                    this,
                    "有动作使用「窗口相对」坐标，但还没有（有效的）绑定窗口。\n请先点「绑定窗口 (3s)」，或把这些动作改成屏幕坐标。",
                    "无法开始",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            SaveSettings();
            _runner.Start(s, _boundHwnd, _boundTitle, isTest);
            UpdateUiState();
        }

        private void RunnerFinished(object sender, RunnerFinishedEventArgs e)
        {
            try
            {
                BeginInvoke(new Action(() =>
                {
                    UpdateUiState();
                    if (e != null && !string.IsNullOrEmpty(e.Error))
                    {
                        _lblStatus.ForeColor = Theme.Danger;
                        _lblStatus.Text = "出错";
                        _dot.DotColor = Theme.Danger;
                        AppLog.WriteError("停止：" + e.Error);
                        if (_runner.ActionsDone == 0)
                        {
                            MessageBox.Show(this, e.Error, "执行失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }));
            }
            catch
            {
            }
        }

        private void ToggleRecord()
        {
            if (_recordArmed || (_recorder != null && _recorder.IsRecording))
            {
                StopRecording();
                return;
            }
            if (_runner.IsRunning)
            {
                _runner.Stop();
            }

            if (_recorder == null)
            {
                _recorder = new Recorder();
                _recorder.OwnPid = Process.GetCurrentProcess().Id;
            }

            _recordArmed = true;
            _recordCountdown = 2;
            _btnRecord.Text = "准备录制";
            _btnRecord.Suffix = "2";
            _dot.DotColor = Theme.Danger;
            _lblStatus.Text = "2 秒后开始录制";
            _recordTimer.Start();
        }

        private void RecordTimerTick(object sender, EventArgs e)
        {
            if (!_recordArmed)
            {
                _recordTimer.Stop();
                return;
            }
            _recordCountdown--;
            if (_recordCountdown > 0)
            {
                _btnRecord.Suffix = _recordCountdown.ToString(CultureInfo.InvariantCulture);
                _lblStatus.Text = _recordCountdown + " 秒后开始录制";
                return;
            }

            _recordTimer.Stop();
            if (_recorder.Start())
            {
                _btnRecord.Text = "停止录制";
                _btnRecord.Suffix = "(F10)";
                _dot.DotColor = Theme.Danger;
                _lblStatus.Text = "录制中";
                _lblDetail.Text = "F10 / F8 结束录制";
                AppLog.Write("开始录制，请操作目标窗口");
            }
            else
            {
                _recordArmed = false;
                _btnRecord.Text = "录制";
                UpdateUiState();
            }
        }

        private void StopRecording()
        {
            if (_recordArmed && _recorder != null && !_recorder.IsRecording)
            {
                _recordArmed = false;
                _recordTimer.Stop();
                _btnRecord.Text = "录制";
                _btnRecord.Suffix = "(F10)";
                UpdateUiState();
                return;
            }
            if (_recorder != null && _recorder.IsRecording)
            {
                _recorder.Stop();
                _btnRecord.Text = "整理中…";
                _btnRecord.Suffix = string.Empty;
            }
        }

        private void HarvestRecording()
        {
            if (_recorder == null || _recorder.IsRecording)
            {
                return;
            }
            if (_recordArmed)
            {
                List<MacroAction> got = _recorder.TakeResult();
                _recordArmed = false;
                if (got.Count > 0)
                {
                    _settings.Actions.AddRange(got);
                    RefreshActions();
                    SaveSettings();
                    AppLog.Write("录制完成，追加 " + got.Count + " 个动作");
                }
                else
                {
                    AppLog.WriteError("录制结束，但没有录到动作");
                }
                _btnRecord.Text = "录制";
                _btnRecord.Suffix = "(F10)";
                UpdateUiState();
            }
        }

        // ==================================================================
        // 历史序列
        // ==================================================================

        private void RefreshHistory()
        {
            if (_gridHistory == null)
            {
                return;
            }
            _gridHistory.Rows.Clear();
            for (int i = 0; i < _sequences.Count; i++)
            {
                SavedSequence s = _sequences[i];
                int idx = _gridHistory.Rows.Add(
                    s.Name,
                    s.Actions.Count.ToString(CultureInfo.InvariantCulture),
                    Fmt.Time(s.SavedAt));
                _gridHistory.Rows[idx].Tag = s;
            }
            if (_gridHistory.Rows.Count > 0)
            {
                int sel = Math.Min(Math.Max(_selectedSequence, 0), _gridHistory.Rows.Count - 1);
                _gridHistory.CurrentCell = _gridHistory.Rows[sel].Cells[0];
            }
            UpdateHistoryPreview();
        }

        private SavedSequence SelectedSequence()
        {
            if (_gridHistory == null || _gridHistory.CurrentRow == null)
            {
                return null;
            }
            return _gridHistory.CurrentRow.Tag as SavedSequence;
        }

        private void UpdateHistoryPreview()
        {
            if (_logPreview == null)
            {
                return;
            }
            SavedSequence s = SelectedSequence();
            if (s == null)
            {
                _logPreview.SetText("选中左侧一条序列，这里会显示它的动作。");
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(s.Name + "   (" + s.Actions.Count + " 个动作)");
            sb.AppendLine(new string('-', 34));
            int winCount = 0;
            for (int i = 0; i < s.Actions.Count; i++)
            {
                MacroAction a = s.Actions[i];
                sb.AppendLine((i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(2) + ". " + a.DescribeForList());
                if (a.Space == CoordSpace.WindowClient)
                {
                    winCount++;
                }
            }
            if (winCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine("注意：其中 " + winCount + " 个动作是窗口相对坐标，执行前需要重新绑定窗口。");
            }
            _logPreview.SetText(sb.ToString());
        }

        private void SaveCurrentSequence()
        {
            if (_settings.Actions.Count == 0)
            {
                MessageBox.Show(this, "当前没有动作可以保存。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string initial = "序列 " + DateTime.Now.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
            NamePromptDialog dlg = new NamePromptDialog("保存当前序列", initial);
            if (dlg.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            string name = dlg.GetValue();

            int existing = -1;
            for (int i = 0; i < _sequences.Count; i++)
            {
                if (string.Equals(_sequences[i].Name, name, StringComparison.Ordinal))
                {
                    existing = i;
                    break;
                }
            }
            if (existing >= 0)
            {
                if (MessageBox.Show(this, "已存在同名序列，覆盖吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }
            }

            SavedSequence seq = new SavedSequence();
            seq.Name = name;
            seq.SavedAt = DateTime.Now;
            for (int i = 0; i < _settings.Actions.Count; i++)
            {
                seq.Actions.Add(_settings.Actions[i].Clone());
            }

            if (existing >= 0)
            {
                _sequences[existing] = seq;
                _selectedSequence = existing;
            }
            else
            {
                _sequences.Add(seq);
                _selectedSequence = _sequences.Count - 1;
            }

            SequenceStore.Save(AppPaths.SequencesFile, _sequences);
            RefreshHistory();
            AppLog.Write("保存序列：" + name + "（" + seq.Actions.Count + " 个动作）");
        }

        private void LoadSelectedSequence()
        {
            SavedSequence s = SelectedSequence();
            if (s == null)
            {
                MessageBox.Show(this, "请先选中一条序列。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_runner.IsRunning)
            {
                _runner.Stop();
            }

            _settings.Actions.Clear();
            for (int i = 0; i < s.Actions.Count; i++)
            {
                _settings.Actions.Add(s.Actions[i].Clone());
            }
            RefreshActions();
            SaveSettings();
            AppLog.Write("载入序列：" + s.Name);

            int winCount = 0;
            for (int i = 0; i < _settings.Actions.Count; i++)
            {
                if (_settings.Actions[i].Space == CoordSpace.WindowClient)
                {
                    winCount++;
                }
            }
            UpdateLocationStatus();
            if (winCount > 0)
            {
                MessageBox.Show(
                    this,
                    "这条序列里有 " + winCount + " 个「窗口相对」动作，执行前需要重新绑定目标窗口。",
                    "载入完成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void RunSelectedSequence(bool isTest)
        {
            SavedSequence s = SelectedSequence();
            if (s == null)
            {
                MessageBox.Show(this, "请先选中一条序列。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_runner.IsRunning)
            {
                _runner.Stop();
                return;
            }

            MacroSettings tmp = CollectSettings();
            tmp.Actions = new List<MacroAction>();
            for (int i = 0; i < s.Actions.Count; i++)
            {
                tmp.Actions.Add(s.Actions[i].Clone());
            }

            if (tmp.HasWindowRelativeAction() && (_boundHwnd == IntPtr.Zero || !Native.IsWindow(_boundHwnd)))
            {
                MessageBox.Show(
                    this,
                    "这条序列包含「窗口相对」动作，请先绑定窗口。",
                    "无法开始",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            AppLog.Write((isTest ? "测试" : "执行") + "历史序列：" + s.Name);
            _runner.Start(tmp, _boundHwnd, _boundTitle, isTest);
            UpdateUiState();
        }

        private void DeleteSelectedSequence()
        {
            SavedSequence s = SelectedSequence();
            if (s == null)
            {
                return;
            }
            if (MessageBox.Show(this, "删除序列「" + s.Name + "」？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            _sequences.Remove(s);
            SequenceStore.Save(AppPaths.SequencesFile, _sequences);
            RefreshHistory();
            AppLog.Write("删除序列：" + s.Name);
        }

        private void RenameSelectedSequence()
        {
            SavedSequence s = SelectedSequence();
            if (s == null)
            {
                return;
            }
            NamePromptDialog dlg = new NamePromptDialog("重命名序列", s.Name);
            if (dlg.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            s.Name = dlg.GetValue();
            SequenceStore.Save(AppPaths.SequencesFile, _sequences);
            RefreshHistory();
            AppLog.Write("重命名序列为：" + s.Name);
        }

        // ==================================================================
        // 诊断
        // ==================================================================

        private void OpenProbe()
        {
            if (_probe == null || _probe.IsDisposed)
            {
                _probe = new ProbeForm();
                _probe.FormClosed += delegate { _probe = null; };
                _probe.StartPosition = FormStartPosition.Manual;
                Rectangle wa = Screen.FromControl(this).WorkingArea;
                int px = Right + 12;
                int py = Top;
                if (px + _probe.Width > wa.Right)
                {
                    px = Math.Max(wa.Left + 12, wa.Right - _probe.Width - 24);
                    py = Math.Max(wa.Top + 12, Math.Min(Top, wa.Bottom - _probe.Height - 24));
                }
                _probe.Location = new Point(px, py);
                _probe.Show(this);
            }
            else
            {
                _probe.Activate();
            }
            AppLog.Write("输入探针已打开：点它看计数，或点「探针自测」");
        }

        private void ProbeRoundTrip()
        {
            OpenProbe();
            if (_probe == null)
            {
                return;
            }
            Application.DoEvents();

            Point origin = _probe.PointToScreen(new Point(60, 110));
            int before = _probe.LeftClicks;

            string detail;
            bool moved = InputEngine.MoveTo(origin.X, origin.Y, out detail);
            AppLog.Write("探针自测 移动：" + detail);
            if (!moved)
            {
                _lblDiagStatus.ForeColor = Theme.Danger;
                _lblDiagStatus.Text = "移动失败：" + detail;
                return;
            }

            Native.ForceForeground(_probe.Handle);
            System.Threading.Thread.Sleep(60);

            bool clicked = InputEngine.Click(MouseBtn.Left, 40, MouseInputMethod.SendInput, out detail);
            AppLog.Write("探针自测 点击：" + detail);
            System.Threading.Thread.Sleep(150);
            Application.DoEvents();

            int after = _probe.LeftClicks;
            if (clicked && after > before)
            {
                _lblDiagStatus.ForeColor = Theme.Success;
                _lblDiagStatus.Text = "自测通过：鼠标移动到探针并点击，探针计数 " + before + " → " + after + "。";
                AppLog.Write("探针自测通过：计数增加");
            }
            else
            {
                _lblDiagStatus.ForeColor = Theme.Danger;
                _lblDiagStatus.Text = "自测失败：点击没有到达探针窗口（移动=" + (moved ? "成功" : "失败")
                    + "，点击=" + (clicked ? "已发送" : "发送失败") + "）。";
                AppLog.WriteError("探针自测失败：点击未到达目标窗口");
            }
        }

        private void CheckPermissions()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("本程序管理员权限：" + (AppEnv.IsSelfElevated ? "是" : "否")
                + "　64 位进程：" + (Environment.Is64BitProcess ? "是" : "否"));

            Native.RECT vs = Native.GetVirtualScreen();
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "虚拟屏幕：{0}×{1}，原点 ({2}, {3})",
                vs.Width,
                vs.Height,
                vs.Left,
                vs.Top));

            IntPtr fg = Native.GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                int pid = AppEnv.PidOfWindow(fg);
                string exe = AppEnv.ProcessNameOf(pid);
                bool? elevated = AppEnv.IsProcessElevated(pid);
                sb.AppendLine();
                sb.AppendLine("前台窗口：" + Native.GetWindowTextSafe(fg));
                sb.AppendLine("进程：" + exe + "（pid " + pid + "）");
                string elevText = elevated == null ? "无法判断" : (elevated.Value ? "是（管理员）" : "否");
                sb.AppendLine("是否提权：" + elevText);
                if (elevated == true && !AppEnv.IsSelfElevated)
                {
                    sb.AppendLine("注意：目标程序是管理员权限，本工具不是。Windows 会拦掉点击 → 用「以管理员重启」。");
                }
            }

            if (_boundHwnd != IntPtr.Zero)
            {
                sb.AppendLine();
                sb.AppendLine("已绑定窗口：" + AppEnv.DescribeTarget(0, _boundHwnd));
            }

            _lblDiagStatus.ForeColor = Theme.TextDim;
            _lblDiagStatus.Text = sb.ToString();
            AppLog.Write("权限检查完成");
        }

        private void OpenLogFile()
        {
            try
            {
                AppPaths.EnsureDir();
                AppLog.Flush();
                if (File.Exists(AppPaths.RunLogFile))
                {
                    Process.Start("notepad.exe", "\"" + AppPaths.RunLogFile + "\"");
                }
                else
                {
                    MessageBox.Show(this, "还没有日志文件（先运行一次）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "打不开日志", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ClearDiagLog()
        {
            _lastLogCount = -1;
            UpdateDiagLog();
        }

        private void RestartAsAdmin()
        {
            if (AppEnv.IsSelfElevated)
            {
                MessageBox.Show(this, "当前已经是管理员权限。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "提权失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateDiagStatus()
        {
            if (_lblDiagStatus == null)
            {
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("数据目录：" + AppPaths.DataDir + (AppPaths.IsPortable ? "（便携：跟着 exe 走）" : "（exe 目录不可写，退回这里）"));
            sb.AppendLine("先用「输入探针」+「探针自测」确认这台机器能合成鼠标键盘。");
            sb.AppendLine("自测能过、游戏里却点不动 → 多半是目标程序提权，或在屏蔽合成输入。");
            sb.AppendLine("屏幕坐标失败时点「打开日志」，看 MOVE / CLICK 两行。");
            _lblDiagStatus.Text = sb.ToString();
            _lblDiagStatus.ForeColor = Theme.Muted;
        }

        /// <summary>启动/开始前的输入能力自检（无损，只把光标设置到原位置）。</summary>
        private bool RecheckInput(bool showDialog)
        {
            string detail;
            bool ok = InputEngine.CanControlCursor(out detail);
            _inputOk = ok;
            _inputCheckDetail = detail;

            if (!ok)
            {
                AppLog.WriteError("输入自检未通过：" + detail);
                if (showDialog)
                {
                    MessageBox.Show(
                        this,
                        "本程序现在无法控制鼠标。\n\n" + detail + "\n\n可能的原因：\n"
                        + "1. 安全软件（电脑管家 / 火绒 / 360 等）拦截了模拟输入 —— 把本程序加入白名单\n"
                        + "2. 程序所在的目录被限制 —— 把 exe 复制到桌面或其它目录再运行\n"
                        + "3. 目标程序以管理员运行，本工具没有 —— 点「以管理员重启」\n\n"
                        + "可以先到「诊断」页用「输入探针」验证。",
                        "输入自检未通过",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            else if (showDialog)
            {
                AppLog.Write("输入自检通过：" + detail);
            }
            return ok;
        }

        private void UpdateDiagLog()
        {
            if (_logDiag == null)
            {
                return;
            }
            string[] all = AppLog.Snapshot();
            if (all.Length == _lastLogCount)
            {
                return;
            }
            _lastLogCount = all.Length;
            _logDiag.SetText(AppLog.Tail(200));
        }

        // ==================================================================
        // 状态刷新
        // ==================================================================

        private void UiTimerTick(object sender, EventArgs e)
        {
            if (_recorder != null && !_recorder.IsRecording && _recordArmed && !_recordTimer.Enabled)
            {
                HarvestRecording();
            }

            UpdateUiState();
            UpdateLogPanel();
            if (_tabIndex == 3)
            {
                UpdateDiagLog();
            }
        }

        private void UpdateUiState()
        {
            if (_lblStatus == null)
            {
                return;
            }

            bool running = _runner.IsRunning;
            bool recording = (_recorder != null && _recorder.IsRecording) || _recordArmed;

            if (running)
            {
                bool test = _runner.IsTest;
                _lblStatus.Text = _runner.Phase;
                _lblStatus.ForeColor = test ? Theme.Accent : Theme.Success;
                _dot.DotColor = test ? Theme.Accent : Theme.Success;
                _btnStart.Text = "停止";
                _btnStart.Suffix = "(F6)";
                _btnStart.Icon = Glyph.Stop;
                _btnStart.Variant = ButtonVariant.Secondary;
                _lblCount.Text = "动作 " + _runner.ActionsDone + " 次 · 第 " + (_runner.LoopsDone + 1) + " 轮";
                if (_runner.CurrentAction >= 0 && _runner.CurrentAction < _settings.Actions.Count)
                {
                    _lblDetail.Text = "当前：" + _settings.Actions[_runner.CurrentAction].DescribeForList();
                }
            }
            else if (recording)
            {
                _lblStatus.Text = _recordArmed ? "准备录制" : "录制中";
                _lblStatus.ForeColor = Theme.Danger;
                _dot.DotColor = Theme.Danger;
                _btnStart.Text = "开始执行";
                _btnStart.Suffix = "(F6)";
                _btnStart.Icon = Glyph.Play;
                _btnStart.Variant = ButtonVariant.Primary;
                _lblCount.Text = _recorder != null
                    ? "已录制 " + _recorder.Count + " 个动作"
                    : "共 " + _settings.Actions.Count + " 个动作";
            }
            else
            {
                bool error = _runner.Phase == "出错";
                _lblStatus.Text = error ? "出错" : "就绪";
                _lblStatus.ForeColor = error ? Theme.Danger : Theme.Text;
                _dot.DotColor = error ? Theme.Danger : Theme.Muted;
                _btnStart.Text = "开始执行";
                _btnStart.Suffix = "(F6)";
                _btnStart.Icon = Glyph.Play;
                _btnStart.Variant = ButtonVariant.Primary;
                int enabledAll = 0;
                for (int i = 0; i < _settings.Actions.Count; i++)
                {
                    if (_settings.Actions[i].Enabled)
                    {
                        enabledAll++;
                    }
                }
                _lblCount.Text = "共 " + _settings.Actions.Count + " 个动作（启用 " + enabledAll + " 个）";

                int roundMs = EstimateRoundMs();
                string round = roundMs > 0
                    ? "整轮 ≈ " + (roundMs / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + " 秒"
                    : "F8 紧急停止";
                if (!_inputOk)
                {
                    _lblDetail.Text = "输入自检未通过，见「诊断」页";
                    _lblDetail.ForeColor = Theme.Danger;
                }
                else
                {
                    string loop = _settings.LoopCount <= 0 ? "无限循环" : ("循环 " + _settings.LoopCount + " 次");
                    _lblDetail.Text = loop + " · " + round;
                    _lblDetail.ForeColor = Theme.Muted;
                }
            }

            _btnTest.Enabled = !recording;
            _btnStart.Invalidate();
        }

        private void UpdateLogPanel()
        {
            if (_logPanel == null)
            {
                return;
            }
            string[] all = AppLog.Snapshot();
            List<string> tail = new List<string>();
            for (int i = Math.Max(0, all.Length - 3); i < all.Length; i++)
            {
                string line = all[i];
                int sp = line.IndexOf("  ", StringComparison.Ordinal);
                if (sp > 0 && sp < 14)
                {
                    line = line.Substring(0, 8) + "  " + line.Substring(sp + 2);
                }
                tail.Add(line);
            }
            _logPanel.SetLines(tail);
            _lblLogTail.Text = "最近 " + tail.Count + " 条";
        }

        // ==================================================================
        // 热键
        // ==================================================================

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotkeysCmd();
        }

        private void RegisterHotkeysCmd()
        {
            bool ok = true;
            ok &= Hotkeys.Register(Handle, HotkeyIdStart, (uint)Keys.F6);
            ok &= Hotkeys.Register(Handle, HotkeyIdCapture, (uint)Keys.F7);
            ok &= Hotkeys.Register(Handle, HotkeyIdStop, (uint)Keys.F8);
            ok &= Hotkeys.Register(Handle, HotkeyIdTest, (uint)Keys.F9);
            ok &= Hotkeys.Register(Handle, HotkeyIdRecord, (uint)Keys.F10);
            if (!ok)
            {
                AppLog.WriteError("部分全局快捷键被占用（F6-F10），请关掉占用的程序后重启本工具");
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                bool busy = _activeDialog != null;
                try
                {
                    if (id == HotkeyIdCapture)
                    {
                        if (_activeDialog != null)
                        {
                            _activeDialog.CaptureNow();
                        }
                        else
                        {
                            CaptureIntoSelected();
                        }
                    }
                    else if (id == HotkeyIdStop)
                    {
                        EmergencyStop();
                    }
                    else if (!busy)
                    {
                        if (id == HotkeyIdStart)
                        {
                            ToggleStart();
                        }
                        else if (id == HotkeyIdTest)
                        {
                            ToggleTest();
                        }
                        else if (id == HotkeyIdRecord)
                        {
                            ToggleRecord();
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLog.WriteError("热键处理出错：" + ex.Message);
                }
                return;
            }
            base.WndProc(ref m);
        }

        private void EmergencyStop()
        {
            if (_recordArmed || (_recorder != null && _recorder.IsRecording))
            {
                StopRecording();
            }
            if (_runner.IsRunning)
            {
                _runner.Stop();
                AppLog.WriteError("F8 紧急停止");
            }
            InputEngine.ReleaseAll();
            UpdateUiState();
        }

        // ==================================================================
        // 配置读写
        // ==================================================================

        private void LoadSettings()
        {
            MacroSettings loaded;
            bool topMost;
            bool dark;
            CoordSpace space;
            int wx;
            int wy;
            bool ok = SettingsStore.Load(AppPaths.SettingsFile, out loaded, out topMost, out dark, out space, out wx, out wy);

            if (ok)
            {
                _settings = loaded;
                _topMost = topMost;
                _dark = dark;
                _uiSpace = space;
                _savedX = wx;
                _savedY = wy;
            }
            else
            {
                _settings = new MacroSettings();
                MacroAction demo = new MacroAction();
                demo.Kind = ActionKind.Click;
                demo.X = 500;
                demo.Y = 400;
                demo.WaitMs = 120;
                _settings.Actions.Add(demo);
                _settings.LoopCount = 0;
                _settings.StartDelayMs = 800;
                AppLog.Write("没有找到配置文件，使用示例动作");
            }
            ApplyThemeStatic();
        }

        private static void ApplyThemeStatic()
        {
            Theme.Apply(Theme.Dark);
        }

        private int _savedX = -1;
        private int _savedY = -1;

        private void RestoreWindowPosition()
        {
            if (_savedX < -10000 || _savedY < -10000)
            {
                return;
            }
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Rectangle probe = new Rectangle(_savedX, _savedY, Width, Height);
            if (wa.IntersectsWith(probe))
            {
                StartPosition = FormStartPosition.Manual;
                Location = new Point(_savedX, _savedY);
            }
        }

        private void SaveSettings()
        {
            MacroSettings s = CollectSettings();
            _settings.LoopCount = s.LoopCount;
            _settings.StartDelayMs = s.StartDelayMs;
            _settings.PositionJitter = s.PositionJitter;
            _settings.TimeJitter = s.TimeJitter;
            _settings.ClickHoldMs = s.ClickHoldMs;
            _settings.DoubleClickGapMs = s.DoubleClickGapMs;
            _settings.ActivateBeforeClick = s.ActivateBeforeClick;
            _settings.InputMethod = s.InputMethod;
            SettingsStore.Save(AppPaths.SettingsFile, _settings, _topMost, _dark, _uiSpace, Location.X, Location.Y);
        }

        private void LoadSequences()
        {
            _sequences = SequenceStore.Load(AppPaths.SequencesFile);
        }

        // ==================================================================
        // 生命周期
        // ==================================================================

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Theme.ApplyTitleBar(this, _dark);
            TopMost = _topMost;
            ApplyTheme();
            LoadSequences();
            RefreshHistory();
            UpdateLocationStatus();
            UpdateDiagStatus();
            RecheckInput(true);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                SaveSettings();
            }
            catch
            {
            }
            try
            {
                Hotkeys.UnregisterAll(Handle);
            }
            catch
            {
            }
            try
            {
                if (_runner.IsRunning)
                {
                    _runner.Stop();
                }
            }
            catch
            {
            }
            try
            {
                if (_recorder != null && (_recorder.IsRecording || _recordArmed))
                {
                    _recorder.Stop();
                    System.Threading.Thread.Sleep(100);
                }
            }
            catch
            {
            }
            try
            {
                if (_wheelRouter != null)
                {
                    Application.RemoveMessageFilter(_wheelRouter);
                }
            }
            catch
            {
            }
            AppLog.Write("程序退出");
            AppLog.Shutdown();
            base.OnFormClosing(e);
        }
    }

    /// <summary>
    /// WM_MOUSEWHEEL 默认只发给有焦点的控件，鼠标悬停在日志上直接滚是没反应的。
    /// 这里把它转发给光标下面的 LogView，滚动就不用先点一下。
    /// </summary>
    internal sealed class WheelRouter : IMessageFilter
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL)
            {
                return false;
            }
            try
            {
                Point p = Cursor.Position;
                IntPtr hwnd = Native.WindowFromPoint(new Native.POINT(p.X, p.Y));
                if (hwnd == IntPtr.Zero)
                {
                    return false;
                }
                Control c = Control.FromHandle(hwnd);
                LogView view = c as LogView;
                if (view == null)
                {
                    return false;
                }
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                view.ApplyWheelDelta(delta);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
