using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using opentuner.MediaPlayers;
using opentuner.Utilities;
using Serilog;

namespace opentuner.MediaSources.Minitiouner
{
    // The MiniTiouner source of the program: one MinitiounerSource per connected board, presented as one source with up
    // to 4 tuners (issue #29). With a single board it passes everything on to that one board unchanged.
    //
    // The tuners are numbered board after board: the board that was chosen first (a Pro before a V2, or the one named in
    // PreferredBoardSerial) has tuners 1..n, the next board continues. Every call with a tuner number ("device") is
    // routed to the board it belongs to with the number inside that board.
    public class MinitiounerMultiSource : OTSource
    {
        private const int MaxTuners = 4;

        private readonly MinitiounerSource _primary = new MinitiounerSource();
        private readonly List<MinitiounerSource> _boards = new List<MinitiounerSource>();
        private readonly List<int> _offsets = new List<int>();     // number of the first tuner of each board (0-based)

        // Every further board has its own "Properties" page (the tuner groups of several boards do not fit on one);
        // the first board keeps the page of the main window. Same order as _boards, null for the first board.
        private readonly List<Panel> _property_panels = new List<Panel>();

        public override event SourceDataChange OnSourceData;

        public MinitiounerMultiSource()
        {
            _primary.OnSourceData += (nr, data, description) => OnSourceData?.Invoke(nr, data, description);
            _boards.Add(_primary);
            _offsets.Add(0);
            _property_panels.Add(null);
        }

        // ---- start / stop -------------------------------------------------------------------------------------------

        public override int Initialize(VideoChangeCallback VideoChangeCB, Control Parent)
        {
            // a second connect starts from the first board again
            while (_boards.Count > 1)
            {
                _boards.RemoveAt(_boards.Count - 1);
                _offsets.RemoveAt(_offsets.Count - 1);
                _property_panels.RemoveAt(_property_panels.Count - 1);
            }

            int total = _primary.Initialize(ForBoard(VideoChangeCB, 0), Parent);
            if (total < 0)
                return total;

            if (_primary.UseAllBoards && _primary.SelectedBoard != null)
            {
                foreach (var board in FindFurtherBoards(_primary.SelectedBoard))
                {
                    if (total >= MaxTuners)
                        break;

                    int offset = total;
                    var child = new MinitiounerSource { TunerNumberOffset = offset };
                    child.OnSourceData += (nr, data, description) => OnSourceData?.Invoke(nr + offset, data, description);

                    Log.Information("Multi board: connecting " + board.Name + " as tuner " + (offset + 1));
                    var properties_panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
                    int players = child.InitializeForBoard(board.ChipSerials[0], ForBoard(VideoChangeCB, offset), properties_panel);
                    if (players <= 0 || !child.hardware_connected)
                    {
                        Log.Warning("Multi board: " + board.Name + " could not be started, it is not used");
                        continue;
                    }

                    if (offset + players > MaxTuners)
                    {
                        Log.Warning("Multi board: " + board.Name + " would exceed " + MaxTuners + " tuners, it is not used");
                        child.Close();
                        continue;
                    }

                    _boards.Add(child);
                    _offsets.Add(offset);
                    _property_panels.Add(properties_panel);
                    total += players;
                }
            }

            if (_boards.Count > 1)
            {
                Log.Information("Multi board: " + _boards.Count + " boards, " + total + " tuners in total");

                // the page of the main window belongs to the first board
                if (Parent is TabPage page)
                    page.Text = "Properties-" + BoardLabel(0);
            }

            return total;
        }

        public override void Close()
        {
            for (int i = _boards.Count - 1; i >= 0; i--)
                _boards[i].Close();
        }

        public override bool DeviceConnected => _boards.Any(b => b.DeviceConnected);

