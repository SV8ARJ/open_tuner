using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using opentuner.MediaSources;
using opentuner.MediaSources.Minitiouner;

namespace opentuner
{
    // Read-only overview of what OpenTuner sees on the USB side (FTDI devices with the role it would give
    // each channel) and what the connected source reports about its hardware. "Copy" puts the same text
    // on the clipboard, e.g. to attach it to an issue.
    // (Not to be confused with the old, unused hardwareInfoForm.)
    public class HardwareOverviewForm : Form
    {
        private readonly OTSource _source;
        private readonly ListView _devices = new ListView();
        private readonly TextBox _sourceInfo = new TextBox();
        private readonly Label _summary = new Label();

        private List<FtdiDeviceInfo> _device_list = new List<FtdiDeviceInfo>();
        private string _enumerate_error;
        private List<KeyValuePair<string, string>> _source_list;

        // source: the currently connected source, null if none
        public HardwareOverviewForm(OTSource source)
        {
            _source = source;

            Text = "Hardware Info";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(980, 560);
            MinimumSize = new Size(700, 400);
            ShowInTaskbar = false;

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Bottom;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Height = 44;
            buttons.Padding = new Padding(8);

            var close = new Button { Text = "Close", Width = 90, DialogResult = DialogResult.Cancel };
            var copy = new Button { Text = "Copy to clipboard", Width = 130 };
            var refresh = new Button { Text = "Refresh", Width = 90 };
            copy.Click += (s, e) => CopyToClipboard();
            refresh.Click += (s, e) => Reload();
            buttons.Controls.Add(close);
            buttons.Controls.Add(copy);
            buttons.Controls.Add(refresh);
            CancelButton = close;

            _sourceInfo.Multiline = true;
            _sourceInfo.ReadOnly = true;
            _sourceInfo.ScrollBars = ScrollBars.Vertical;
            _sourceInfo.Font = new Font(FontFamily.GenericMonospace, 9f);
            _sourceInfo.Dock = DockStyle.Fill;

            var sourceLabel = new Label { Text = "Connected source", Dock = DockStyle.Top, Height = 24, Padding = new Padding(6, 6, 0, 0) };

            _devices.Dock = DockStyle.Top;
            _devices.Height = 260;
            _devices.View = View.Details;
            _devices.FullRowSelect = true;
            _devices.GridLines = true;
            _devices.HideSelection = false;
            _devices.Columns.Add("#", 35);
            _devices.Columns.Add("Type", 120);
            _devices.Columns.Add("Description", 230);
            _devices.Columns.Add("Serial", 120);
            _devices.Columns.Add("Role", 55);
            _devices.Columns.Add("Board", 170);
            _devices.Columns.Add("State", 70);
            _devices.Columns.Add("LocId", 90);

            _summary.Dock = DockStyle.Top;
            _summary.Height = 24;
            _summary.Padding = new Padding(6, 6, 0, 0);

            // Fill first, then the docked edges (the last one added is laid out first)
            Controls.Add(_sourceInfo);
            Controls.Add(sourceLabel);
            Controls.Add(_devices);
            Controls.Add(_summary);
            Controls.Add(buttons);

            Load += (s, e) => Reload();
        }

        private void Reload()
        {
            _device_list = HardwareInfo.EnumerateFtdiDevices(out _enumerate_error);
            _source_list = _source?.GetHardwareInfo();

            _devices.BeginUpdate();
            _devices.Items.Clear();
            foreach (var d in _device_list)
            {
                var item = new ListViewItem(d.Index.ToString());
                item.SubItems.Add(d.Type);
                item.SubItems.Add(d.Description);
                item.SubItems.Add(d.Serial);
                item.SubItems.Add(d.Role);
                item.SubItems.Add(d.Board);
                item.SubItems.Add(d.IsOpen ? "open" : "free");
                item.SubItems.Add("0x" + d.LocId.ToString("X"));
                if (d.Role == "-")
                    item.ForeColor = SystemColors.GrayText;
                _devices.Items.Add(item);
            }
            _devices.EndUpdate();

            int used = 0;
            foreach (var d in _device_list)
                if (d.Role != "-") used++;

            _summary.Text = _enumerate_error != null
                ? "FTDI devices: " + _enumerate_error
                : "FTDI devices (as listed by the USB driver): " + _device_list.Count + ", MiniTiouner channels: " + used
                  + "   (\"open\" = in use by a program, e.g. OpenTuner itself; gray = not a MiniTiouner chip, skipped)";

            var text = new StringBuilder();
            if (_source_list == null)
            {
                text.AppendLine(_source == null ? "Not connected." : "This source has no hardware details.");
            }
            else
            {
                foreach (var kv in _source_list)
                    text.AppendLine(kv.Key.PadRight(28) + kv.Value);
            }
            _sourceInfo.Text = text.ToString();
            _sourceInfo.SelectionStart = 0;
        }

        private string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("OpenTuner hardware info " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            if (Owner != null)
                sb.AppendLine("Window title: " + Owner.Text);
            sb.AppendLine();
            sb.AppendLine("FTDI devices (" + _device_list.Count + ")");
            if (_enumerate_error != null)
                sb.AppendLine("  error: " + _enumerate_error);

            sb.AppendLine("  " + "#".PadRight(3) + "Type".PadRight(20) + "Description".PadRight(32) + "Serial".PadRight(14) + "Role".PadRight(6) + "Board".PadRight(22) + "State".PadRight(7) + "LocId");
            foreach (var d in _device_list)
            {
                sb.AppendLine("  " + d.Index.ToString().PadRight(3) + d.Type.PadRight(20) + d.Description.PadRight(32) + d.Serial.PadRight(14)
                    + d.Role.PadRight(6) + d.Board.PadRight(22) + (d.IsOpen ? "open" : "free").PadRight(7) + "0x" + d.LocId.ToString("X"));
            }

            sb.AppendLine();
            sb.AppendLine("Connected source");
            if (_source_list == null)
            {
                sb.AppendLine("  " + (_source == null ? "not connected" : "no hardware details"));
            }
            else
            {
                foreach (var kv in _source_list)
                    sb.AppendLine("  " + kv.Key.PadRight(28) + kv.Value);
            }

            return sb.ToString();
        }

        private void CopyToClipboard()
        {
            try
            {
                Clipboard.SetText(BuildReport());
                _summary.Text = "Copied to the clipboard.";
            }
            catch (Exception ex)
            {
                // the clipboard can be locked by another program for a moment
                _summary.Text = "Copy failed: " + ex.Message;
            }
        }
    }
}
