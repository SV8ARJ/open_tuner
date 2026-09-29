using System;
using System.Windows.Forms;
using System.Drawing;

namespace opentuner.Utilities
{
    public class DynamicPropertyMediaControls : DynamicPropertyInterface
    {
        private delegate void UpdateLabelDelegate(Label Lbl, Object obj);
        private delegate void UpdateLabelColorDelegate(Label Lbl, Color Col);

        public delegate void ButtonPressedCallback(string key, int function); // 0 = mute, 1 snapshot, 2 = record, 3 = UDP stream, 4 = stop TS

        protected GroupBox _parent;
        protected string _key;
        protected string _title;
        protected string _value;

        // ShowAlways: without it a ToolTip only shows while the containing form is the active
        // window - with several other windows open (chat, spectrum, video) that made these
        // tooltips "often not shown" (issue #9).
        ToolTip _toolTip = new ToolTip { ShowAlways = true };

        private Button _MuteButton;
        private Button _SnapshotButton;
        private Button _UDPStreamButton;
        private Button _RecordButton;
        private Button _StopTSButton;

        private readonly int buttonWidth = 56;
        private readonly int buttonHeight = 26;
        private readonly int button_gap = 2;
        private readonly int left_margin = -10;

        // False for sources that can't stop their tuner (only MiniTiouner/PicoTuner can, see
        // OTSource.StopTuner): the "Stop TS" button is then not created at all and the other
        // buttons close the gap - see ButtonLeft().
        private readonly bool _showStopTS;

        public override string Key
        {
            get { return _key; }
            set { _key = value; }
        }

        public override string LastValue => _value;

        ButtonPressedCallback _buttonPressedCallback;

        private void UpdateLabel(Label Lbl, Object obj)
        {
            if (Lbl == null)
                return;

            if (Lbl.InvokeRequired)
            {
                UpdateLabelDelegate ulb = new UpdateLabelDelegate(UpdateLabel);
                try
                {
                    Lbl.Invoke(ulb, new object[] { Lbl, obj });
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ObjectDisposedException || ex is System.ComponentModel.InvalidAsynchronousStateException)
                {
                }
            }
            else
            {
                Lbl.Text = obj.ToString();
            }
        }

        private void UpdateColor(Label Lbl, Color Col)
        {
            if (Lbl == null)
                return;

            if (Lbl.InvokeRequired)
            {
                UpdateLabelColorDelegate ulb = new UpdateLabelColorDelegate(UpdateColor);
                try
                {
                    Lbl.Invoke(ulb, new object[] { Lbl, Col });
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ObjectDisposedException || ex is System.ComponentModel.InvalidAsynchronousStateException)
                {
                }
            }
            else
            {
                Lbl.BackColor = Col;
            }
        }

        public override void UpdateValue(string Value)
        {
        }

        public DynamicPropertyMediaControls(GroupBox Group, string Key, string Title, ButtonPressedCallback ButtonPressedCB, bool showStopTS = true)
        {
            _buttonPressedCallback = ButtonPressedCB;
            _showStopTS = showStopTS;
            InitComponents(Group, Key, Title, Color.Transparent);
        }