        // The other complete boards on USB (not the one already in use), a Pro before a V2, then by serial number.
        private static List<MiniTiounerBoard> FindFurtherBoards(MiniTiounerBoard used)
        {
            var devices = HardwareInfo.EnumerateFtdiDevices(out string list_error);
            var boards = BoardDetection.Detect(devices, new List<string>());

            return boards
                .Where(b => b.Complete && !b.ChipSerials.Any(s => used.ChipSerials.Contains(s)))
                .OrderByDescending(b => b.TsCount)
                .ThenBy(b => b.Type == "Minitiouner Pro 2" ? 0 : 1)
                .ThenBy(b => b.ChipSerials.FirstOrDefault() ?? "", StringComparer.Ordinal)
                .ToList();
        }

        // "video_number" of the callbacks is 1-based and local to the board
        private static VideoChangeCallback ForBoard(VideoChangeCallback callback, int offset)
        {
            return (video_number, start) => callback?.Invoke(video_number + offset, start);
        }

        // ---- routing by tuner number --------------------------------------------------------------------------------

        private bool Resolve(int device, out MinitiounerSource board, out int local)
        {
            for (int i = _boards.Count - 1; i >= 0; i--)
            {
                if (device >= _offsets[i])
                {
                    board = _boards[i];
                    local = device - _offsets[i];
                    return local < board.GetVideoSourceCount();
                }
            }

            board = null;
            local = 0;
            return false;
        }

        // Short name of a board for the tab titles: "Pro", "V2" (also the E-Tiouner), "Express", "S"; several boards of the
        // same kind are numbered ("Pro1", "Pro2").
        private string BoardLabel(int board_index)
        {
            string label = ShortBoardName(_boards[board_index].SelectedBoard?.Type);
            var same = Enumerable.Range(0, _boards.Count).Where(i => ShortBoardName(_boards[i].SelectedBoard?.Type) == label).ToList();
            return same.Count > 1 ? label + (same.IndexOf(board_index) + 1) : label;
        }

        private static string ShortBoardName(string board_type)
        {
            switch (board_type)
            {
                case "Minitiouner Pro 2": return "Pro";
                case "BATC V2 Minitiouner": return "V2";
                case "Minitiouner Express": return "Express";
                case "Minitiouner-S": return "S";
                default: return string.IsNullOrEmpty(board_type) ? "M" : board_type;
            }
        }

        // "1-2" for a board with two tuners, "3" for one with a single tuner
        private string TunerRange(int board_index)
        {
            int first = _offsets[board_index] + 1;
            int count = _boards[board_index].GetVideoSourceCount();
            return count <= 1 ? first.ToString() : first + "-" + (first + count - 1);
        }

        public override int GetVideoSourceCount()
        {
            return _boards.Sum(b => b.GetVideoSourceCount());
        }

        public override void SetFrequency(int device, uint frequency, uint symbol_rate, bool offset_included)
        {
            if (Resolve(device, out var board, out int local))
                board.SetFrequency(local, frequency, symbol_rate, offset_included);
        }

        public override void SetFrequencyFromSpectrum(int device, uint frequency, uint symbol_rate)
        {
            if (Resolve(device, out var board, out int local))
                board.SetFrequencyFromSpectrum(local, frequency, symbol_rate);
        }

        public override uint GetSymbolRate(int device)
        {
            return Resolve(device, out var board, out int local) ? board.GetSymbolRate(local) : 0;
        }

        // "Special" with one board, "Special-Pro" / "Special-V2" with several (see GetExtraTabs)
        public override string GetSpecialTabTitle(int device)
        {
            if (!Resolve(device, out var board, out int local))
                return null;

            string title = board.GetSpecialTabTitle(local);
            if (title == null || _boards.Count == 1)
                return title;

            return title + "-" + BoardLabel(_boards.IndexOf(board));
        }

        public override long GetFrequency(int device, bool offset_included)
        {
            return Resolve(device, out var board, out int local) ? board.GetFrequency(local, offset_included) : 0;
        }

        public override int GetVolume(int device)
        {
            return Resolve(device, out var board, out int local) ? board.GetVolume(local) : 0;
        }

        public override void UpdateVolume(int device, int volume_delta)
        {
            if (Resolve(device, out var board, out int local))
                board.UpdateVolume(local, volume_delta);
        }

        public override void ToggleMute(int device)
        {
            if (Resolve(device, out var board, out int local))
                board.ToggleMute(local);
        }

