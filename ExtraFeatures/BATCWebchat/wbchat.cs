using SocketIOClient;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading;
using Serilog;
using opentuner.ExtraFeatures.BATCWebchat;
using opentuner.MediaSources;

namespace opentuner
{
    public partial class WebChatForm : Form
    {
        static Font consoleFont; 
        static Font consoleFontBold;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string prop_title { set { this.Text = value; } }

        WebChatSettings _settings;
        OTSource _source;

        public WebChatForm(WebChatSettings Settings, OTSource Source)
        {
            InitializeComponent();

            _settings = Settings;
            _source = Source;
            consoleFont = new Font("Consolas", _settings.chat_font_size);
            consoleFontBold = new Font("Consolas", _settings.chat_font_size + 1, FontStyle.Bold);
        }

        // After the settings window with the chat open: the signal report template and the auto login are read where they
        // are used, so they apply at once, the nick is used for the next login. The font size cannot be changed for the
        // lines already written: the chat is built again then (BATCChat.CloseForRestart).
        public void ApplySettings()
        {
            if (_settings.nickname.Length > 0)
                txtNick.Text = _settings.nickname;
        }

        // Closing the window with the X only hides it (wbchat_FormClosing); this closes it for real and drops the connection.
        private bool _shutdown = false;

        public void Shutdown()
        {
            _shutdown = true;

            try
            {
                client?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Chat: closing the connection failed");
            }

            Close();
        }

        private SocketIO client = null;

        private void wbchat_Load(object sender, EventArgs e)
        {
            if (_settings.nickname.Length > 0)
            {
                txtNick.Text = _settings.nickname;
            }
            else
            {
                txtNick.Text = "NONICK";
            }

            client = new SocketIO("https://eshail.batc.org.uk/", new SocketIOOptions
            {
                Path = "/wb/chat/socket.io",
                Query = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("room", "eshail-wb"),
                    }
            });

            client.OnConnected += Client_OnConnected;
            client.OnDisconnected += Client_OnDisconnected;

            Action<SocketIOResponse> callbackHistory = new Action<SocketIOResponse>(onHistoryCallback);
            Action<SocketIOResponse> callbackMessage = new Action<SocketIOResponse>(onMessageCallback);
            Action<SocketIOResponse> callbackNicks = new Action<SocketIOResponse>(onNicksCallback);
            Action<SocketIOResponse> callbackViewers = new Action<SocketIOResponse>(onViewersCallback);

            client.On("history", callbackHistory);
            client.On("message", callbackMessage);
            client.On("nicks", callbackNicks);
            client.On("viewers", callbackViewers);

            lbUsers.Font = consoleFontBold;
            lbUsers.ForeColor = Color.FromArgb(204, 204, 204);
            txtMessage.BackColor = Color.FromArgb(63, 70, 76);
            txtMessage.ForeColor = Color.FromArgb(204, 204, 204);
            txtMessage.Font = consoleFontBold;

            AddMessage("", "", "Connecting...");
            client.ConnectAsync();

            if (_source.GetVideoSourceCount() > 0)
                btnSigReportTuner1.Enabled = true;
            if (_source.GetVideoSourceCount() > 1)
                btnSigReportTuner2.Enabled = true;
            if (_source.GetVideoSourceCount() > 2)
            {
                btnSigReportTuner3.Enabled = true;
                btnSigReportTuner4.Enabled = true;
            }
        }

        private void Client_OnDisconnected(object sender, string e)
        {
            Log.Information("Chat: disconnected ({Reason}), disconnects so far: {Count}", e, Interlocked.Increment(ref _disconnectCount));
            SetConnectedLabel(false);
        }

        private int _connectCount;
        private int _disconnectCount;

