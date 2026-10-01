using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using opentuner.Utilities;

namespace opentuner.MediaSources.Minitiouner
{
    // One tuner's group on the "TS Info" tab (issue #61), top to bottom: service and provider, TS bit rate over time
    // (with the expected and the chip's bit rate), Payload Diagram (pie), TS statistics and the TS error flags. The numbers
    // come from the TSAnalyzer / TSHealth of the tuner's TS parser, once per second.
    public class TsInfoTunerView
    {
        private const int PacketBits = TSParserThread.TS_PACKET_SIZE * 8;

        private readonly CustomGroupBox _group;
        private readonly Func<TSParserThread> _get_parser;
        private readonly Timer _timer = new Timer();

        private readonly FlowLayoutPanel _service_row = new FlowLayoutPanel();
        private readonly Label _service_value = new Label();    // the callsign, bold
        private readonly Label _provider_value = new Label();
        private readonly BitrateChartControl _chart = new BitrateChartControl();
        private readonly Label _scale_label = new Label();
        private readonly Label _bitrate_label = new Label();
        private readonly Label _expected_label = new Label();
        private readonly PayloadPieControl _pie = new PayloadPieControl();
        private readonly Label _null_label = new Label();
        private readonly Label _video_label = new Label();
        private readonly Label _audio_label = new Label();
        private readonly Label _overhead_label = new Label();
        private readonly Label _received_label = new Label();
        private readonly Label _chip_label = new Label();
        private readonly Label _error_label = new Label();

        private string _service_name = "";
        private string _service_provider = "";
        private volatile float _expected_kbps = float.NaN;
        private volatile float _chip_kbps = float.NaN;

        public void SetHighlight(Color color)
        {
            _group.HighlightColor = color;
        }

        // get_parser: the TS parser of this tuner, null as long as there is none (the tab is built before the TS threads start)
        public TsInfoTunerView(string title, Control parent, Func<TSParserThread> get_parser)
        {
            _get_parser = get_parser;

            _group = new CustomGroupBox();
            _group.Dock = DockStyle.Top;
            _group.Height = 700; // starting value, FitHeight() sets it from the content
            _group.Text = title;
            _group.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, (byte)0);
            _group.Padding = new Padding(8, 7, 8, 8);

            var tips = new ToolTip();
            tips.ShowAlways = true;

            // Docking is evaluated last-added first, so the controls are added bottom to top.
            ConfigureValueLabel(_error_label, DockStyle.Top, "TS err:  -");
            tips.SetToolTip(_error_label, "TS errors of the last 10 s: CC = continuity counter jumped (packets lost), TEI = packet flagged uncorrectable by the demodulator, SYNC = TS sync lost, BUF = a consumer was too slow and bytes were dropped, ENC = scrambled packets");
            _group.Controls.Add(_error_label);

            _group.Controls.Add(TitleLabel("TS err"));

            var stat = new TableLayoutPanel();
            stat.Dock = DockStyle.Top;
            stat.RowCount = 3;
            stat.ColumnCount = 2;
            stat.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            stat.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int row = 0; row < 3; row++)
                stat.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            stat.Height = 3 * 24 + 4;
            foreach (var label in new[] { _null_label, _overhead_label, _video_label, _audio_label, _received_label, _chip_label })
                ConfigureValueLabel(label, DockStyle.Fill, "");
            _null_label.Text = "Null Packets:  -";
            _overhead_label.Text = "Overhead:  -";
            _video_label.Text = "Video:  -";
            _audio_label.Text = "Audio:  -";
            _received_label.Text = "data rcvd:  -";
            _chip_label.Text = "chip TS bitrate:  -";
            tips.SetToolTip(_received_label, "TS bit rate counted from the received packets of the last 10 s (including null packets)");
            tips.SetToolTip(_chip_label, "TSBITRATE register of the demodulator: bit rate of the stream leaving the packet delineator");
            stat.Controls.Add(_null_label, 0, 0);
            stat.Controls.Add(_overhead_label, 1, 0);
            stat.Controls.Add(_video_label, 0, 1);
            stat.Controls.Add(_audio_label, 1, 1);
            stat.Controls.Add(_received_label, 0, 2);
            stat.Controls.Add(_chip_label, 1, 2);
            _group.Controls.Add(stat);

            var stat_header = new Panel();
            stat_header.Dock = DockStyle.Top;
            stat_header.Height = 28;
            var reset_stat = new Button { Text = "reset %", Width = 80, Height = 24, Dock = DockStyle.Right };
            reset_stat.Click += (s, e) => _get_parser()?.Analyzer.ResetStats();
            tips.SetToolTip(reset_stat, "Payload Diagram and statistics show the last 10 s; this starts them again from zero");
            stat_header.Controls.Add(reset_stat);
            var stat_title = TitleLabel("TS stat");
            stat_title.Dock = DockStyle.Fill;
            stat_header.Controls.Add(stat_title);
            stat_title.BringToFront();
            _group.Controls.Add(stat_header);