        public override Dictionary<string, string> GetSignalData(int device)
        {
            return Resolve(device, out var board, out int local) ? board.GetSignalData(local) : new Dictionary<string, string>();
        }

        public override void StartStreaming(int device)
        {
            if (Resolve(device, out var board, out int local))
                board.StartStreaming(local);
        }

        public override void StopStreaming(int device)
        {
            if (Resolve(device, out var board, out int local))
                board.StopStreaming(local);
        }

        public override void StopTuner(int device)
        {
            if (Resolve(device, out var board, out int local))
                board.StopTuner(local);
        }

        public override CircularBuffer GetVideoDataQueue(int device)
        {
            return Resolve(device, out var board, out int local) ? board.GetVideoDataQueue(local) : null;
        }

        public override void RegisterTSConsumer(int device, CircularBuffer ts_buffer_queue)
        {
            if (Resolve(device, out var board, out int local))
                board.RegisterTSConsumer(local, ts_buffer_queue);
        }

        // ---- lists of players, recorders and streamers: every board takes its part ----------------------------------

        public override void ConfigureVideoPlayers(List<OTMediaPlayer> MediaPlayers)
        {
            for (int i = 0; i < _boards.Count; i++)
                _boards[i].ConfigureVideoPlayers(MediaPlayers.Skip(_offsets[i]).Take(_boards[i].GetVideoSourceCount()).ToList());
        }

        public override void ConfigureTSRecorders(List<TSRecorder> TSRecorders)
        {
            for (int i = 0; i < _boards.Count; i++)
                _boards[i].ConfigureTSRecorders(TSRecorders.Skip(_offsets[i]).Take(_boards[i].GetVideoSourceCount()).ToList());
        }

        public override void ConfigureTSStreamers(List<TSUdpStreamer> TSStreamers)
        {
            for (int i = 0; i < _boards.Count; i++)
                _boards[i].ConfigureTSStreamers(TSStreamers.Skip(_offsets[i]).Take(_boards[i].GetVideoSourceCount()).ToList());
        }

        public override void ConfigureMediaPath(string MediaPath)
        {
            foreach (var board in _boards)
                board.ConfigureMediaPath(MediaPath);
        }

        public override void OverrideDefaultMuted(bool Override)
        {
            foreach (var board in _boards)
                board.OverrideDefaultMuted(Override);
        }

        public override void UpdateFrequencyPresets(List<StoredFrequency> FrequencyPresets)
        {
            foreach (var board in _boards)
                board.UpdateFrequencyPresets(FrequencyPresets);
        }

        // ---- names, settings and tabs --------------------------------------------------------------------------------

        public override string GetName()
        {
            return _primary.GetName();
        }

        public override string GetDeviceName()
        {
            return string.Join(" + ", _boards.Select(b => b.GetDeviceName()).Where(n => !string.IsNullOrEmpty(n)));
        }

        public override string GetDescription()
        {
            return _primary.GetDescription();
        }

        public override string GetMoreInfoLink()
        {
            return _primary.GetMoreInfoLink();
        }