        // socket callbacks run on a worker thread, never block it waiting for the UI thread
        private void SetConnectedLabel(bool connected)
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            BeginInvoke(new Action(() => lblConnected.Text = "Connected: " + connected));
        }

        private delegate void UpdateLBDelegate(System.Windows.Forms.ListBox LB, Object obj);

        public static void AddItem(System.Windows.Forms.ListBox LB, Object obj)
        {
            if (LB.InvokeRequired)
            {
                UpdateLBDelegate ulb = new UpdateLBDelegate(AddItem);
                
                    LB.Invoke(ulb, new object[] { LB, obj });
            }
            else
            {
                if (LB.Items.Count > 1000)
                {
                    LB.Items.Remove(0);
                }

                int i = LB.Items.Add(obj);
                LB.TopIndex = i;
            }
        }

        private delegate void UpdateFormTitle(WebChatForm frm, string new_title);

        public void updateTitle(WebChatForm frm, string new_title)
        {

            if (frm.InvokeRequired)
            {
                UpdateFormTitle ulb = new UpdateFormTitle(updateTitle);

                    frm.Invoke(ulb, new object[] { frm, new_title });
            }
            else
            {
                frm.prop_title = new_title;
            }
        }

        private const int MaxChatLines = 500;
        private const int TrimChatBatch = 100;
        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private delegate void UpdateRTBDelegate(RichTextBox LB, string tstr, string nick, string msg);

        // appends one chat line, formatting only, no scrolling or trimming
        private static void AppendChatLine(RichTextBox rtb, string tstr, string nick, string msg)
        {
            // 204, 204, 204
            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionFont = consoleFont;
            rtb.SelectionColor = Color.FromArgb(204, 204, 204);
            rtb.AppendText(tstr);

            rtb.SelectionFont = consoleFontBold;
            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionLength = 0;
            rtb.SelectionColor = Color.FromArgb(251, 222, 45);
            rtb.AppendText(" <" + nick + "> ");

            rtb.SelectionFont = consoleFont;
            rtb.SelectionColor = Color.FromArgb(204, 204, 204);
            rtb.SelectionStart = rtb.TextLength;
            rtb.AppendText(msg + "\n");
        }

        // keeps the chat at about MaxChatLines lines, trimming in batches
        private static void TrimChat(RichTextBox rtb)
        {
            int lines = rtb.GetLineFromCharIndex(rtb.TextLength) + 1;
            if (lines <= MaxChatLines + TrimChatBatch)
                return;

            int cut = rtb.GetFirstCharIndexFromLine(lines - MaxChatLines);
            if (cut > 0)
            {
                rtb.Select(0, cut);
                rtb.SelectedText = "";
            }
        }

        private static void ScrollToEnd(RichTextBox rtb)
        {
            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionLength = 0;
            rtb.ScrollToCaret();
        }

        public static void AddChat(RichTextBox rtb, string tstr, string nick, string msg)
        {
            if (rtb.InvokeRequired)
            {
                if (rtb.IsDisposed || !rtb.IsHandleCreated)
                    return;

                // BeginInvoke keeps the order of messages and does not block the caller
                rtb.BeginInvoke(new UpdateRTBDelegate(AddChat), new object[] { rtb, tstr, nick, msg });
            }
            else
            {
                AppendChatLine(rtb, tstr, nick, msg);
                TrimChat(rtb);
                ScrollToEnd(rtb);
            }
        }

        // replaces the whole chat with the history in one go, redraw is off while doing so
        public static void SetChatHistory(RichTextBox rtb, List<(string time, string nick, string msg)> items)
        {
            if (rtb.InvokeRequired)
            {
                if (rtb.IsDisposed || !rtb.IsHandleCreated)
                    return;

                rtb.BeginInvoke(new Action<RichTextBox, List<(string time, string nick, string msg)>>(SetChatHistory), new object[] { rtb, items });
                return;
            }

            SendMessage(rtb.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                rtb.Clear();
                foreach (var item in items)
                {
                    AppendChatLine(rtb, item.time, item.nick, item.msg);
                }
                TrimChat(rtb);
                ScrollToEnd(rtb);
            }
            finally
            {
                SendMessage(rtb.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                rtb.Invalidate();
            }
        }

        public static void ClearAll(System.Windows.Forms.ListBox LB, Object obj)
        {
            if (LB.InvokeRequired)
            {
                UpdateLBDelegate ulb = new UpdateLBDelegate(ClearAll);
                LB.Invoke(ulb, new object[] { LB, obj });
            }
            else
            {
                LB.Items.Clear();
            }
        }

        private const int MaxStoredMessages = 500;

        // all received messages (UI thread only), the chat shows the ones matching the filter
        private readonly List<(string time, string nick, string msg)> _messages = new List<(string time, string nick, string msg)>();

        private string CurrentFilter
        {
            get { return txtFilter.Text.Trim(); }
        }

        // free text, ignores case, matches the nick or the message text
        private static bool MatchesFilter((string time, string nick, string msg) m, string filter)
        {
            return filter.Length == 0
                || m.nick.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || m.msg.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateFilterLabel(string filter)
        {
            lblFilter.Text = filter.Length == 0
                ? "Filter"
                : "Filter: " + _messages.FindAll(m => MatchesFilter(m, filter)).Count + " of " + _messages.Count;
        }

        // redraws the chat from the stored messages, UI thread only
        private void RenderChat()
        {
            string filter = CurrentFilter;
            SetChatHistory(richChat, _messages.FindAll(m => MatchesFilter(m, filter)));
            UpdateFilterLabel(filter);
        }

        // can be called from any thread
        private void AddMessage(string time, string nick, string msg)
        {
            if (InvokeRequired)
            {
                if (IsDisposed || !IsHandleCreated)
                    return;

                BeginInvoke(new Action<string, string, string>(AddMessage), time, nick, msg);
                return;
            }

            var entry = (time, nick, msg);
            _messages.Add(entry);
            if (_messages.Count > MaxStoredMessages)
            {
                _messages.RemoveRange(0, _messages.Count - MaxStoredMessages);
            }

            string filter = CurrentFilter;
            if (MatchesFilter(entry, filter))
            {
                AddChat(richChat, time, nick, msg);
            }
            UpdateFilterLabel(filter);
        }

        // replaces all stored messages, can be called from any thread
        private void SetHistory(List<(string time, string nick, string msg)> items)
        {
            if (InvokeRequired)
            {
                if (IsDisposed || !IsHandleCreated)
                    return;

                BeginInvoke(new Action<List<(string time, string nick, string msg)>>(SetHistory), items);
                return;
            }

            _messages.Clear();
            _messages.AddRange(items);
            if (_messages.Count > MaxStoredMessages)
            {
                _messages.RemoveRange(0, _messages.Count - MaxStoredMessages);
            }
            RenderChat();
        }

        // redraw shortly after the last keystroke, not on every letter
        private void txtFilter_TextChanged(object sender, EventArgs e)
        {
            tmrFilter.Stop();
            tmrFilter.Start();
        }

        private void tmrFilter_Tick(object sender, EventArgs e)
        {
            tmrFilter.Stop();
            RenderChat();
        }

        private void btnFilterClear_Click(object sender, EventArgs e)
        {
            txtFilter.Clear();
            tmrFilter.Stop();
            RenderChat();
            txtFilter.Focus();
        }

        private void initUsers(SocketIOResponse response)
        {
            ClearAll(lbUsers, "");

            var nicks = response.GetValue(0).GetProperty("nicks").EnumerateArray();

            foreach (System.Text.Json.JsonElement nick in nicks)
            {
                AddItem(lbUsers, nick.ToString());
            }
        }

        private void initHistory(SocketIOResponse response)
        {
            var history = response.GetValue(0).GetProperty("history").EnumerateArray();
            var items = new List<(string time, string nick, string msg)>();

            foreach (System.Text.Json.JsonElement hist_item in history)
            {
                string time = hist_item.GetProperty("time").ToString();
                DateTime timeobj = Convert.ToDateTime(time);

                items.Add((timeobj.ToString("HH:mm"), hist_item.GetProperty("name").ToString(), hist_item.GetProperty("message").ToString()));
            }

            Log.Information("Chat: history received, {Count} messages", items.Count);
            SetHistory(items);
        }

        private void onViewersCallback(SocketIOResponse response)
        {
            updateTitle(this, "QO-100 Wideband Chat - Viewers: " + response.GetValue(0).GetProperty("num").ToString());
        }

        private void onMessageCallback(SocketIOResponse response)
        {
            var newMessage = response.GetValue(0);
            string time = newMessage.GetProperty("time").ToString();
            DateTime timeobj = Convert.ToDateTime(time);
            //string newMsg = timeobj.ToString("HH:mm") + " <" + newMessage.GetProperty("name").ToString() + ">" + " " + newMessage.GetProperty("message").ToString();
            //AddItem(lbChat, newMsg);

            AddMessage(timeobj.ToString("HH:mm"), newMessage.GetProperty("name").ToString(), newMessage.GetProperty("message").ToString());
        }

        private void onNicksCallback(SocketIOResponse response)
        {
            initUsers(response);
        }

        private void onHistoryCallback(SocketIOResponse response)
        {
            initUsers(response);
            initHistory(response);
        }

        class nickInfo
        {
            [JsonPropertyName("nick")]
            public string nick { get; set; }
        }

        class chatMessage
        {
            [JsonPropertyName("message")]
            public string message { get; set; }
        }

        private void Client_OnConnected(object sender, EventArgs e)
        {
            Log.Information("Connected socketio (connects so far: {Count})", Interlocked.Increment(ref _connectCount));
            SetConnectedLabel(true);

            // also after a reconnect, the server does not remember the nick
            if (_settings.gui_autologin && !IsDisposed && IsHandleCreated)
            {
                BeginInvoke(new Action(setNick));
            }
        }

        private void setNick()
        {
            if (client.Connected)
            {
                string nick = txtNick.Text.Trim();

                if (nick.Length > 0 && nick != "NONICK")
                {
                    client.EmitAsync("setnick", new nickInfo { nick = nick });
                    txtMessage.Enabled = true;
                    AddItem(lbChat, "*** Your nick is set to " + nick + " ***");
                    _settings.nickname = nick;
                }
            }
        }

        private void btnSetNick_Click(object sender, EventArgs e)
        {
            setNick();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            sendMessage();
        }

        private void sendMessage()
        {
            if (client.Connected)
            {
                string msg = txtMessage.Text.Trim();

                if (msg.Length > 0)
                {
                    client.EmitAsync("message", new chatMessage { message = msg });
                }

                txtMessage.Text = "";
            }

        }

        private void txtMessage_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                sendMessage();
            }
        }

        private void wbchat_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_shutdown)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void lbUsers_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (txtMessage.Enabled && lbUsers.SelectedIndex >= 0)
            {
                string selectedName = lbUsers.SelectedItem.ToString();
                txtMessage.Text = txtMessage.Text + " @" + selectedName + " ";
            }
        }

        private void checkStayOnTop_CheckedChanged(object sender, EventArgs e)
        {
            
            if ( checkStayOnTop.Checked )
            {
                TopMost = true;
            }
            else
            {
                TopMost = false;
            }
           
        }

        private void copySelectedTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(richChat.SelectedText, TextDataFormat.UnicodeText);
        }

        private void txtNick_Click(object sender, EventArgs e)
        {
            setnickdialog nickDialog = new setnickdialog();

            nickDialog.txtNick.Text = txtNick.Text;

            _settings.nickname = txtNick.Text;

            if (nickDialog.ShowDialog() == DialogResult.OK)
            {
                txtNick.Text = nickDialog.txtNick.Text;
                setNick();

                if (txtNick.Text.Length > 0 && txtNick.Text != "NONICK")
                {
                    DateTime timeobj = DateTime.Now;
                    AddMessage(timeobj.ToString("HH:mm"), "Chat", "You are now known as '" + txtNick.Text + "'");
                }
            }
        }

        private void lbChat_Resize(object sender, EventArgs e)
        {
        }

        private void richChat_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to follow this link?", "Warning", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                opentuner.Utilities.CommonFunctions.OpenUrl(e.LinkText);
            }
        }

        private void lbUsers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbUsers.SelectedIndex > 0)
            {
                // from Chris - DH3CS
                string user = lbUsers.GetItemText(lbUsers.SelectedItem);

                try
                {
                    richChat.SelectionStart = 0;
                    richChat.SelectionLength = richChat.Text.Length - 1;
                    richChat.SelectionBackColor = Color.FromArgb(63, 70, 76);
                    // set the current caret position to the end, if nothing will be found
                    richChat.SelectionStart = richChat.Text.Length;

                    foreach (Match match in Regex.Matches(richChat.Text, user))
                    {
                        richChat.SelectionStart = match.Index;
                        richChat.SelectionLength = match.Length;
                        richChat.SelectionBackColor = Color.FromArgb(0, 255, 0);
                    }
                    // scroll it automatically
                    richChat.ScrollToCaret();
                }
                catch { }
            }
            else
            {
                richChat.SelectionStart = 0;
                richChat.SelectionLength = richChat.Text.Length - 1;
                richChat.SelectionBackColor = Color.FromArgb(63, 70, 76);
                richChat.SelectionStart = richChat.Text.Length;
                richChat.ScrollToCaret();
            }
        }

        private void lbUsers_MouseClick(object sender, MouseEventArgs e)
        {
        }

        private void lbUsers_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { lbUsers.SelectedIndex = -1; }
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            txtMessage.Text = Clipboard.GetText();
        }

        private void getSignalReportData(int tuner)
        {
            var data = _source.GetSignalData(tuner);

            //string signalReport = "SigReport: " + lblServiceName.Text.ToString() + "/" + lblServiceProvider.Text.ToString() + " - " + lbldbMargin.Text.ToString() + " (" + lblMer.Text.ToString() + ") - " + lblSR.Text.ToString() + "" + " - " + (freq).ToString() + " ";
            string signalReport = _settings.sigreport_template.ToString();

            // SigReport: {SN}/{SP} - {DBM} - ({MER}) - {SR} - {VCODEC} - {FREQ} - {TSERR}

            signalReport = signalReport.Replace("{SN}", data["ServiceName"]);
            signalReport = signalReport.Replace("{SP}", data["ServiceProvider"]);
            signalReport = signalReport.Replace("{DBM}", data["dbMargin"]);
            signalReport = signalReport.Replace("{MER}", data["Mer"] + " dB");
            signalReport = signalReport.Replace("{SR}", data["SR"] + "");
            signalReport = signalReport.Replace("{VCODEC}", data["VideoCodec"] + "");
            signalReport = signalReport.Replace("{FREQ}", data["Frequency"] + "");

            // TS errors of the last few seconds (MiniTiouner only, other sources don't provide them). Without any
            // errors the placeholder and the " - " in front of it are removed, so a clean report stays as it was.
            string ts_errors;
            if (!data.TryGetValue("TSErrors", out ts_errors))
                ts_errors = "";

            if (ts_errors.Length > 0)
                signalReport = signalReport.Replace("{TSERR}", "TS errors: " + ts_errors);
            else
                signalReport = Regex.Replace(signalReport, @"(\s*-)?\s*\{TSERR\}", "");

            txtMessage.Text = signalReport;

            Clipboard.SetText(signalReport);
        }

        private void btnSigReportTuner1_Click(object sender, EventArgs e)
        {
            getSignalReportData(0);
        }

        private void btnSigReportTuner2_Click(object sender, EventArgs e)
        {
            getSignalReportData(1);
        }

        private void btnSigReportTuner3_Click(object sender, EventArgs e)
        {
            getSignalReportData(2);
        }

        private void btnSigReportTuner4_Click(object sender, EventArgs e)
        {
            getSignalReportData(3);
        }
    }
}