            _pie.Dock = DockStyle.Top;
            _group.Controls.Add(_pie);

            var values = new TableLayoutPanel();
            values.Dock = DockStyle.Top;
            values.Height = 26;
            values.RowCount = 1;
            values.ColumnCount = 2;
            values.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            values.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            values.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            ConfigureValueLabel(_bitrate_label, DockStyle.Fill, "TS bitrate:  -");
            ConfigureValueLabel(_expected_label, DockStyle.Fill, "Bitrate expected:  -");
            tips.SetToolTip(_bitrate_label, "Bit rate of the received TS (all packets including null packets), average of the last 3 s");
            tips.SetToolTip(_expected_label, "The bit rate the signal can carry at most, from symbol rate, modulation, code rate, frame size and pilots (DVB-S2: Rs * (Kbch - 80) / symbols per frame, DVB-S: Rs * 2 * rate * 188/204)");
            values.Controls.Add(_bitrate_label, 0, 0);
            values.Controls.Add(_expected_label, 1, 0);
            _group.Controls.Add(values);

            _scale_label.Dock = DockStyle.Top;
            _scale_label.Height = 18;
            _scale_label.Font = new Font("Microsoft Sans Serif", 8f);
            _scale_label.Text = "";
            _scale_label.TextAlign = ContentAlignment.MiddleLeft;
            _group.Controls.Add(_scale_label);

            _chart.Dock = DockStyle.Top;
            _group.Controls.Add(_chart);

            var bitrate_header = new Panel();
            bitrate_header.Dock = DockStyle.Top;
            bitrate_header.Height = 28;
            var reset_ts = new Button { Text = "reset TS", Width = 80, Height = 24, Dock = DockStyle.Right };
            reset_ts.Click += (s, e) => { _get_parser()?.Analyzer.ResetHistory(); UpdateView(); };
            tips.SetToolTip(reset_ts, "Clears the bit rate chart");
            bitrate_header.Controls.Add(reset_ts);
            var bitrate_title = TitleLabel("TS bitrate [kbit/s], last " + TSAnalyzer.HistorySeconds + " s");
            bitrate_title.Dock = DockStyle.Fill;
            bitrate_header.Controls.Add(bitrate_title);
            bitrate_title.BringToFront();
            _group.Controls.Add(bitrate_header);

            _service_row.Dock = DockStyle.Top;
            _service_row.Height = 26;
            _service_row.WrapContents = false;
            _service_row.Padding = new Padding(0, 3, 0, 0);
            Label service_caption = ServiceLabel("Service:", FontStyle.Regular);
            Label provider_caption = ServiceLabel("Provider:", FontStyle.Regular);
            provider_caption.Margin = new Padding(16, 0, 3, 0);
            _service_row.Controls.Add(service_caption);
            _service_row.Controls.Add(ServiceLabel("", FontStyle.Regular, _service_value, bold: true));
            _service_row.Controls.Add(provider_caption);
            _service_row.Controls.Add(ServiceLabel("", FontStyle.Regular, _provider_value));
            _service_value.Text = "-";
            _provider_value.Text = "-";
            _group.Controls.Add(_service_row);

            _group.SizeChanged += (s, e) => FitHeight();
            parent.Controls.Add(_group);
            _group.BringToFront(); // stacks the groups top to bottom in creation order
            FitHeight();