        // One board: its settings at once. Several boards: a small menu at the mouse first ("Pro - tuner 1-2",
        // "V2 - tuner 3"), then the settings of the chosen board (own Digole, LNB voltage, offsets ... per board).
        public override void ShowSettings()
        {
            var entries = new List<KeyValuePair<string, Action>>();

            if (_boards.Count > 1)
            {
                // connected: the boards in use
                for (int i = 0; i < _boards.Count; i++)
                {
                    var board = _boards[i];
                    string title = BoardLabel(i) + " - tuner " + TunerRange(i);
                    board.SettingsTitleSuffix = title;
                    entries.Add(new KeyValuePair<string, Action>(title, () => board.ShowSettings()));
                }
            }
            else if (!_primary.hardware_connected && _primary.UseAllBoards)
            {
                // not connected yet: the boards that are on USB, numbered the way the connect will number them
                try
                {
                    var devices = HardwareInfo.EnumerateFtdiDevices(out string list_error);
                    var found = BoardDetection.Detect(devices, new List<string>()).Where(b => b.Complete).ToList();
                    var first = BoardDetection.Select(found, _primary.PreferredBoardSerial);
                    if (first != null)
                    {
                        var ordered = new List<MiniTiounerBoard> { first };
                        ordered.AddRange(FindFurtherBoards(first));

                        if (ordered.Count > 1)
                        {
                            int offset = 0;
                            foreach (var board in ordered)
                            {
                                int first_tuner = offset;
                                offset += board.TsCount;

                                string label = ShortBoardName(board.Type);
                                var same = ordered.Where(o => ShortBoardName(o.Type) == label).ToList();
                                if (same.Count > 1)
                                    label += same.IndexOf(board) + 1;

                                string range = board.TsCount <= 1 ? (first_tuner + 1).ToString() : (first_tuner + 1) + "-" + (first_tuner + board.TsCount);
                                string title = label + " - tuner " + range;
                                var chosen = board;

                                entries.Add(new KeyValuePair<string, Action>(title, () =>
                                {
                                    var source = first_tuner == 0 ? _primary : new MinitiounerSource();
                                    source.TunerNumberOffset = first_tuner;
                                    source.SettingsTitleSuffix = title;
                                    source.SettingsTunerCount = chosen.TsCount;
                                    source.ShowSettingsForBoard(chosen.ChipSerials[0]);
                                }));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Source settings: could not list the boards before connecting");
                }
            }

            if (entries.Count <= 1)
            {
                _primary.ShowSettings();
                return;
            }

            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripLabel("Source settings of:"));
            menu.Items.Add(new ToolStripSeparator());
            foreach (var entry in entries)
            {
                var action = entry.Value;
                menu.Items.Add(entry.Key, null, (s, e) => action());
            }
            menu.Closed += (s, e) => System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault()?.BeginInvoke(new MethodInvoker(menu.Dispose));
            menu.Show(Cursor.Position);
        }

        // With several boards every board brings its own tabs; their names get the numbers of their tuners
        // ("Expert 1-2", "Special 1-2", "Expert 3", "Special 3").
        public override List<KeyValuePair<string, Control>> GetExtraTabs()
        {
            if (_boards.Count == 1)
                return _primary.GetExtraTabs();

            var per_board = _boards.Select(b => b.GetExtraTabs()).ToList();
            var titles = per_board.SelectMany(t => t.Select(k => k.Key)).Distinct().ToList();

            var tabs = new List<KeyValuePair<string, Control>>();

            // the "Properties" pages of the further boards come first, right behind the page of the main window
            for (int i = 1; i < _boards.Count; i++)
            {
                if (_property_panels[i] != null)
                    tabs.Add(new KeyValuePair<string, Control>("Properties-" + BoardLabel(i), _property_panels[i]));
            }

            foreach (var title in titles)
            {
                for (int i = 0; i < per_board.Count; i++)
                {
                    foreach (var tab in per_board[i].Where(t => t.Key == title))
                        tabs.Add(new KeyValuePair<string, Control>(title + "-" + BoardLabel(i), tab.Value));
                }
            }
            return tabs;
        }

        public override List<KeyValuePair<string, string>> GetHardwareInfo()
        {
            if (_boards.Count == 1)
                return _primary.GetHardwareInfo();

            var info = new List<KeyValuePair<string, string>>();
            info.Add(new KeyValuePair<string, string>("Source", GetName()));
            info.Add(new KeyValuePair<string, string>("Boards in use", _boards.Count + ", " + GetVideoSourceCount() + " tuners"));

            for (int i = 0; i < _boards.Count; i++)
            {
                info.Add(new KeyValuePair<string, string>("Board " + (i + 1) + " (" + BoardLabel(i) + ")", "tuner " + TunerRange(i)));
                foreach (var entry in _boards[i].GetHardwareInfo() ?? new List<KeyValuePair<string, string>>())
                {
                    if (entry.Key == "Source")
                        continue;
                    info.Add(new KeyValuePair<string, string>("  " + entry.Key, entry.Value));
                }
            }
            return info;
        }
    }
}
