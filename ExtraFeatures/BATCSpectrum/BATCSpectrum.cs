using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static opentuner.signal;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Diagnostics;
using System.IO;
using System.Drawing.Drawing2D;
using Serilog;

namespace opentuner.ExtraFeatures.BATCSpectrum
{
    public class BATCSpectrum
    {
        public delegate void SignalSelected(int Receiver, uint Freq, uint SymbolRate);

        public event SignalSelected OnSignalSelected;

        // right click in the signal area: tuner of the clicked row, frequency in kHz (centre of the signal under the
        // mouse, else the frequency of the mouse position) and symbol rate of that signal (kS, 0 if there is none)
        public event SignalSelected OnSpectrumRightClick;

        private static readonly Object list_lock = new Object();

        static int height = 255;    //makes things easier
        static int bandplan_height = 30;

        Bitmap bmp;
        static Bitmap bmp2;
        Pen greyPen = new Pen(Color.FromArgb(200, 123, 123, 123));
        Pen greyPen2 = new Pen(Color.FromArgb(200, 123, 123, 123));
        SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(128, Color.Gray));
        SolidBrush bandplanBrush = new SolidBrush(Color.FromArgb(180, 250, 250, 255));
        SolidBrush overpowerBrush = new SolidBrush(Color.FromArgb(128, Color.Red));

        SolidBrush tuner1Brush = new SolidBrush(Color.FromArgb(50, Color.Blue));
        SolidBrush tuner2Brush = new SolidBrush(Color.FromArgb(50, Color.Green));

        Graphics tmp;
        Graphics tmp2;

        int[,] rx_blocks = new int[4, 3];

        double start_freq = 10490.5f;

        XElement bandplan;
        Rectangle[] channels;
        IList<XElement> indexedbandplan;
        string InfoText;
        string TX_Text;  //dh3cs

        List<string> blocks = new List<string>();

        socket web_socket;
        signal sigs;

        int num_rxs_to_scan = 1;

        private PictureBox _spectrum;
        private int _tuners;

        Timer SpectrumTuneTimer;
        Timer websocketTimer;

        private int _autoTuneMode = 0;

        int connect_retries = 5;
        int connect_retry_count = 0;
        int slow_retry_seconds = 15;
        DateTime last_slow_retry = DateTime.MinValue;

        public void updateSignalCallsign(string callsign, double freq, float sr)
        {
            sigs.updateCurrentSignal(callsign, freq, sr);
        }

