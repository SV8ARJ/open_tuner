using System;
using System.Drawing;
using System.Windows.Forms;

namespace opentuner.MediaSources.Minitiouner
{
    // The coefficients of the two equalizers of a demodulator (STV0910 EQUA_ACC = DFE, 8 taps, and FFE_ACC = FFE, 4 taps; every tap
    // is an I and a Q value, signed 8 bit), as bars around a centre line: I yellow, Q light blue. Like the green tile in the
    // MiniTioune Extra Panel it shows whether the equalizers work: all bars near zero = nothing to correct (or the equalizer is
    // switched off). Used on the UI thread; null = not locked, nothing to show.
    public class EqualizerControl : Control
    {
        private sbyte[] _dfe;   // I1, Q1, I2, Q2 ... (16 values)
        private sbyte[] _ffe;   // I1, Q1 ... (8 values)

        public EqualizerControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Black;
            Height = 72;
        }

        public void SetCoefficients(sbyte[] dfe, sbyte[] ffe)
        {
            _dfe = dfe;
            _ffe = ffe;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);

            sbyte[] dfe = _dfe;
            sbyte[] ffe = _ffe;

            using (var grid = new Pen(Color.FromArgb(0, 110, 0)))
            using (var label_brush = new SolidBrush(Color.FromArgb(150, 150, 150)))
            using (var font = new Font("Microsoft Sans Serif", 7f))
            {
                float middle = Height / 2f;
                g.DrawLine(grid, 0, middle, Width, middle);
                g.DrawLine(grid, 0, Height / 4f, Width, Height / 4f);
                g.DrawLine(grid, 0, Height * 3 / 4f, Width, Height * 3 / 4f);

                if (dfe == null || ffe == null)
                {
                    g.DrawString("not locked", font, label_brush, 4, 4);
                    return;
                }

                // 8 DFE taps, a gap, 4 FFE taps
                int taps = dfe.Length / 2 + ffe.Length / 2;
                float slot = (Width - 8f) / (taps + 1);   // + 1 for the gap
                float x = 4;

                DrawTaps(g, dfe, ref x, slot, middle);
                g.DrawString("DFE", font, label_brush, 4, Height - 12);

                float ffe_start = x + slot;
                x = ffe_start;
                DrawTaps(g, ffe, ref x, slot, middle);
                g.DrawString("FFE", font, label_brush, ffe_start, Height - 12);
            }
        }

        // two bars (I, Q) per tap; 127 reaches the border of the control
        private void DrawTaps(Graphics g, sbyte[] values, ref float x, float slot, float middle)
        {
            using (var i_brush = new SolidBrush(Color.Yellow))
            using (var q_brush = new SolidBrush(Color.FromArgb(120, 200, 255)))
            {
                float bar = Math.Max(2f, (slot - 3f) / 2f);

                for (int tap = 0; tap < values.Length / 2; tap++)
                {
                    DrawBar(g, i_brush, x, bar, values[tap * 2], middle);
                    DrawBar(g, q_brush, x + bar, bar, values[tap * 2 + 1], middle);
                    x += slot;
                }
            }
        }

        private void DrawBar(Graphics g, Brush brush, float x, float width, sbyte value, float middle)
        {
            float height = value / 128f * middle;
            if (height >= 0)
                g.FillRectangle(brush, x, middle - height, width, Math.Max(1f, height));
            else
                g.FillRectangle(brush, x, middle, width, Math.Max(1f, -height));
        }
    }
}
