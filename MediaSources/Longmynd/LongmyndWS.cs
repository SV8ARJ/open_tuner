using Newtonsoft.Json;
using opentuner.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using System.Drawing;
using Serilog;

namespace opentuner.MediaSources.Longmynd
{
    public partial class LongmyndSource
    {
        // ws interface
        private WebSocket controlWS;        // longmynd control ws websocket
        private WebSocket monitorWS;        // longmynd monitor ws websocket

        // Set by Close() so the OnClose handlers below stop reconnecting once the app is shutting
        // down (issue #16) - otherwise our own Close() triggers yet another Connect().
        private volatile bool _closing;

        // Pause between reconnect attempts. A refused connection returns immediately, so without
        // it an unreachable host would spin; a timing-out one (no route) already takes ~20 s.
        private const int ReconnectDelayMs = 3000;


        public void WSSetTS(string ip, int port)
        {
            Log.Information(ip.ToString() + " - " + port.ToString());
            controlWS.Send("U" + ip + ":" + port);
        }

        private void WSSetFrequency(uint frequency, uint symbol_rate)
        {
            controlWS.Send("C" + (frequency - _settings.Offset1).ToString() + "," + symbol_rate.ToString());
        }

        private void connectWebsockets()
        {
            _closing = false;   // a re-connect after Close() must reconnect again

            string url = "ws://" + _settings.LongmyndWSHost + ":" + _settings.LongmyndWSPort.ToString() + "/ ";

            monitorWS = new WebSocket(url, "monitor");
            monitorWS.OnOpen += Monitorws_OnOpen;
            monitorWS.OnMessage += Monitorws_OnMessage;
            monitorWS.OnClose += Monitorws_OnClose;
            System.Threading.Tasks.Task.Run(() => monitorWS.Connect());

            controlWS = new WebSocket(url, "control");
            controlWS.OnClose += Controlws_OnClose;
            controlWS.OnMessage += Controlws_OnMessage;
            controlWS.OnOpen += Controlws_OnOpen;
            System.Threading.Tasks.Task.Run(() => controlWS.Connect());
        }

        private void Monitorws_OnOpen(object sender, EventArgs e)
        {
            debug("Success: Monitor WS Open");
            _connected = true;
        }



        public void debug(string msg)
        {
            Log.Information(msg);
        }

        private void Controlws_OnOpen(object sender, EventArgs e)
        {
            debug("Success: Control WS Open");
        }


        private void Controlws_OnMessage(object sender, MessageEventArgs e)
        {
        }

        private void Controlws_OnClose(object sender, CloseEventArgs e)
        {
            if (_closing)
                return;

            debug("Error: Control WS Closed - Check WS IP");
            Reconnect(controlWS);
        }

        private void Monitorws_OnClose(object sender, CloseEventArgs e)
        {
            if (_closing)
                return;

            debug("Error: Monitor WS Closed - Check WS IP");
            Reconnect(monitorWS);
        }

