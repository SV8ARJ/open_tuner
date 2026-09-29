using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using opentuner.MediaSources;
using System.Globalization;

namespace opentuner.Utilities
{
    public class StreamInfoContainer : Label
    {
        OTSourceData last_info_data = null;
        private Font font = new Font("Arial", 12, FontStyle.Bold);
        private readonly ToolTip _toolTip = new ToolTip { ShowAlways = true };
        private string _toolTipText = "";

        public StreamInfoContainer(bool show) 
        {
            BackColor = Color.Black;
            Dock = DockStyle.Top;
            DoubleBuffered = true;   // the line is repainted several times a second: no flicker

            //Height = 30;
            Visible = show;
        }

        public void UpdateInfo(OTSourceData info_data)
        {
            last_info_data = info_data;
            UpdateToolTip();
            Invalidate();
        }

        // Below this scale the text is clipped at the window edge instead of shrinking further.
        private const float MinScale = 0.6f;

        protected override void OnPaint(PaintEventArgs pe)
        {
            base.OnPaint(pe);

            ForeColor = Color.Black;

            Graphics g = pe.Graphics;

            SizeF temp_text_size = g.MeasureString("Temp", font);
            int wanted_height = (int)temp_text_size.Height + 10;
            if (Height != wanted_height)   // changing Height inside OnPaint re-triggers layout: only when needed
                Height = wanted_height;

            g.FillRectangle(Brushes.Black, 0, 0, Width, Height);
            g.DrawRectangle(Pens.White, 0, 0, Width - 1, Height - 1);

            if (last_info_data == null)
                return;

            // In a narrow window the right end (icons, U/R, TS error flags) used to be cut off:
            // measure first and shrink the whole line to fit, down to MinScale.
            float needed = DrawContent(g, false);
            float available = Width - 4;
            float scale = 1f;
            if (needed > available && needed > 0)
                scale = Math.Max(MinScale, available / needed);

            if (scale < 1f)
            {
                g.TranslateTransform(0, Height * (1f - scale) / 2f);   // keep it vertically centred
                g.ScaleTransform(scale, scale);
            }

            DrawContent(g, true);
        }

        // Draws the info line (or, with draw = false, only measures it) and returns its width.
        private float DrawContent(Graphics g, bool draw)
        {
            string info = "";

            string locked_info = (last_info_data.service_name.Length == 0 ? "Lock" : last_info_data.service_name) + " - " + " D" + last_info_data.db_margin.ToString("F1");

            if (last_info_data.demod_locked)
            {
                info = "MER: " + last_info_data.mer.ToString("F1") + " dB";
            }
            info = info + " - " + last_info_data.frequency.ToString("N0", CultureInfo.CurrentCulture);
            info = info + " - " + last_info_data.symbol_rate.ToString();
            if (last_info_data.demod_locked && !string.IsNullOrEmpty(last_info_data.modcode))
            {
                info = info + " - " + last_info_data.modcode;
            }

            float x_pos = 2;
            int y_pos = 5;

            if (last_info_data.demod_locked)
            {
                if (draw) g.DrawString(locked_info, font, Brushes.LimeGreen, new PointF(x_pos, y_pos));
                x_pos += g.MeasureString(locked_info, font).Width;
            }
            else
            {
                if (draw) g.DrawString("No Lock", font, Brushes.PaleVioletRed, new PointF(x_pos, y_pos));
                x_pos += g.MeasureString("No Lock", font).Width;
            }

            if (draw) g.DrawString(info, font, Brushes.AntiqueWhite, new PointF(x_pos, y_pos));
            x_pos += g.MeasureString(info, font).Width;

            using (Font emojiFont = new Font("Segoe UI Emoji", 12, FontStyle.Bold))
            {
                string audio_icon_mute = "\U0001F507";
                string audio_icon_high = "\uD83D\uDD0A";

                if (draw)
                {
                    if (last_info_data.volume > 0)
                        g.DrawString(audio_icon_high, emojiFont, Brushes.LimeGreen, new PointF(x_pos - 5, 2));
                    else
                        g.DrawString(audio_icon_mute, emojiFont, Brushes.IndianRed, new PointF(x_pos - 5, 2));
                }

                x_pos += g.MeasureString(audio_icon_mute, emojiFont).Width;
            }

            if (last_info_data.streaming)
            {
                if (draw) g.DrawString("U", font, Brushes.PaleTurquoise, new PointF(x_pos, y_pos));
                x_pos += g.MeasureString("U ", font).Width;
            }

            if (last_info_data.recording)
            {
                if (draw) g.DrawString("R", font, Brushes.PaleVioletRed, new PointF(x_pos, y_pos));
                x_pos += g.MeasureString("R ", font).Width;
            }

            // TS health flags (issue #19): only shown while errors occurred in the last few seconds
            x_pos = DrawFlag(g, draw, "CC", last_info_data.ts_cc_errors, true, Brushes.PaleVioletRed, x_pos, y_pos);
            x_pos = DrawFlag(g, draw, "TEI", last_info_data.ts_tei_errors, true, Brushes.Orange, x_pos, y_pos);
            x_pos = DrawFlag(g, draw, "SYNC", last_info_data.ts_sync_losses, true, Brushes.Orange, x_pos, y_pos);
            x_pos = DrawFlag(g, draw, "BUF", last_info_data.ts_buffer_overflows, true, Brushes.Orange, x_pos, y_pos);
            x_pos = DrawFlag(g, draw, "ENC", last_info_data.ts_scrambled, false, Brushes.Gold, x_pos, y_pos);   // no count: the number of packets says nothing

            return x_pos;
        }

        private float DrawFlag(Graphics g, bool draw, string name, uint count, bool showCount, Brush brush, float x_pos, int y_pos)
        {
            if (count == 0)
                return x_pos;

            string text = showCount ? name + " " + count.ToString() : name;
            if (draw) g.DrawString(text, font, brush, new PointF(x_pos, y_pos));
            return x_pos + g.MeasureString(text + " ", font).Width;
        }

        private void UpdateToolTip()
        {
            string text = "";

            if (last_info_data != null &&
                (last_info_data.ts_cc_errors > 0 || last_info_data.ts_tei_errors > 0 || last_info_data.ts_sync_losses > 0 ||
                 last_info_data.ts_buffer_overflows > 0 || last_info_data.ts_scrambled > 0))
            {
                text = "TS problems in the last " + TSHealth.WindowSeconds + " s:\n" +
                       "CC = continuity errors (TS packets lost)\n" +
                       "TEI = packets flagged uncorrectable by the demodulator\n" +
                       "SYNC = lost TS sync, bytes skipped\n" +
                       "BUF = a queue overflowed, bytes dropped (player/recorder/UDP too slow)\n" +
                       "ENC = scrambled (encrypted) packets";
            }

            // only touch the ToolTip when the text changed - this runs several times a second
            if (text != _toolTipText)
            {
                _toolTipText = text;
                _toolTip.SetToolTip(this, text);
            }
        }
    }
}