        public BATCSpectrum(PictureBox Spectrum, int Tuners) 
        {
            _spectrum = Spectrum;

            _tuners = Tuners;

            _spectrum.Click += new System.EventHandler(this.spectrum_Click);
            _spectrum.MouseLeave += new System.EventHandler(this.spectrum_MouseLeave);
            _spectrum.MouseMove += new System.Windows.Forms.MouseEventHandler(this.spectrum_MouseMove);
            _spectrum.SizeChanged += new System.EventHandler(this.spectrum_SizeChanged);

            bmp2 = new Bitmap(_spectrum.Width, bandplan_height);     //bandplan
            bmp = new Bitmap(_spectrum.Width, height + 20);
            tmp = Graphics.FromImage(bmp);
            tmp2 = Graphics.FromImage(bmp2);

            greyPen.DashCap = System.Drawing.Drawing2D.DashCap.Round;
            greyPen.DashPattern = new float[] { 4.0F, 4.0F };

            try
            {
                bandplan = XElement.Load(Path.GetDirectoryName(Application.ExecutablePath) + @"\extra\bandplan.xml");
                drawspectrum_bandplan();
                indexedbandplan = bandplan.Elements().ToList();
                BuildChannelLabels();
                foreach (var channel in bandplan.Elements("channel"))
                {
                    if (!blocks.Contains(channel.Element("block").Value))
                    {
                        blocks.Add(channel.Element("block").Value);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            web_socket = new socket();

            sigs = new signal(list_lock);
            web_socket.callback += drawspectrum;
            web_socket.ConnectionStatusChanged += Web_socket_ConnectionStatusChanged;
            sigs.debug += debug;

            // try to connect
            web_socket.start();

            sigs.set_num_rx_scan(num_rxs_to_scan);
            sigs.set_num_rx(1);

            sigs.set_avoidbeacon(true);

            SpectrumTuneTimer = new Timer();
            SpectrumTuneTimer.Enabled = false;
            SpectrumTuneTimer.Interval = 1500;
            SpectrumTuneTimer.Tick += new System.EventHandler(this.SpectrumTuneTimer_Tick);

            websocketTimer = new Timer();
            websocketTimer.Interval = 2000;
            websocketTimer.Tick += new System.EventHandler(this.websocketTimer_Tick);
            websocketTimer.Enabled = true;

            draw_disconnect();
        }

        private void draw_disconnect()
        {
            tmp.Clear(Color.Black);
            tmp.DrawString("Spectrum server not reachable (eshail.batc.org.uk)", new Font("Tahoma", 10), Brushes.Red, new PointF(10, 10));

            string retry_text;
            if (connect_retry_count < connect_retries)
                retry_text = "Retrying ...." + connect_retry_count.ToString() + "/" + connect_retries.ToString();
            else
                retry_text = "Trying again every " + (slow_retry_seconds).ToString() + " s";

            tmp.DrawString(retry_text, new Font("Tahoma", 10), Brushes.Red, new PointF(10, 30));
            tmp.DrawString("Manual tuning: right click on the bandplan bars below", new Font("Tahoma", 10), Brushes.Gray, new PointF(10, 50));

            // keep the bandplan visible and clickable without spectrum data
            tmp.DrawImage(bmp2, 0, 255 - bandplan_height);
            UpdateDrawing();
        }

        private void Web_socket_ConnectionStatusChanged(object sender, bool connection_status)
        {
            if (connection_status)  // we are connected
            {
                // reset retry count
                connect_retry_count = 0;
                
            }
            else
            {
                // if we lost connection then disable autotune
                _autoTuneMode = 0;
                SpectrumTuneTimer.Enabled = false;
            }
            
        }

        public void Close()
        {
            websocketTimer?.Stop();
            websocketTimer?.Dispose();

            SpectrumTuneTimer?.Stop();
            SpectrumTuneTimer?.Dispose();
            
            // stop socket - not on the UI thread: WebSocket.Close() waits for the server and took up to 8 s with a
            // connection that was not clean, which froze the window on exit (issue #34)
            var socket_to_stop = web_socket;
            if (socket_to_stop != null)
                System.Threading.Tasks.Task.Run(() =>
                {
                    try { socket_to_stop.stop(); }
                    catch (Exception ex) { Log.Debug(ex, "BATC spectrum: closing the websocket failed"); }
                });
        }

        private void websocketTimer_Tick(object sender, EventArgs e)
        {
            if (web_socket != null)
            {

                TimeSpan t = DateTime.Now - web_socket.lastdata;

                if (t.TotalSeconds > 2)
                {
                    web_socket.stop();
                }

                if (!web_socket.connected)
                {
                    draw_disconnect();

                    // first retries every tick, afterwards slowly but without giving up - the server
                    // was once down for days and the spectrum never came back without a restart
                    if (connect_retry_count < connect_retries)
                    {
                        connect_retry_count += 1;
                        debug("Websocket Not Connected: Retrying " + connect_retry_count.ToString() + "/" + connect_retries.ToString());
                        web_socket.start();
                    }
                    else if ((DateTime.Now - last_slow_retry).TotalSeconds >= slow_retry_seconds)
                    {
                        last_slow_retry = DateTime.Now;
                        debug("Websocket Not Connected: Retrying (every " + slow_retry_seconds.ToString() + " s)");
                        web_socket.start();
                    }
                }

            }
        }

        public void changeTuneMode(int mode)
        {
            _autoTuneMode = mode;

            if (mode == 0)
            {
                SpectrumTuneTimer?.Stop();
            }
            else
            {
                SpectrumTuneTimer?.Start();
            }

        }

        private void SpectrumTuneTimer_Tick(object sender, EventArgs e)
        {
            int mode = _autoTuneMode;
            float spectrum_w = _spectrum.Width;
            float spectrum_wScale = spectrum_w / 922;

            ushort autotuneWait = 30;

            Tuple<signal.Sig, int> ret = sigs.tune(mode, Convert.ToInt16(autotuneWait), 0);
            if (ret.Item1.frequency > 0)      //above 0 is a change in signal
            {
                System.Threading.Thread.Sleep(100);
                selectSignal(Convert.ToInt32(ret.Item1.fft_centre * spectrum_wScale), 0);
                sigs.set_tuned(ret.Item1, 0);
                rx_blocks[0, 0] = Convert.ToInt16(ret.Item1.fft_centre);
                rx_blocks[0, 1] = Convert.ToInt16(ret.Item1.fft_stop - ret.Item1.fft_start);
            }

        }

        private void debug(string msg)
        {
            Log.Information(msg);
        }

        private void spectrum_MouseLeave(object sender, EventArgs e)
        {
            spectrumTunerHighlight = -1;
        }

        private void spectrum_SizeChanged(object sender, EventArgs e)
        {
            try
            {
                bmp2 = new Bitmap(_spectrum.Width, bandplan_height);     //bandplan
                bmp = new Bitmap(_spectrum.Width, height + 20);
                tmp = Graphics.FromImage(bmp);
                tmp2 = Graphics.FromImage(bmp2);

                if (bandplan != null)
                {
                    drawspectrum_bandplan();
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "BATC spectrum: update failed");
            }
        }


        // quicktune functions
        private void drawspectrum_bandplan()
        {
            int span = 9;
            int count = 0;

            float spectrum_w = _spectrum.Width;
            float spectrum_wScale = spectrum_w / 922;

            List<string> blocks = new List<string>();

            //count blocks ('layers' of bandplan)
            foreach (var channel in bandplan.Elements("channel"))
            {
                count++;
                if (!blocks.Contains(channel.Element("block").Value))
                {
                    blocks.Add(channel.Element("block").Value);
                }
            }

            channels = new Rectangle[count];

            int n = 0;

            //create rectangle blocks to display bandplan
            foreach (var channel in bandplan.Elements("channel"))
            {
                int w = 0;
                int offset = 0;
                float rolloff = 1.35f;
                string xval = channel.Element("x-freq").Value;

                float freq;
                int sr;

                freq = Convert.ToSingle(xval, CultureInfo.InvariantCulture);
                sr = Convert.ToInt32(channel.Element("sr").Value, CultureInfo.InvariantCulture);

                int pos = Convert.ToInt16((922.0 / span) * (freq - start_freq));
                w = Convert.ToInt32(sr / (span * 1000.0) * 922 * rolloff);
                w = Convert.ToInt32(w * spectrum_wScale);

                int split = bandplan_height / blocks.Count();
                int b = blocks.Count();
                foreach (string blk in blocks)
                {
                    if (channel.Element("block").Value == blk)
                    {
                        offset = b * split;
                    }
                    b--;
                }
                channels[n] = new Rectangle(Convert.ToInt32(pos * spectrum_wScale) - (w / 2), offset - (split / 2) - 3, w, split - 2);
                n++;
            }

            //draw blocks
            for (int i = 0; i < count; i++)
            {
                tmp2.FillRectangles(bandplanBrush, new RectangleF[] { channels[i] });      //x,y,w,h
            }
        }

        private void drawspectrum_signals(List<signal.Sig> signals)
        {
            float spectrum_w = _spectrum.Width;
            float spectrum_wScale = spectrum_w / 922;

            lock (list_lock)        //hopefully lock signals list while drawing
            {
                //draw the text for each signal found
                foreach (signal.Sig s in signals)
                {
                    string dbb_line = float.IsNaN(s.dbb) ? "" : "\n " + s.dbb.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "dBb";

                    tmp.DrawString(s.callsign + "\n" + s.frequency.ToString("#.00") + "\n " + (s.sr * 1000).ToString("#kS") + dbb_line, new Font("Tahoma", 10), Brushes.White, new PointF(Convert.ToSingle((s.fft_centre * spectrum_wScale) - (25)), (255 - Convert.ToSingle(s.fft_strength + 50) - (dbb_line.Length > 0 ? 14 : 0))));
                }
            }

            UpdateDrawing();
        }

        private void UpdateDrawing()
        {
            try
            {
                _spectrum.Parent?.Invoke(new MethodInvoker(delegate () { _spectrum.Image = bmp; _spectrum.Update(); }));
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "BATC spectrum: update failed");
            }

        }

        private void drawspectrum(UInt16[] fft_data)
        {
            tmp.Clear(Color.Black);     //clear canvas


            int spectrum_h = _spectrum.Height - bandplan_height;
            float spectrum_w = _spectrum.Width;
            float spectrum_wScale = spectrum_w / 922;

            int i = 1;
            int y = 0;


            PointF[] points = new PointF[fft_data.Length - 2];

            for (i = 1; i < fft_data.Length - 3; i++)     //ignore padding?
            {
                PointF point = new PointF(i * spectrum_wScale, 255 - fft_data[i] / 255);
                points[i] = point;
            }

            points[0] = new PointF(0, 255);
            points[points.Length - 1] = new PointF(spectrum_w, 255);

            if (spectrumTunerHighlight > -1)
            {
                y = spectrumTunerHighlight * (spectrum_h / _tuners);
                tmp.FillRectangle(tuner1Brush, new RectangleF(0, y, spectrum_w, (spectrum_h/_tuners)));
            }

            //tmp.FillRectangle((spectrumTunerHighlight == 1 ? tuner1Brush : tuner2Brush), new RectangleF(0, (spectrumTunerHighlight == 1 ? 0 : spectrum_h / 2), spectrum_w, _tuners == 1 ? spectrum_h : spectrum_h / 2));

            //tmp.DrawPolygon(greenpen, points);
            SolidBrush spectrumBrush = new SolidBrush(Color.Blue);

            LinearGradientBrush linGrBrush = new LinearGradientBrush(
               new Point(0, 0),
               new Point(0, 255),
               Color.FromArgb(255, 255, 99, 132),   // Opaque red
               Color.FromArgb(255, 54, 162, 235));  // Opaque blue

            tmp.FillPolygon(linGrBrush, points);

            tmp.DrawImage(bmp2, 0, 255 - bandplan_height); //bandplan

            y = 0;

            for (int tuner = 0; tuner < _tuners; tuner++)
            {
                y = tuner * (spectrum_h / _tuners);

                //draw block showing signal selected
                if (rx_blocks[tuner, 0] > 0)
                {
                    tmp.FillRectangle(shadowBrush, new RectangleF(rx_blocks[tuner, 0] * spectrum_wScale - ((rx_blocks[tuner, 1] * spectrum_wScale)/2) , y, rx_blocks[tuner, 1] * spectrum_wScale, (spectrum_h / _tuners)));
                }
            }

            using (Font info_font = new Font("Tahoma", 15))
            using (StringFormat info_format = new StringFormat())
            {
                // tab stop behind the widest "Dn: ..." text, so "SR:" (line 1) and "CH:" (line 2) start at the same place
                info_format.SetTabStops(0, new float[] { tmp.MeasureString(" Dn: 10499.25", info_font).Width });
                tmp.DrawString(InfoText, info_font, Brushes.White, new PointF(10, 10), info_format);
            }
            tmp.DrawString(TX_Text, new Font("Tahoma", 15), Brushes.Red, new PointF(70, _spectrum.Height - 50));  //dh3cs

            //drawspectrum_signals(sigs.detect_signals(fft_data));
            sigs.detect_signals(fft_data);

            // draw over power
            // locked like every other signalsData access (detect_signals, updateCurrentSignal,
            // drawspectrum_signals) - this loop was the one unprotected spot and could race with
            // a concurrent mutation from the socket data thread ("Collection was modified").
            lock (list_lock)
            {
                foreach (var sig in sigs.signalsData)
                {
                    if (sig.overpower)
                    {
                        tmp.FillRectangles(overpowerBrush, new RectangleF[] { new System.Drawing.Rectangle(Convert.ToInt16(sig.fft_centre * spectrum_wScale) - (Convert.ToInt16((sig.fft_stop - sig.fft_start) * spectrum_wScale) / 2), 1, Convert.ToInt16((sig.fft_stop - sig.fft_start) * spectrum_wScale), (255) - 4) });
                    }
                }
            }

            for (i = 0; i < _tuners; i++)
            {
                y = i * (spectrum_h / _tuners);
                tmp.DrawLine(greyPen, 10, y, spectrum_w, y);
                tmp.DrawString("RX " + (i+1).ToString(), new Font("Tahoma", 10), Brushes.White, new PointF(5, y));
            }

            drawspectrum_signals(sigs.signalsData);
        }

        private void spectrum_Click(object sender, EventArgs e)
        {

            float spectrum_w = _spectrum.Width;
            float spectrum_wScale = spectrum_w / 922;

            System.Windows.Forms.MouseEventArgs me = (System.Windows.Forms.MouseEventArgs)e;
            var pos = me.Location;


            int X = pos.X;
            int Y = pos.Y;

            if (me.Button == MouseButtons.Right)
            {
                // manual tuning: right click on a bandplan channel tunes to its frequency with the
                // symbol rate of the bar (A 1000, B 333, C 125 kS, from bandplan.xml). Works without
                // spectrum data, so it is also usable while the spectrum server is down.
                if (find_bandplan_channel(X, Y) >= 0)
                    tuneBandplanChannel(X, Y);
                else if (Y <= height - bandplan_height)
                    rightClickSignalArea(X, Y);
            }
            else
            {
                selectSignal(X, Y);
            }

        }


        // right click in the signal area (above the bandplan): the tuner of the clicked RX row is tuned to the signal
        // under the mouse, or to the frequency of the mouse position if there is none (narrow signals of 20 / 25 kS
        // are often not detected); the caller picks the symbol rate and opens the tab of the tuner
        private void rightClickSignalArea(int X, int Y)
        {
            float spectrum_wScale = _spectrum.Width / 922f;
            float bin = X / spectrum_wScale;
            int rx = Math.Min(determine_rx(Y), _tuners - 1);

            double freq_mhz = start_freq + ((bin + 1) / 922.0) * 9.0;   // same as detect_signals
            uint signal_sr = 0;

            try
            {
                foreach (signal.Sig s in sigs.signals)
                {
                    if (bin > s.fft_start & bin < s.fft_stop)
                    {
                        freq_mhz = s.frequency;
                        signal_sr = Convert.ToUInt32(s.sr * 1000.0);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "BATC spectrum: right click failed");
            }

            uint freq = Convert.ToUInt32(freq_mhz * 1000.0);   // kHz
            debug("Spectrum right click - RX: " + rx + " Freq: " + freq + " SR: " + signal_sr);

            OnSpectrumRightClick?.Invoke(rx, freq, signal_sr);
        }

        // grey block of a tuner in the spectrum (frequency in kHz, symbol rate in kS), for a tuning done outside of it
        public void MarkTuner(int rx, uint freq_khz, uint sr_ks)
        {
            if (rx < 0 || rx >= 4)
                return;

            rx_blocks[rx, 0] = Convert.ToInt16(((freq_khz / 1000.0 - start_freq) / 9.0) * 922.0);
            rx_blocks[rx, 1] = Convert.ToInt16(sr_ks / 1000.0 / 9.0 * 922.0 * 1.35);
        }

        // quick tune functions - From https://github.com/m0dts/QO-100-WB-Live-Tune - Rob Swinbank

        private int determine_rx(int pos)
        {
            int rx = 0;
            int div = (_spectrum.Height - bandplan_height) / _tuners;
            rx = pos / div;

            return rx;
        }

        private void selectSignal(int X, int Y)
        {

            float spectrum_w = _spectrum.Width;
            float spectrum_wScale = spectrum_w / 922;
            int spectrum_h = _spectrum.Height - bandplan_height;

            int rx = determine_rx(Y);

            debug("Select Signal - RX: " + rx.ToString());

            try
            {
                foreach (signal.Sig s in sigs.signals)
                {
                    if ((X / spectrum_wScale) > s.fft_start & (X / spectrum_wScale) < s.fft_stop)
                    {

                        sigs.set_tuned(s, rx);
                        rx_blocks[rx, 0] = Convert.ToInt16(s.fft_centre);
                        rx_blocks[rx, 1] = Convert.ToInt16((s.fft_stop) - (s.fft_start));
                        UInt32 freq = Convert.ToUInt32((s.frequency) * 1000);
                        UInt32 sr = Convert.ToUInt32((s.sr * 1000.0));

                        debug("Freq: " + freq.ToString());
                        debug("SR: " + sr.ToString());

                        OnSignalSelected?.Invoke(rx, freq, sr);

                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "BATC spectrum: update failed");
            }
        }

        int spectrumTunerHighlight = -1;

        public void spectrum_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            get_bandplan_TX_freq(e.X, e.Y);  // dh3cs

            spectrumTunerHighlight = determine_rx(e.Y);

        }

        // index of the bandplan channel under the mouse, -1 if there is none
        private int find_bandplan_channel(int x, int y)
        {
            // the bandplan is drawn at a fixed place in the bitmap (see drawspectrum), not at the bottom of the
            // PictureBox, which can be taller or smaller than the bitmap
            int bandplan_top = height - bandplan_height;

            if (channels == null || indexedbandplan == null || y <= bandplan_top)
                return -1;

            int n = 0;
            int found = -1;
            foreach (Rectangle ch in channels)
            {
                if (x >= ch.Location.X & x <= ch.Location.X + ch.Width)
                {
                    if (y - bandplan_top >= ch.Location.Y - (ch.Height / 2) + 3 & y - bandplan_top <= ch.Location.Y + (ch.Height / 2) + 3)
                    {
                        found = n;
                    }
                }
                n++;
            }
            return found;
        }

        // "A1", "B3", ...: the row of a bandplan channel and its number in that row, counted by ascending frequency
        // (bandplan.xml has no channel numbers); same index as indexedbandplan
        private string[] bandplan_channel_labels;

        private void BuildChannelLabels()
        {
            bandplan_channel_labels = new string[indexedbandplan.Count];

            foreach (var row in indexedbandplan.Select((channel, index) => new { channel, index }).GroupBy(c => c.channel.Element("block").Value))
            {
                int number = 1;
                foreach (var item in row.OrderBy(c => Convert.ToDouble(c.channel.Element("x-freq").Value, CultureInfo.InvariantCulture)))
                {
                    bandplan_channel_labels[item.index] = row.Key + number.ToString();
                    number++;
                }
            }
        }

        // returns TX-Freq in MHz from the rectangle in Bandplan and updates the info text, dh3cs
        private string get_bandplan_TX_freq(int x, int y)
        {
            string tx_freq_MHz = "";
            int n = find_bandplan_channel(x, y);

            if (n >= 0)
            {
                tx_freq_MHz = indexedbandplan[n].Element("s-freq").Value;
                // "SR:" and "CH:" start at the tab stop set where the text is drawn, so they are aligned
                InfoText = " Dn: " + indexedbandplan[n].Element("x-freq").Value + "\tSR: " + indexedbandplan[n].Element("name").Value + Environment.NewLine
                    + " Up: " + tx_freq_MHz + "\tCH: " + bandplan_channel_labels[n];
            }
            else if (InfoText != "")
            {
                InfoText = "";
            }
            return tx_freq_MHz;
        }

        // tune the bandplan channel under the mouse (frequency and symbol rate from bandplan.xml): directly with
        // one tuner, otherwise the tuner is chosen from a small menu that only lists the available ones
        private void tuneBandplanChannel(int x, int y)
        {
            int n = find_bandplan_channel(x, y);
            if (n < 0)
                return;

            if (_tuners <= 1)
            {
                tuneBandplanChannelRx(n, 0);
                return;
            }

            string name = indexedbandplan[n].Element("x-freq").Value + " MHz, " + indexedbandplan[n].Element("sr").Value + " kS";

            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripLabel(name));
            menu.Items.Add(new ToolStripSeparator());
            for (int rx = 0; rx < _tuners && rx < 4; rx++)
            {
                int selected_rx = rx;
                menu.Items.Add("Tune RX" + (rx + 1).ToString(), null, (s, e) => tuneBandplanChannelRx(n, selected_rx));
            }
            menu.Closed += (s, e) => _spectrum.BeginInvoke(new MethodInvoker(menu.Dispose)); // after the item click ran
            menu.Show(_spectrum, new Point(x, y));
        }

        private void tuneBandplanChannelRx(int n, int rx)
        {
            try
            {
                double freq_mhz = Convert.ToDouble(indexedbandplan[n].Element("x-freq").Value, CultureInfo.InvariantCulture);
                double sr_ks = Convert.ToDouble(indexedbandplan[n].Element("sr").Value, CultureInfo.InvariantCulture);

                uint freq = Convert.ToUInt32(freq_mhz * 1000.0);   // kHz
                uint sr = Convert.ToUInt32(sr_ks);                  // kS, same units as selectSignal

                debug("Bandplan tune - RX: " + rx.ToString() + " Freq: " + freq.ToString() + " SR: " + sr.ToString());

                // grey block for the selected channel
                MarkTuner(rx, freq, sr);

                OnSignalSelected?.Invoke(rx, freq, sr);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "BATC spectrum: bandplan tune failed");
            }
        }
    }
}