        // Reconnects in the background after a short pause: Connect() blocks until the attempt
        // succeeds or times out, which must not happen on the WebSocket's own event thread.
        private void Reconnect(WebSocket ws)
        {
            debug("Attempting to reconnect...");
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(ReconnectDelayMs);
                    if (!_closing)
                        ws.Connect();
                }
                catch (Exception ex)
                {
                    Log.Warning("Longmynd WS reconnect failed: " + ex.Message);
                }
            });
        }

        // Closes both sockets off the UI thread: Close() waits for a connect attempt that is still
        // in flight (up to ~20 s if the host is unreachable), which froze the window on exit.
        private void CloseWebsockets()
        {
            _closing = true;
            var monitor = monitorWS;
            var control = controlWS;
            Task.Run(() =>
            {
                try { monitor?.Close(); } catch (Exception ex) { Log.Warning("Longmynd monitor WS close failed: " + ex.Message); }
                try { control?.Close(); } catch (Exception ex) { Log.Warning("Longmynd control WS close failed: " + ex.Message); }
            });
        }

        private void Monitorws_OnMessage(object sender, MessageEventArgs e)
        {
            monitorMessage mm = JsonConvert.DeserializeObject<monitorMessage>(e.Data);

            if (demodState != mm.packet.rx.demod_state)
            {
                if (mm.packet.rx.demod_state < 3)
                {
                    Log.Information("Stopping");
                    VideoChangeCB?.Invoke(1, false);
                    playing = false;
                }
                else
                {
                    Log.Information("Playing");
                    VideoChangeCB?.Invoke(1, true);
                    playing = true;
                }

                demodState = mm.packet.rx.demod_state;
            }


            current_frequency_0 = (uint)mm.packet.rx.frequency;
            current_sr_0 = (uint)mm.packet.rx.symbolrate;

            UpdatePropertiesWs(mm);

        }

        private void UpdatePropertiesWs(monitorMessage monitor_message)
        {
            double mer = Convert.ToDouble(monitor_message.packet.rx.mer) / 10;
            double db_margin = 0;
            string modcod_text = "";

            _tuner1_properties.UpdateValue("requested_freq", "(" + GetFrequency(0, true).ToString("N0") + ") (" + GetFrequency(0, false).ToString("N0") + ")");
            _tuner1_properties.UpdateValue("symbol_rate", (monitor_message.packet.rx.symbolrate / 1000).ToString());
            _tuner1_properties.UpdateValue("demodstate", demod_state_lookup[monitor_message.packet.rx.demod_state]);
            _tuner1_properties.UpdateValue("ber", monitor_message.packet.rx.ber.ToString());

            if (monitor_message.packet.rx.demod_state < 3)
                _tuner1_properties.UpdateColor("demodstate", Color.PaleVioletRed);
            else
                _tuner1_properties.UpdateColor("demodstate", Color.PaleGreen);


            try
            {
                switch (monitor_message.packet.rx.demod_state)
                {
                    case 3:
                        modcod_text = modcod_lookup_dvbs[monitor_message.packet.rx.modcod];
                        db_margin = (mer - modcod_lookup_dvbs_threshold[monitor_message.packet.rx.modcod]);
                        break;
                    case 4:
                        modcod_text = modcod_lookup_dvbs2[monitor_message.packet.rx.modcod];
                        db_margin = (mer - modcod_lookup_dvbs2_threshold[monitor_message.packet.rx.modcod]);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                debug("Unknown ModCod : " + ex.Message);
                debug(monitor_message.packet.rx.modcod.ToString());
            }

            last_mer_0 = mer.ToString();
            last_dbm_0 = "D" + db_margin.ToString("N1").ToString();
            _tuner1_properties.UpdateBigLabel("D" + db_margin.ToString("N1"));
            //_tuner1_properties.UpdateValue("db_margin", "D" + db_margin.ToString("N1"));
            _tuner1_properties.UpdateValue("modcod", modcod_text);
            _tuner1_properties.UpdateValue("mer", mer.ToString() + " dB");

            _tuner1_properties.UpdateValue("rf_input", (monitor_message.packet.rx.rfport == 0 ? "A" : "B"));

            _tuner1_properties.UpdateValue("service_name_provider", monitor_message.packet.ts.service_provider_name);
            _tuner1_properties.UpdateValue("service_name", monitor_message.packet.ts.service_name);

            last_service_name_0 = monitor_message.packet.ts.service_name;
            last_service_provider_0 = monitor_message.packet.ts.service_provider_name;

            _tuner1_properties.UpdateValue("null_packets", monitor_message.packet.ts.null_ratio + "%");

            _source_properties.UpdateValue("source_ts_ip", monitor_message.packet.rx.ts_ip_addr + ":" + monitor_message.packet.rx.ts_ip_port.ToString());

            if (_TsAddress + ":" + _settings.TS_Port != monitor_message.packet.rx.ts_ip_addr + ":" + monitor_message.packet.rx.ts_ip_port.ToString())
            {
                _source_properties.UpdateColor("source_ts_ip", Color.PaleVioletRed);
            }
            else
            {
                _source_properties.UpdateColor("source_ts_ip", Color.Bisque);
            }

            //_source_properties.UpdateValue("source_ip", _settings.LongmyndWSHost);

            // lost lock
            if (monitor_message.packet.rx.demod_state < 3)
            {
                _tuner1_properties.UpdateValue("service_name_provider", "");
                _tuner1_properties.UpdateValue("service_name", "");
                _tuner1_properties.UpdateValue("stream_format", "");

                _tuner1_properties.UpdateValue("video_codec", "");
                _tuner1_properties.UpdateValue("video_resolution", "");
                _tuner1_properties.UpdateValue("audio_codec", "");
                _tuner1_properties.UpdateValue("audio_rate", "");

                // stop recording if we lost lock
                if (_recorder.record)
                {
                    _recorder.record = false;    // stop recording
//                    ClearIndicator(ref indicatorStatus, PropertyIndicators.RecordingIndicator);
                    _tuner1_properties.UpdateRecordButtonColor("media_controls_1", Color.Transparent);
                    _tuner1_properties.UpdateValue("media_controls", indicatorStatus.ToString());
                }

                // stop streaming if we lost lock
                if (_streamer.stream)
                {
                    _streamer.stream = false;
//                    ClearIndicator(ref indicatorStatus, PropertyIndicators.StreamingIndicator);
                    _tuner1_properties.UpdateStreamButtonColor("media_controls_1", Color.Transparent);
                    _tuner1_properties.UpdateValue("media_controls", indicatorStatus.ToString());
                }

            }

        }

    }
}