            _timer.Interval = 1000;
            _timer.Tick += (s, e) => { if (_group.Visible) UpdateView(); };
            _timer.Start();
            _group.Disposed += (s, e) => _timer.Dispose();
        }

        // label of the service row: autosized, so the callsign (bold) and the provider follow each other
        private Label ServiceLabel(string text, FontStyle style, Label label = null, bool bold = false)
        {
            label = label ?? new Label();
            label.AutoSize = true;
            label.Margin = new Padding(0, 0, 3, 0);
            label.Font = new Font("Microsoft Sans Serif", 9f, bold ? FontStyle.Bold : style);
            label.Text = text;
            return label;
        }

        private Label TitleLabel(string text)
        {
            var label = new Label();
            label.Dock = DockStyle.Top;
            label.Height = 24;
            label.Text = text;
            label.Font = new Font(_group.Font, FontStyle.Bold);
            label.TextAlign = ContentAlignment.BottomLeft;
            return label;
        }

        // Dock Fill for the labels inside a table, Top for the ones that stand on their own
        private static void ConfigureValueLabel(Label label, DockStyle dock, string text)
        {
            label.Dock = dock;
            if (dock == DockStyle.Top)
                label.Height = 26;
            label.Margin = new Padding(0);
            label.Font = new Font("Microsoft Sans Serif", 8.5f);
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
        }

        private void FitHeight()
        {
            int height = _group.Padding.Vertical + 24;
            foreach (Control child in _group.Controls)
            {
                if (child.Dock == DockStyle.Top)
                    height += child.Height;
            }

            if (_group.Height != height)
                _group.Height = height;
        }

        // From the SDT callback (any thread).
        public void SetService(string name, string provider)
        {
            _service_name = name ?? "";
            _service_provider = provider ?? "";
        }

        // Called with every status of the tuner: expected_kbps (NaN = unknown) and the chip's bit rate in kbit/s (NaN = not locked).
        public void SetSignal(double expected_kbps, double chip_kbps)
        {
            _expected_kbps = (float)expected_kbps;
            _chip_kbps = (float)chip_kbps;
        }

        private static string Kbps(double kbps)
        {
            return double.IsNaN(kbps) ? "-" : kbps.ToString("N3") + " kb/s";
        }

        // Name and type of a PID: the fixed DVB PIDs, the PMT PIDs of the PAT and the streams of the PMTs.
        private static void DescribePid(TSPidInfo pid, out string name, out string type)
        {
            switch (pid.Pid)
            {
                case 0x0000: name = "PAT"; type = ""; return;
                case 0x0001: name = "CAT"; type = ""; return;
                case 0x0002: name = "TSDT"; type = ""; return;
                case 0x0010: name = "NIT"; type = ""; return;
                case 0x0011: name = "SDT/BAT"; type = ""; return;
                case 0x0012: name = "EIT"; type = ""; return;
                case 0x0013: name = "RST"; type = ""; return;
                case 0x0014: name = "TDT/TOT"; type = ""; return;
                case 0x1FFF: name = "Null Packets"; type = "(stuffing)"; return;
            }

            if (pid.IsPmt)
            {
                name = "PMT";
                type = "";
                return;
            }

            if (pid.IsPcr && pid.StreamType == -1)
            {
                name = "PCR";
                type = "(clock reference only)";
                return;
            }

            switch (pid.StreamType)
            {
                case 0x01: name = "Video"; type = "MPEG-1"; return;
                case 0x02: name = "Video"; type = "MPEG-2"; return;
                case 0x10: name = "Video"; type = "MPEG-4"; return;
                case 0x1B: name = "Video"; type = "H.264"; return;
                case 0x24: name = "Video"; type = "H.265"; return;
                case 0x03: name = "Audio"; type = "MPEG-1"; return;
                case 0x04: name = "Audio"; type = "MPEG-2"; return;
                case 0x0F: name = "Audio"; type = "AAC"; return;
                case 0x11: name = "Audio"; type = "AAC LATM"; return;
                case 0x81: name = "Audio"; type = "AC-3"; return;
                case 0x87: name = "Audio"; type = "E-AC-3"; return;
                case 0x06: name = "Data"; type = "private data"; return;
                case -1: name = "Unknown"; type = "(not in a PMT)"; return;
                default: name = "Data"; type = "type 0x" + pid.StreamType.ToString("X2"); return;
            }
        }

        // "256" for one PID, "3 PIDs" for several, "" for none
        private static string PidText(List<TSPidInfo> pids)
        {
            return pids.Count == 0 ? "" : pids.Count == 1 ? pids[0].Pid.ToString() : pids.Count + " PIDs";
        }

        // Hover text of a legend row: one line per PID with its number, name, type and the decoded content of its table.
        private static string PidTip(List<TSPidInfo> pids)
        {
            const int MaxLines = 12;
            var text = new System.Text.StringBuilder();

            for (int i = 0; i < pids.Count; i++)
            {
                if (i == MaxLines)
                {
                    text.Append("... " + (pids.Count - MaxLines) + " more");
                    break;
                }

                DescribePid(pids[i], out string name, out string type);
                if (i > 0)
                    text.Append('\n');

                text.Append(pids[i].Pid.ToString().PadLeft(4) + " (0x" + pids[i].Pid.ToString("X4") + ")  " + name + (type == "" ? "" : " " + type));
                if (!string.IsNullOrEmpty(pids[i].Info))
                    text.Append(": " + pids[i].Info);
            }

            return text.ToString();
        }

        // Fills the Payload Diagram: packets of the Overhead per part (PAT, PMT, ...), the PIDs shown in the legend and the
        // hover text of its rows.
        private void UpdatePie(TSAnalysis analysis)
        {
            string[] names = PayloadPieControl.OverheadNames;
            var parts = new double[names.Length];
            var overhead_pids = new List<TSPidInfo>[names.Length];
            var category_pids = new List<TSPidInfo>[3];   // video, audio, null packets (the Overhead has none of its own)
            for (int i = 0; i < overhead_pids.Length; i++) overhead_pids[i] = new List<TSPidInfo>();
            for (int i = 0; i < category_pids.Length; i++) category_pids[i] = new List<TSPidInfo>();

            // the PIDs are sorted by PID, so the list of a part is too
            foreach (TSPidInfo pid in analysis.Pids ?? new TSPidInfo[0])
            {
                if (pid.Category >= 1 && pid.Category <= 3)
                {
                    category_pids[pid.Category - 1].Add(pid);
                    continue;
                }

                DescribePid(pid, out string name, out _);
                int index = Array.IndexOf(names, name);
                if (index < 0)
                    index = names.Length - 1;

                parts[index] += pid.Packets;
                overhead_pids[index].Add(pid);
            }

            var value_pids = new[] { PidText(category_pids[0]), PidText(category_pids[1]), PidText(category_pids[2]), "" };
            var value_tips = new[]
            {
                PidTip(category_pids[0]), PidTip(category_pids[1]), PidTip(category_pids[2]),
                "Everything that is neither video, audio nor a null packet:\nthe tables (PAT, PMT, SDT, ...), the PCR and other PIDs, see the second pie chart",
            };

            _pie.SetData(_service_name, analysis.Video, analysis.Audio, analysis.Null, analysis.Overhead, parts,
                         value_pids, overhead_pids.Select(PidText).ToArray(), value_tips, overhead_pids.Select(PidTip).ToArray());
        }

        private void UpdateView()
        {
            TSParserThread parser = _get_parser();
            if (parser == null)
                return;

            TSAnalysis analysis = parser.Analyzer.Get();

            // bit rate per second; the seconds before the first packet have no value (not 0)
            uint[] packets = analysis.PacketsPerSecond;
            var kbps = new double[packets.Length];
            bool started = false;
            for (int i = 0; i < packets.Length; i++)
            {
                started |= packets[i] > 0;
                kbps[i] = started ? packets[i] * (double)PacketBits / 1000.0 : double.NaN;
            }

            // no TS packet in the last 3 complete seconds: the station is gone, service, provider and the statistics are cleared
            bool no_ts = true;
            for (int i = Math.Max(0, packets.Length - 3); i < packets.Length; i++)
                no_ts &= packets[i] == 0;

            if (no_ts)
            {
                _service_name = "";
                _service_provider = "";
                analysis.Video = analysis.Audio = analysis.Null = analysis.Overhead = 0;
                analysis.Pids = new TSPidInfo[0];
            }

            double expected = _expected_kbps;
            _chart.SetData(kbps, expected);
            _scale_label.Text = "scale " + _chart.ScaleMin.ToString("0") + " ... " + _chart.ScaleMax.ToString("0") + " kb/s" +
                                (double.IsNaN(expected) ? "" : "   (dashed: expected)");

            // average of the last 3 complete seconds
            double sum = 0;
            int n = 0;
            for (int i = kbps.Length - 3; i < kbps.Length; i++)
            {
                if (i >= 0 && !double.IsNaN(kbps[i]))
                {
                    sum += kbps[i];
                    n++;
                }
            }
            _bitrate_label.Text = "TS bitrate:  " + Kbps(n > 0 ? sum / n : double.NaN);
            _expected_label.Text = "Bitrate expected:  " + Kbps(expected);

            _service_value.Text = _service_name == "" ? "-" : _service_name;
            _provider_value.Text = _service_provider == "" ? "-" : _service_provider;

            // sums of the last TSAnalyzer.StatWindowSeconds seconds
            long total = analysis.Total;
            double seconds = Math.Max(1, analysis.Seconds);
            UpdatePie(analysis);

            if (total > 0)
            {
                Func<long, string> text = count =>
                    (count * (double)PacketBits / seconds / 1000.0).ToString("N0") + " kb/s  " + (count * 100.0 / total).ToString("0.0") + " %";

                _null_label.Text = "Null Packets:  " + (analysis.Null * 100.0 / total).ToString("0.0") + " %";
                _overhead_label.Text = "Overhead:  " + text(analysis.Overhead);
                _video_label.Text = "Video:  " + text(analysis.Video);
                _audio_label.Text = "Audio:  " + text(analysis.Audio);
                _received_label.Text = "data rcvd:  " + Kbps(total * (double)PacketBits / seconds / 1000.0);
            }
            else
            {
                _null_label.Text = "Null Packets:  -";
                _overhead_label.Text = "Overhead:  -";
                _video_label.Text = "Video:  -";
                _audio_label.Text = "Audio:  -";
                _received_label.Text = "data rcvd:  -";
            }

            _chip_label.Text = "chip TS bitrate:  " + Kbps(_chip_kbps);

            string errors = TSHealth.Describe(parser.Health.Get());
            _error_label.Text = "TS err:  " + (errors == "" ? "none" : errors);
        }
    }
}