        protected virtual void InitComponents(GroupBox Group, string Key, string Title, System.Drawing.Color Color)
        {
            _parent = Group;
            _key = Key;
            _title = Title;

            int top_margin = 10;

            _parent.Resize += _parent_Resize;

            int top = _parent.Controls[_parent.Controls.Count - 1].Top + _parent.Controls[_parent.Controls.Count - 1].Height + top_margin;

            // Order left to right: Mute, Snap, UDP, Record, Stop TS - slotFromRight counts from the
            // right-aligned edge, 0 = rightmost (Stop TS).
            _MuteButton = MakeButton("Mute", "Mute TS audio", 4, top);
            _MuteButton.Click += _MuteButton_Click;

            // Snap/Record tooltips get the actual save path appended once it is known - see
            // SetSnapshotTooltip/SetRecordTooltip (called once ConfigureMediaPath/ConfigureTSRecorders run).
            _SnapshotButton = MakeButton("Snap", "Snapshot picture of this tuner", 3, top);
            _SnapshotButton.Click += _SnapshotButton_Click;

            _UDPStreamButton = MakeButton("UDP", "Start/stop UDP stream", 2, top);
            _UDPStreamButton.Click += _UDPStreamButton_Click;

            _RecordButton = MakeButton("REC", "Start/stop recording the TS stream", 1, top);
            _RecordButton.Click += _RecordButton_Click;

            if (_showStopTS)
            {
                _StopTSButton = MakeButton("Stop TS", "Stop TS: ends decoding and stops the demodulator - the tuner needs a new signal click afterwards", 0, top);
                _StopTSButton.Click += _StopTSButton_Click;
            }

            _parent.Controls.Add(_MuteButton);
            _parent.Controls.Add(_SnapshotButton);
            _parent.Controls.Add(_RecordButton);
            _parent.Controls.Add(_UDPStreamButton);
            if (_StopTSButton != null)
                _parent.Controls.Add(_StopTSButton);
        }

        private Button MakeButton(string text, string tooltip, int slotFromRight, int top)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = false,
                Height = buttonHeight,
                Width = buttonWidth,
                Top = top,
                Left = ButtonLeft(slotFromRight)
            };
            _toolTip.SetToolTip(button, tooltip);
            return button;
        }

        private int ButtonLeft(int slotFromRight)
        {
            // Without the Stop TS button (slot 0) every other button moves one slot to the right.
            if (!_showStopTS)
                slotFromRight--;
            return left_margin + _parent.Width - ((slotFromRight + 1) * (buttonWidth + button_gap));
        }

        private void _StopTSButton_Click(object sender, EventArgs e)
        {
            _buttonPressedCallback?.Invoke(_key, 4);
        }

        private void _UDPStreamButton_Click(object sender, EventArgs e)
        {
            _buttonPressedCallback?.Invoke(_key, 3);
        }

        private void _RecordButton_Click(object sender, EventArgs e)
        {
            _buttonPressedCallback?.Invoke(_key, 2);
        }

        private void _SnapshotButton_Click(object sender, EventArgs e)
        {
            _buttonPressedCallback?.Invoke(_key, 1);
        }

        private void _MuteButton_Click(object sender, EventArgs e)
        {
            _buttonPressedCallback?.Invoke(_key, 0);
        }

        protected virtual void _parent_Resize(object sender, EventArgs e)
        {
            _MuteButton.Left = ButtonLeft(4);
            _SnapshotButton.Left = ButtonLeft(3);
            _UDPStreamButton.Left = ButtonLeft(2);
            _RecordButton.Left = ButtonLeft(1);
            if (_StopTSButton != null)
                _StopTSButton.Left = ButtonLeft(0);
        }

        public override void UpdateColor(Color Col)
        {
            //UpdateColor(_valueLabel, Col);
        }

        public override void UpdateMuteButtonColor(Color Col)
        {
            _MuteButton.BackColor = Col;
        }

        public override void UpdateRecordButtonColor(Color Col)
        {
            _RecordButton.BackColor = Col;
        }

        public override void UpdateStreamButtonColor(Color Col)
        {
            _UDPStreamButton.BackColor = Col;
        }

        // Shown next to the button row for a few seconds (e.g. "Snapshot saved: ..."), not kept
        // like SetToolTip's hover text. Called on the UI thread (button Click), so no Invoke needed.
        public override void ShowNotice(string text)
        {
            _toolTip.Show(text, _parent, _MuteButton.Left, _MuteButton.Top - 20, 4000);
        }

        // The save path isn't known yet when the button is built (ConfigureMediaPath/
        // ConfigureTSRecorders run later than BuildSourceProperties) - appended to the hover
        // tooltip once it is.
        public void SetSnapshotTooltip(string path)
        {
            _toolTip.SetToolTip(_SnapshotButton, "Snapshot picture of this tuner to " + path);
        }

        public void SetRecordTooltip(string path)
        {
            _toolTip.SetToolTip(_RecordButton, "Start/stop recording the TS stream to " + path);
        }
    }
}
