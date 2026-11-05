using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace AutoClicker
{
    /// <summary>所有对话框的基类：主题、标题、确定/取消。</summary>
    public class DialogBase : Form
    {
        protected RoundButton BtnOk;
        protected RoundButton BtnCancel;

        protected DialogBase(string title)
        {
            Text = title;
            ClientSize = new Size(440, 320);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            DoubleBuffered = true;
            KeyPreview = true;

            BtnOk = new RoundButton();
            BtnOk.Text = "确定";
            BtnOk.Variant = ButtonVariant.Primary;
            BtnOk.Size = new Size(96, 34);
            BtnOk.BackColor = Theme.Card;
            BtnOk.DialogResult = DialogResult.None;
            BtnOk.Click += delegate
            {
                if (ValidateInput())
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };
            Controls.Add(BtnOk);

            BtnCancel = new RoundButton();
            BtnCancel.Text = "取消";
            BtnCancel.Variant = ButtonVariant.Secondary;
            BtnCancel.Size = new Size(96, 34);
            BtnCancel.BackColor = Theme.Card;
            BtnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(BtnCancel);

            CancelButton = BtnCancel;
            AcceptButton = BtnOk;
            LayoutButtons();
        }

        protected void LayoutButtons()
        {
            if (BtnOk != null)
            {
                BtnOk.Location = new Point(ClientSize.Width - 96 - 114, ClientSize.Height - 52);
            }
            if (BtnCancel != null)
            {
                BtnCancel.Location = new Point(ClientSize.Width - 96 - 20, ClientSize.Height - 52);
            }
        }

        protected virtual bool ValidateInput()
        {
            return true;
        }

        protected void Error(string message)
        {
            MessageBox.Show(this, message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Theme.ApplyTitleBar(this, Theme.Dark);
        }
    }

    /// <summary>F7 捕捉坐标的接收方。</summary>
    public interface ICaptureTarget
    {
        bool CanCapture { get; }
        void CaptureNow();
    }

    public class ActionDialog : DialogBase, ICaptureTarget
    {
        private readonly MacroAction _source;
        private readonly bool _isNew;
        private readonly CoordSpace _space;
        private readonly IntPtr _window;
        private readonly string _windowTitle;

        private ThemedCombo _cmbButton;
        private ToggleSwitch _tglDouble;
        private Segmented _segWheelDir;
        private NumericBox _numWheelCount;
        private NumericBox _numX;
        private NumericBox _numY;
        private NumericBox _numWait;
        private Label _lblWaitPreview;
        private Label _lblCaptureHint;
        private Timer _captureTimer;
        private int _captureCountdown;

        private TextBox _keyBox;
        private Keys _capturedKey = Keys.None;
        private Keys _capturedMods = Keys.None;
        private Label _lblKeyHint;

        private ToggleSwitch _tglEnabled;
        private Label _lblSpace;
        private CoordSpace _valueSpace = CoordSpace.Screen;

        public ActionDialog(MacroAction action, bool isNew, CoordSpace space, IntPtr window, string windowTitle)
            : base("动作")
        {
            _source = action;
            _isNew = isNew;
            _space = space;
            _window = window;
            _windowTitle = windowTitle ?? string.Empty;

            bool isClick = action.Kind == ActionKind.Click;
            bool isKey = action.Kind == ActionKind.Key;
            bool isWheel = action.Kind == ActionKind.Wheel;

            string kindName = isClick ? "点击" : (isWheel ? "滚轮" : (isKey ? "按键" : "等待"));
            Text = (isNew ? "添加" : "编辑") + kindName;
            ClientSize = new Size(440, isClick ? 392 : (isWheel ? 330 : (isKey ? 330 : 246)));
            LayoutButtons();

            Label title = Ui.Label(kindName, 22, 18, 220, 26, Theme.Text, new Font("Microsoft YaHei UI", 12F, FontStyle.Bold));
            Controls.Add(title);

            Label lblEnabled = Ui.Label("启用此动作", 22, ClientSize.Height - 66, 100, 22, Theme.TextDim, Theme.SmallFont);
            Controls.Add(lblEnabled);
            _tglEnabled = new ToggleSwitch();
            _tglEnabled.Location = new Point(126, ClientSize.Height - 66);
            _tglEnabled.Size = new Size(40, 22);
            _tglEnabled.Checked = true;
            _tglEnabled.CheckedChanged += delegate { Invalidate(); };
            Controls.Add(_tglEnabled);

            if (isClick)
            {
                BuildClick();
            }
            else if (isWheel)
            {
                BuildWheel();
            }
            else if (isKey)
            {
                BuildKey();
            }
            else
            {
                BuildWait();
            }

            LoadValues();
        }

        private void BuildClick()
        {
            Controls.Add(Ui.Label("鼠标按钮", 22, 62, 70, 22, Theme.Muted, Theme.SmallFont));
            _cmbButton = new ThemedCombo();
            _cmbButton.Location = new Point(100, 58);
            _cmbButton.Size = new Size(130, 28);
            _cmbButton.Items.Add("左键");
            _cmbButton.Items.Add("右键");
            _cmbButton.Items.Add("中键");
            _cmbButton.SelectedIndex = 0;
            Controls.Add(_cmbButton);

            Controls.Add(Ui.Label("双击", 256, 62, 40, 22, Theme.Muted, Theme.SmallFont));
            _tglDouble = new ToggleSwitch();
            _tglDouble.Location = new Point(300, 60);
            _tglDouble.Size = new Size(40, 22);
            Controls.Add(_tglDouble);

            Controls.Add(Ui.Label("X", 22, 104, 16, 22, Theme.Muted, Theme.SmallFont));
            _numX = new NumericBox();
            _numX.Min = -100000;
            _numX.Max = 100000;
            _numX.Step = 10;
            Ui.Host(this, _numX, 42, 102, 108, 32);

            Controls.Add(Ui.Label("Y", 160, 104, 16, 22, Theme.Muted, Theme.SmallFont));
            _numY = new NumericBox();
            _numY.Min = -100000;
            _numY.Max = 100000;
            _numY.Step = 10;
            Ui.Host(this, _numY, 180, 102, 108, 32);

            RoundButton btnCapture = new RoundButton();
            btnCapture.Text = "捕捉坐标";
            btnCapture.Suffix = "(F7)";
            btnCapture.Variant = ButtonVariant.Secondary;
            btnCapture.Icon = Glyph.Target;
            btnCapture.Location = new Point(302, 102);
            btnCapture.Size = new Size(118, 32);
            btnCapture.BackColor = Theme.Card;
            btnCapture.Click += delegate { StartCaptureCountdown(); };
            Controls.Add(btnCapture);

            _lblSpace = Ui.Label(string.Empty, 22, 146, 396, 20, Theme.Muted, Theme.SmallFont);
            Controls.Add(_lblSpace);

            _lblCaptureHint = Ui.Label(
                "把鼠标移到目标位置后按 F7（或点按钮后 1.5 秒捕捉）。",
                22,
                168,
                396,
                20,
                Theme.Muted,
                Theme.TinyFont);
            Controls.Add(_lblCaptureHint);

            Controls.Add(Ui.Label("动作后等待", 22, 206, 80, 22, Theme.Muted, Theme.SmallFont));
            _numWait = new NumericBox();
            _numWait.Min = 0;
            _numWait.Max = 3600000;
            _numWait.Step = 10;
            _numWait.TextChanged += delegate { UpdateWaitPreview(); };
            Ui.Host(this, _numWait, 104, 202, 84, 32);

            Controls.Add(Ui.Label("ms", 194, 206, 30, 22, Theme.Muted, Theme.SmallFont));
            _lblWaitPreview = Ui.Label(string.Empty, 220, 206, 200, 22, Theme.Accent, Theme.SmallFont);
            Controls.Add(_lblWaitPreview);

            _captureTimer = new Timer();
            _captureTimer.Interval = 100;
            _captureTimer.Tick += CaptureTimerTick;
        }

        private void BuildWheel()
        {
            Controls.Add(Ui.Label("滚动方向", 22, 62, 70, 22, Theme.Muted, Theme.SmallFont));
            _segWheelDir = new Segmented();
            _segWheelDir.SetItems("向上滚动", "向下滚动");
            _segWheelDir.Location = new Point(100, 56);
            _segWheelDir.Size = new Size(166, 32);
            _segWheelDir.SelectedIndex = _source.WheelDelta >= 0 ? 0 : 1;
            Controls.Add(_segWheelDir);

            Controls.Add(Ui.Label("格数", 288, 62, 36, 22, Theme.Muted, Theme.SmallFont));
            _numWheelCount = new NumericBox();
            _numWheelCount.Min = 1;
            _numWheelCount.Max = 50;
            _numWheelCount.Step = 1;
            _numWheelCount.Value = Math.Max(1, Math.Abs(_source.WheelDelta));
            Ui.Host(this, _numWheelCount, 328, 54, 90, 32);

            Controls.Add(Ui.Label("X", 22, 104, 16, 22, Theme.Muted, Theme.SmallFont));
            _numX = new NumericBox();
            _numX.Min = -100000;
            _numX.Max = 100000;
            _numX.Step = 10;
            _numX.Value = _source.X;
            Ui.Host(this, _numX, 42, 102, 108, 32);

            Controls.Add(Ui.Label("Y", 160, 104, 16, 22, Theme.Muted, Theme.SmallFont));
            _numY = new NumericBox();
            _numY.Min = -100000;
            _numY.Max = 100000;
            _numY.Step = 10;
            _numY.Value = _source.Y;
            Ui.Host(this, _numY, 180, 102, 108, 32);

            RoundButton btnCapture = new RoundButton();
            btnCapture.Text = "捕捉坐标";
            btnCapture.Suffix = "(F7)";
            btnCapture.Variant = ButtonVariant.Secondary;
            btnCapture.Icon = Glyph.Target;
            btnCapture.Location = new Point(302, 102);
            btnCapture.Size = new Size(118, 32);
            btnCapture.BackColor = Theme.Card;
            btnCapture.Click += delegate { StartCaptureCountdown(); };
            Controls.Add(btnCapture);

            _lblSpace = Ui.Label(string.Empty, 22, 146, 396, 20, Theme.Muted, Theme.SmallFont);
            Controls.Add(_lblSpace);

            _lblCaptureHint = Ui.Label(
                "滚轮会先移动到目标位置再滚动；按 F7 捕捉鼠标当前所在位置。",
                22,
                168,
                400,
                20,
                Theme.Muted,
                Theme.TinyFont);
            Controls.Add(_lblCaptureHint);

            Controls.Add(Ui.Label("动作后等待", 22, 206, 80, 22, Theme.Muted, Theme.SmallFont));
            _numWait = new NumericBox();
            _numWait.Min = 0;
            _numWait.Max = 3600000;
            _numWait.Step = 10;
            _numWait.TextChanged += delegate { UpdateWaitPreview(); };
            Ui.Host(this, _numWait, 104, 202, 84, 32);

            Controls.Add(Ui.Label("ms", 194, 206, 30, 22, Theme.Muted, Theme.SmallFont));
            _lblWaitPreview = Ui.Label(string.Empty, 220, 206, 200, 22, Theme.Accent, Theme.SmallFont);
            Controls.Add(_lblWaitPreview);

            _captureTimer = new Timer();
            _captureTimer.Interval = 100;
            _captureTimer.Tick += CaptureTimerTick;
        }

        private void BuildKey()
        {
            Controls.Add(Ui.Label("按下的键", 22, 62, 80, 22, Theme.Muted, Theme.SmallFont));

            _keyBox = new TextBox();
            _keyBox.ReadOnly = true;
            _keyBox.TextAlign = HorizontalAlignment.Center;
            _keyBox.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
            _keyBox.ForeColor = Theme.Accent;
            _keyBox.Text = "点这里，然后按下组合键";
            _keyBox.KeyDown += KeyBoxKeyDown;
            Ui.Host(this, _keyBox, 100, 56, 226, 34);

            RoundButton btnClear = new RoundButton();
            btnClear.Text = "清除";
            btnClear.Variant = ButtonVariant.Ghost;
            btnClear.Location = new Point(334, 58);
            btnClear.Size = new Size(66, 30);
            btnClear.BackColor = Theme.Card;
            btnClear.Click += delegate
            {
                _capturedKey = Keys.None;
                _capturedMods = Keys.None;
                _keyBox.Text = "点这里，然后按下组合键";
            };
            Controls.Add(btnClear);

            _lblKeyHint = Ui.Label(
                "支持 Ctrl / Alt / Shift 组合键；不能只按修饰键。",
                22,
                98,
                396,
                20,
                Theme.Muted,
                Theme.TinyFont);
            Controls.Add(_lblKeyHint);

            Controls.Add(Ui.Label("动作后等待", 22, 138, 80, 22, Theme.Muted, Theme.SmallFont));
            _numWait = new NumericBox();
            _numWait.Min = 0;
            _numWait.Max = 3600000;
            _numWait.Step = 10;
            _numWait.TextChanged += delegate { UpdateWaitPreview(); };
            Ui.Host(this, _numWait, 104, 134, 84, 32);

            Controls.Add(Ui.Label("ms", 194, 138, 30, 22, Theme.Muted, Theme.SmallFont));
            _lblWaitPreview = Ui.Label(string.Empty, 220, 138, 200, 22, Theme.Accent, Theme.SmallFont);
            Controls.Add(_lblWaitPreview);
        }

        private void BuildWait()
        {
            Controls.Add(Ui.Label("等待时长", 22, 62, 80, 22, Theme.Muted, Theme.SmallFont));
            _numWait = new NumericBox();
            _numWait.Min = 0;
            _numWait.Max = 3600000;
            _numWait.Step = 50;
            _numWait.TextChanged += delegate { UpdateWaitPreview(); };
            Ui.Host(this, _numWait, 104, 58, 92, 32);

            Controls.Add(Ui.Label("ms", 202, 62, 30, 22, Theme.Muted, Theme.SmallFont));

            _lblWaitPreview = Ui.Label(string.Empty, 22, 102, 300, 22, Theme.Accent, Theme.SmallFont);
            Controls.Add(_lblWaitPreview);

            Controls.Add(Ui.Label("等待动作不产生任何输入，只用来控制节奏。", 22, 132, 396, 20, Theme.Muted, Theme.TinyFont));
        }

        private void LoadValues()
        {
            _tglEnabled.Checked = _source.Enabled;
            _numWait.Value = Math.Max(0, _source.WaitMs);

            if (_source.Kind == ActionKind.Click)
            {
                _cmbButton.SelectedIndex = (int)_source.Button;
                _tglDouble.Checked = _source.DoubleClick;
                _numX.Value = _source.X;
                _numY.Value = _source.Y;
                _valueSpace = _source.Space;

                if (_source.Space == CoordSpace.WindowClient)
                {
                    _lblSpace.Text = "坐标类型：窗口客户区相对坐标（相对绑定窗口）";
                    _lblSpace.ForeColor = Theme.Accent;
                }
                else
                {
                    _lblSpace.Text = "坐标类型：屏幕绝对坐标";
                    _lblSpace.ForeColor = Theme.Muted;
                }
            }
            else if (_source.Kind == ActionKind.Key)
            {
                _capturedKey = _source.KeyCode;
                _capturedMods = _source.Modifiers;
                if (_capturedKey != Keys.None)
                {
                    _keyBox.Text = HotkeyText.Describe(_capturedKey, _capturedMods);
                }
            }
            UpdateWaitPreview();
        }

        private void UpdateWaitPreview()
        {
            if (_lblWaitPreview == null || _numWait == null)
            {
                return;
            }
            _lblWaitPreview.Text = Fmt.Wait(_numWait.Value);
        }

        // ---------------- 捕捉坐标 ----------------

        public bool CanCapture
        {
            get { return _source.Kind == ActionKind.Click || _source.Kind == ActionKind.Wheel; }
        }

        public void CaptureNow()
        {
            if (!CanCapture)
            {
                return;
            }
            StopCountdown();

            Native.POINT p;
            if (!Native.GetCursorPos(out p))
            {
                _lblCaptureHint.Text = "捕捉失败：取不到鼠标位置。";
                _lblCaptureHint.ForeColor = Theme.Danger;
                return;
            }

            if (_space == CoordSpace.WindowClient && _window != IntPtr.Zero && Native.IsWindow(_window))
            {
                Native.POINT q = new Native.POINT(p.X, p.Y);
                if (Native.ScreenToClient(_window, ref q))
                {
                    _numX.Value = Fmt.Clamp(q.X, _numX.Min, _numX.Max);
                    _numY.Value = Fmt.Clamp(q.Y, _numY.Min, _numY.Max);
                    _valueSpace = CoordSpace.WindowClient;
                    _lblSpace.Text = "坐标类型：窗口客户区相对坐标（相对绑定窗口）";
                    _lblSpace.ForeColor = Theme.Accent;
                    _lblCaptureHint.Text = string.Format(
                        CultureInfo.InvariantCulture,
                        "已捕捉：窗口坐标 ({0}, {1})。需要重来时按 F7。",
                        q.X,
                        q.Y);
                    _lblCaptureHint.ForeColor = Theme.Success;
                    return;
                }
            }

            _numX.Value = Fmt.Clamp(p.X, _numX.Min, _numX.Max);
            _numY.Value = Fmt.Clamp(p.Y, _numY.Min, _numY.Max);
            _valueSpace = CoordSpace.Screen;
            _lblSpace.Text = _space == CoordSpace.WindowClient && _window == IntPtr.Zero
                ? "坐标类型：屏幕绝对坐标（没有绑定窗口，窗口相对不可用）"
                : "坐标类型：屏幕绝对坐标";
            _lblSpace.ForeColor = Theme.Muted;
            _lblCaptureHint.Text = string.Format(
                CultureInfo.InvariantCulture,
                "已捕捉：屏幕坐标 ({0}, {1})。需要重来时按 F7。",
                p.X,
                p.Y);
            _lblCaptureHint.ForeColor = Theme.Success;
        }

        private void StartCaptureCountdown()
        {
            if (!CanCapture)
            {
                return;
            }
            _captureCountdown = 15;
            _lblCaptureHint.ForeColor = Theme.Accent;
            _lblCaptureHint.Text = "1.5 秒后捕捉…";
            _captureTimer.Start();
        }

        private void CaptureTimerTick(object sender, EventArgs e)
        {
            _captureCountdown--;
            if (_captureCountdown <= 0)
            {
                _captureTimer.Stop();
                CaptureNow();
            }
            else
            {
                _lblCaptureHint.Text = (_captureCountdown / 10.0).ToString("0.0", CultureInfo.InvariantCulture) + " 秒后捕捉…";
            }
        }

        private void StopCountdown()
        {
            if (_captureTimer != null)
            {
                _captureTimer.Stop();
            }
        }

        // ---------------- 按键捕捉 ----------------

        private void KeyBoxKeyDown(object sender, KeyEventArgs e)
        {
            Keys key;
            Keys mods;
            HotkeyText.Split(e, out key, out mods);
            e.Handled = true;
            e.SuppressKeyPress = true;

            if (!HotkeyText.IsValidKey(key))
            {
                _lblKeyHint.Text = "这只是修饰键，请再按一个主键（例如 Ctrl+A 里的 A）。";
                _lblKeyHint.ForeColor = Theme.Danger;
                return;
            }

            _capturedKey = key;
            _capturedMods = mods;
            _keyBox.Text = HotkeyText.Describe(key, mods);
            _lblKeyHint.Text = "已记录：" + HotkeyText.Describe(key, mods);
            _lblKeyHint.ForeColor = Theme.Success;
        }

        // ---------------- 校验 / 结果 ----------------

        protected override bool ValidateInput()
        {
            if (_source.Kind == ActionKind.Key && !HotkeyText.IsValidKey(_capturedKey))
            {
                Error("请先在输入框里按下一个要发送的按键。");
                _keyBox.Focus();
                return false;
            }
            return true;
        }

        public MacroAction GetResult()
        {
            MacroAction a = _source.Clone();
            a.Enabled = _tglEnabled.Checked;
            a.WaitMs = _numWait.Value;

            if (_source.Kind == ActionKind.Click)
            {
                a.Button = (MouseBtn)Math.Max(0, _cmbButton.SelectedIndex);
                a.DoubleClick = _tglDouble.Checked;
                a.X = _numX.Value;
                a.Y = _numY.Value;
                a.Space = _valueSpace;
            }
            else if (_source.Kind == ActionKind.Wheel)
            {
                int count = Math.Max(1, _numWheelCount.Value);
                a.WheelDelta = _segWheelDir.SelectedIndex == 1 ? -count : count;
                a.X = _numX.Value;
                a.Y = _numY.Value;
                a.Space = _valueSpace;
            }
            else if (_source.Kind == ActionKind.Key)
            {
                a.KeyCode = _capturedKey;
                a.Modifiers = _capturedMods;
            }
            return a;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            StopCountdown();
        }
    }

    /// <summary>输入一个名字（保存/重命名历史序列）。</summary>
    public class NamePromptDialog : DialogBase
    {
        private TextBox _box;
        public string Value;

        public NamePromptDialog(string title, string initial)
            : base(title)
        {
            ClientSize = new Size(400, 186);
            LayoutButtons();

            Controls.Add(Ui.Label("名称", 22, 22, 60, 20, Theme.Muted, Theme.TinyFont));

            _box = new NormalTextBox();
            _box.Text = initial ?? string.Empty;
            Ui.Host(this, _box, 22, 46, 356, 34);
            _box.SelectAll();
        }

        protected override bool ValidateInput()
        {
            string v = (_box.Text ?? string.Empty).Trim();
            if (v.Length == 0)
            {
                Error("名称不能为空。");
                _box.Focus();
                return false;
            }
            if (v.Length > 40)
            {
                v = v.Substring(0, 40);
            }
            Value = v;
            return true;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _box.Focus();
            _box.SelectAll();
        }

        public string GetValue()
        {
            return Value;
        }
    }

    /// <summary>简单的信息框（主题化）。</summary>
    public class InfoDialog : DialogBase
    {
        public InfoDialog(string title, string message)
            : base(title)
        {
            ClientSize = new Size(480, 300);
            LayoutButtons();

            NormalTextBox t = Ui.Themed(0, 0, 400, 200, message, true);
            t.ReadOnly = true;
            Ui.Host(this, t, 20, 20, 440, 196);
            BtnCancel.Text = "关闭";
            BtnOk.Visible = false;
        }

        protected override bool ValidateInput()
        {
            return true;
        }
    }
}
