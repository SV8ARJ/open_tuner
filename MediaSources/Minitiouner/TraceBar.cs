using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace opentuner.MediaSources.Minitiouner
{
    // Small scrolling trace with a title and the current value, used for the noise level of one demodulator.
    // Every SetValue() adds one sample (one status poll), the newest is on the right. The vertical scale follows
    // the minimum and maximum of the samples on show (at least MinSpan wide), so small movements of a heavily
    // filtered value stay visible. Values are set from any thread.
    public class TraceBar : Control
    {
        private const int MaxSamples = 120; // about 30 s at one status poll every 250 ms
        private const double MinSpan = 1.0;  // smallest vertical range, in the unit of the value

        private readonly string _title;
        private readonly string _unit;
        private readonly List<double> _samples = new List<double>();
        private readonly object _lock = new object();
        private bool _valid = false;

        public TraceBar(string title, string unit)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _title = title;
            _unit = unit;
            Height = 44;
            BackColor = Color.FromArgb(210, 240, 245);
        }

        public void SetValue(bool valid, double value)
        {
            lock (_lock)
            {
                if (!valid)
                {
                    _samples.Clear();
                }
                else
                {
                    _samples.Add(value);
                    if (_samples.Count > MaxSamples)
                        _samples.RemoveAt(0);
                }

                _valid = valid;
            }

            if (!IsHandleCreated || IsDisposed)
                return;

            try
            {
                BeginInvoke((MethodInvoker)Invalidate);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ObjectDisposedException)
            {
            }
        }

        // In a narrow control the title and the value would run into each other: then the value is written on a second
        // line, right aligned, and the bar / plot moves down; PreferredHeight tells the row that holds the bars.
        private bool _wrapped = false;
        public int PreferredHeight => _wrapped ? 58 : 44;
        public event EventHandler PreferredHeightChanged;

        private void UpdateWrap(Graphics g, Font title_font, Font text_font, float available, string widest_value)
        {
            bool wrapped = g.MeasureString(_title, title_font).Width + g.MeasureString(widest_value, text_font).Width + 8 > available;
            if (wrapped == _wrapped)
                return;

            _wrapped = wrapped;
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke((MethodInvoker)(() => PreferredHeightChanged?.Invoke(this, EventArgs.Empty)));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);

            const int margin = 6;
            int plot_top = _wrapped ? 32 : 18;
            float x0 = margin;
            float x1 = Width - margin;
            float plot_height = Height - plot_top - 3;
            if (x1 - x0 < 40 || plot_height < 10)
                return;

            double[] samples;
            bool valid;
            lock (_lock)
            {
                samples = _samples.ToArray();
                valid = _valid;
            }

            using (var title_font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold))
            using (var text_font = new Font("Microsoft Sans Serif", 8f))
            using (var brush = new SolidBrush(Color.Black))
            using (var right = new StringFormat { Alignment = StringAlignment.Far })
            {
                UpdateWrap(g, title_font, text_font, x1 - x0, "99.9" + _unit + "  (99.9 - 99.9)");
                g.DrawString(_title, title_font, brush, x0, 2);

                using (var back = new SolidBrush(Color.White))
                using (var border = new Pen(Color.FromArgb(90, 90, 90)))
                {
                    g.FillRectangle(back, x0, plot_top, x1 - x0, plot_height);
                    g.DrawRectangle(border, x0, plot_top, x1 - x0, plot_height);
                }

                if (!valid || samples.Length == 0)
                {
                    g.DrawString("-", text_font, brush, new RectangleF(x0, 2, x1 - x0, 14), right);
                    return;
                }

                double min = double.MaxValue, max = double.MinValue;
                foreach (double s in samples)
                {
                    min = Math.Min(min, s);
                    max = Math.Max(max, s);
                }

                double span = Math.Max(MinSpan, (max - min) * 1.2);
                double centre = (min + max) / 2;
                double low = centre - span / 2;

                float ToX(int i) => x0 + 1 + (float)((x1 - x0 - 2) * (i + MaxSamples - samples.Length) / (double)(MaxSamples - 1));
                float ToY(double v) => (float)(plot_top + plot_height - 1 - (v - low) / span * (plot_height - 2));

                if (samples.Length > 1)
                {
                    var points = new PointF[samples.Length];
                    for (int i = 0; i < samples.Length; i++)
                        points[i] = new PointF(ToX(i), ToY(samples[i]));

                    using (var pen = new Pen(Color.FromArgb(70, 130, 180), 1.5f))
                        g.DrawLines(pen, points);
                }

                using (var dot = new SolidBrush(Color.Red))
                    g.FillEllipse(dot, ToX(samples.Length - 1) - 2, ToY(samples[samples.Length - 1]) - 2, 4, 4);

                g.DrawString(samples[samples.Length - 1].ToString("N1") + _unit + "  (" + min.ToString("N1") + " - " + max.ToString("N1") + ")",
                             text_font, brush, new RectangleF(x0, _wrapped ? 16 : 2, x1 - x0, 14), right);
            }
        }
    }
}
