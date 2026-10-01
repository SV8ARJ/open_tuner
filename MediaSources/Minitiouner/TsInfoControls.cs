using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace opentuner.MediaSources.Minitiouner
{
    // TS bit rate over time (kbit/s), one value per second, the newest on the right. Like MiniTioune's "TS bitrate" plot:
    // dark background with a green grid, yellow trace; the vertical scale is rounded to 100 kbit/s and follows the data
    // and the expected bit rate (dashed green line), so a drop of a few hundred kbit/s is visible. Used on the UI thread.
    public class BitrateChartControl : Control
    {
        private double[] _kbps = new double[0];   // NaN = no data in that second
        private double _expected_kbps = double.NaN;

        public BitrateChartControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Black;
            Height = 170;
        }

        // vertical range of the last paint, for the labels next to the chart
        public double ScaleMin { get; private set; }
        public double ScaleMax { get; private set; }

        public void SetData(double[] kbps, double expected_kbps)
        {
            _kbps = kbps ?? new double[0];
            _expected_kbps = expected_kbps;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);

            const int margin = 4;
            var plot = new RectangleF(margin, margin, Width - 2 * margin - 1, Height - 2 * margin - 1);
            if (plot.Width < 40 || plot.Height < 20)
                return;

            double min = double.MaxValue, max = double.MinValue;
            foreach (double v in _kbps)
            {
                if (double.IsNaN(v) || v <= 0)
                    continue;
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
            if (!double.IsNaN(_expected_kbps))
            {
                min = Math.Min(min, _expected_kbps);
                max = Math.Max(max, _expected_kbps);
            }
            if (min > max)   // no data at all
            {
                min = 0;
                max = 1000;
            }

            double pad = Math.Max(100, (max - min) * 0.25);
            double scale_min = Math.Max(0, Math.Floor((min - pad) / 100) * 100);
            double scale_max = Math.Ceiling((max + pad) / 100) * 100;
            ScaleMin = scale_min;
            ScaleMax = scale_max;

            using (var grid = new Pen(Color.FromArgb(0, 110, 0)))
            {
                for (int i = 0; i <= 4; i++)
                {
                    float y = plot.Top + plot.Height * i / 4f;
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                }
                for (int i = 0; i <= 8; i++)
                {
                    float x = plot.Left + plot.Width * i / 8f;
                    g.DrawLine(grid, x, plot.Top, x, plot.Bottom);
                }
            }

            Func<double, float> y_of = v => (float)(plot.Bottom - (v - scale_min) / (scale_max - scale_min) * plot.Height);

            if (!double.IsNaN(_expected_kbps))
            {
                using (var dash = new Pen(Color.LimeGreen) { DashStyle = DashStyle.Dash })
                    g.DrawLine(dash, plot.Left, y_of(_expected_kbps), plot.Right, y_of(_expected_kbps));
            }

            int count = _kbps.Length;
            if (count < 2)
                return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var trace = new Pen(Color.Yellow, 1.5f))
            {
                PointF? last = null;
                for (int i = 0; i < count; i++)
                {
                    double v = _kbps[i];
                    if (double.IsNaN(v))
                    {
                        last = null;
                        continue;
                    }

                    var point = new PointF(plot.Left + plot.Width * i / (count - 1f), y_of(Math.Max(scale_min, Math.Min(scale_max, v))));
                    if (last.HasValue)
                        g.DrawLine(trace, last.Value, point);
                    last = point;
                }
            }
        }
    }

    // Payload Diagram: pie chart of the TS packets by category (Video, Audio, Null Packet, Overhead) and, to the right of it,
    // a second pie chart of what the Overhead consists of (PAT, PMT, SDT/BAT, NIT, EIT, TDT/TOT, PCR, other). One legend
    // for both on the right. Used on the UI thread.
    public class PayloadPieControl : Control
    {
        private static readonly string[] Names = { "Video", "Audio", "Null Packet", "Overhead" };
        private static readonly Color[] Colors =
        {
            Color.FromArgb(70, 110, 180),    // video
            Color.FromArgb(240, 150, 60),    // audio
            Color.FromArgb(250, 205, 160),   // null packets
            Color.FromArgb(220, 80, 50),     // overhead
        };

        // the parts of the Overhead; what is not listed is "Other" (the last one)
        public static readonly string[] OverheadNames = { "PAT", "PMT", "SDT/BAT", "NIT", "EIT", "TDT/TOT", "PCR", "Other" };
        private static readonly Color[] OverheadColors =
        {
            Color.FromArgb(26, 170, 150),    // PAT
            Color.FromArgb(142, 68, 173),    // PMT
            Color.FromArgb(120, 170, 60),    // SDT/BAT
            Color.FromArgb(235, 190, 20),    // NIT
            Color.FromArgb(150, 100, 60),    // EIT
            Color.FromArgb(230, 130, 170),   // TDT/TOT
            Color.FromArgb(150, 30, 40),     // PCR
            Color.FromArgb(150, 150, 150),   // Other
        };

        private const int LegendWidth = 185;

        private readonly double[] _values = new double[4];
        private readonly double[] _overhead = new double[8];
        private readonly string[] _value_pids = new string[4];       // PID column of the legend, "" = none
        private readonly string[] _overhead_pids = new string[8];
        private readonly string[] _value_tips = new string[4];       // hover text of the legend rows
        private readonly string[] _overhead_tips = new string[8];
        private readonly List<KeyValuePair<Rectangle, string>> _legend_hits = new List<KeyValuePair<Rectangle, string>>();
        private readonly ToolTip _tooltip = new ToolTip();
        private string _shown_tip = null;
        private string _subtitle = "";

        public PayloadPieControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(235, 235, 235);
            Height = 250;
        }

        // overhead_parts: packets per entry of OverheadNames; value_pids / overhead_pids: what the legend shows in its PID column
        // (a PID number, "3 PIDs" or ""); value_tips / overhead_tips: the text shown when the mouse is over a legend row
        public void SetData(string subtitle, double video, double audio, double nulls, double overhead, double[] overhead_parts,
                            string[] value_pids, string[] overhead_pids, string[] value_tips, string[] overhead_tips)
        {
            _subtitle = subtitle ?? "";
            _values[0] = video;
            _values[1] = audio;
            _values[2] = nulls;
            _values[3] = overhead;
            for (int i = 0; i < _overhead.Length; i++)
                _overhead[i] = overhead_parts != null && i < overhead_parts.Length ? overhead_parts[i] : 0;
            for (int i = 0; i < _value_pids.Length; i++)
                _value_pids[i] = value_pids != null && i < value_pids.Length ? value_pids[i] ?? "" : "";
            for (int i = 0; i < _overhead_pids.Length; i++)
                _overhead_pids[i] = overhead_pids != null && i < overhead_pids.Length ? overhead_pids[i] ?? "" : "";
            for (int i = 0; i < _value_tips.Length; i++)
                _value_tips[i] = value_tips != null && i < value_tips.Length ? value_tips[i] ?? "" : "";
            for (int i = 0; i < _overhead_tips.Length; i++)
                _overhead_tips[i] = overhead_tips != null && i < overhead_tips.Length ? overhead_tips[i] ?? "" : "";
            Invalidate();
        }

        private static void DrawPie(Graphics g, Rectangle pie, double[] values, Color[] colors, Brush text_brush, Font text_font, string empty_text)
        {
            double total = 0;
            foreach (double v in values)
                total += v;

            if (total <= 0)
            {
                using (var empty = new Pen(Color.Gray))
                    g.DrawEllipse(empty, pie);
                using (var center = new StringFormat { Alignment = StringAlignment.Center })
                    g.DrawString(empty_text, text_font, text_brush, new RectangleF(pie.Left, pie.Top + pie.Height / 2 - 8, pie.Width, 16), center);
                return;
            }

            float angle = -90f;
            for (int i = 0; i < values.Length; i++)
            {
                float sweep = (float)(values[i] / total * 360.0);
                if (sweep <= 0)
                    continue;

                using (var fill = new SolidBrush(colors[i]))
                {
                    if (sweep >= 359.9f)
                        g.FillEllipse(fill, pie);
                    else
                        g.FillPie(fill, pie, angle, sweep);
                }
                angle += sweep;
            }

            using (var outline = new Pen(Color.FromArgb(90, 90, 90)))
                g.DrawEllipse(outline, pie);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            _legend_hits.Clear();

            using (var title_font = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold))
            using (var text_font = new Font("Microsoft Sans Serif", 8.5f))
            using (var bold_font = new Font("Microsoft Sans Serif", 8.5f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.Black))
            using (var center = new StringFormat { Alignment = StringAlignment.Center })
            {
                const int top = 46;
                int size = Math.Min(Height - top - 8, (Width - LegendWidth - 16) / 2 - 8);
                if (size < 40)
                    return;

                var pie1 = new Rectangle(8, top, size, size);
                var pie2 = new Rectangle(pie1.Right + 16, top, size, size);

                g.DrawString("Payload Diagram", title_font, brush, new RectangleF(pie1.Left - 6, 4, size + 12, 18), center);
                g.DrawString(_subtitle, title_font, brush, new RectangleF(pie1.Left - 6, 22, size + 12, 18), center);
                g.DrawString("Overhead", title_font, brush, new RectangleF(pie2.Left - 6, 4, size + 12, 18), center);

                DrawPie(g, pie1, _values, Colors, brush, text_font, "no data");
                DrawPie(g, pie2, _overhead, OverheadColors, brush, text_font, "none");

                // one legend for both: the four categories, below them the parts of the Overhead (only those that occur)
                int x = pie2.Right + 14;
                int y = 8;
                double total = _values[0] + _values[1] + _values[2] + _values[3];
                for (int i = 0; i < _values.Length; i++)
                    y = LegendRow(g, x, y, Colors[i], _value_pids[i], Names[i], _value_tips[i], total > 0 ? _values[i] / total * 100 : 0, text_font, brush);

                double overhead_total = 0;
                foreach (double v in _overhead)
                    overhead_total += v;

                if (overhead_total > 0)
                {
                    y += 6;
                    g.DrawString("Overhead consists of", bold_font, brush, x, y);
                    y += 18;
                    for (int i = 0; i < _overhead.Length; i++)
                    {
                        if (_overhead[i] > 0)
                            y = LegendRow(g, x, y, OverheadColors[i], _overhead_pids[i], OverheadNames[i], _overhead_tips[i], _overhead[i] / overhead_total * 100, text_font, brush);
                    }
                }
            }
        }

        // The text of the legend row under the mouse stays at the pointer; it only changes when another row is entered.
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            string tip = null;
            foreach (var hit in _legend_hits)
            {
                if (hit.Key.Contains(e.Location))
                {
                    tip = hit.Value;
                    break;
                }
            }

            if (tip == _shown_tip)
                return;

            _shown_tip = tip;
            if (string.IsNullOrEmpty(tip))
                _tooltip.Hide(this);
            else
                _tooltip.Show(tip, this, e.X + 16, e.Y + 22, 30000);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _shown_tip = null;
            _tooltip.Hide(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _tooltip.Dispose();
            base.Dispose(disposing);
        }

        // swatch, PID (right aligned, four digits wide), name, percentage (right aligned)
        private int LegendRow(Graphics g, int x, int y, Color color, string pid, string name, string tip, double percent, Font font, Brush brush)
        {
            _legend_hits.Add(new KeyValuePair<Rectangle, string>(new Rectangle(x, y, LegendWidth, 17), tip));

            using (var fill = new SolidBrush(color))
                g.FillRectangle(fill, x, y + 2, 11, 11);
            g.DrawRectangle(Pens.Gray, x, y + 2, 11, 11);

            using (var right = new StringFormat { Alignment = StringAlignment.Far })
            {
                g.DrawString(pid, font, brush, new RectangleF(x + 14, y, 40, 16), right);
                g.DrawString(percent.ToString("0.0") + " %", font, brush, new RectangleF(x + LegendWidth - 62, y, 52, 16), right);
            }
            g.DrawString(name, font, brush, x + 58, y);
            return y + 17;
        }
    }
}
