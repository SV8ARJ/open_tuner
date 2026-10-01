using System;
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

    // Payload Diagram: pie chart of the TS packets by category, with title, service name and a legend with the
    // percentages on the right. Used on the UI thread.
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

        private readonly double[] _values = new double[4];
        private string _title = "Payload Diagram";
        private string _subtitle = "";

        public PayloadPieControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(235, 235, 235);
            Height = 230;
        }

        public void SetData(string subtitle, double video, double audio, double nulls, double overhead)
        {
            _subtitle = subtitle ?? "";
            _values[0] = video;
            _values[1] = audio;
            _values[2] = nulls;
            _values[3] = overhead;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var title_font = new Font("Microsoft Sans Serif", 11f, FontStyle.Bold))
            using (var text_font = new Font("Microsoft Sans Serif", 9f))
            using (var brush = new SolidBrush(Color.Black))
            using (var center = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString(_title, title_font, brush, new RectangleF(0, 4, Width, 20), center);
                g.DrawString(_subtitle, title_font, brush, new RectangleF(0, 24, Width, 20), center);

                double total = _values[0] + _values[1] + _values[2] + _values[3];
                int top = 50;
                int size = Math.Min(Height - top - 8, Width / 2);
                if (size < 40)
                    return;

                var pie = new Rectangle(10, top, size, size);

                if (total <= 0)
                {
                    using (var empty = new Pen(Color.Gray))
                        g.DrawEllipse(empty, pie);
                    g.DrawString("no data", text_font, brush, new RectangleF(pie.Left, pie.Top + size / 2 - 8, size, 16), center);
                    return;
                }

                float angle = -90f;
                for (int i = 0; i < _values.Length; i++)
                {
                    float sweep = (float)(_values[i] / total * 360.0);
                    if (sweep <= 0)
                        continue;

                    using (var fill = new SolidBrush(Colors[i]))
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

                // legend: colour, name, percentage
                int x = pie.Right + 20;
                int y = top + 10;
                for (int i = 0; i < _values.Length; i++)
                {
                    using (var fill = new SolidBrush(Colors[i]))
                        g.FillRectangle(fill, x, y + 2, 12, 12);
                    g.DrawRectangle(Pens.Gray, x, y + 2, 12, 12);
                    g.DrawString(Names[i] + "  " + (_values[i] / total * 100).ToString("0.0") + " %", text_font, brush, x + 18, y);
                    y += 22;
                }
            }
        }
    }
}
