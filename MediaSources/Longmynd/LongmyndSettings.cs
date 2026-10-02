using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace opentuner.MediaSources.Longmynd
{
    public class LongmyndSettings
    {
        // websocket
        public string LongmyndWSHost = "192.168.0.109";        
        public int LongmyndWSPort = 8080;

        // mqtt
        public string LongmyndMqttHost = "192.168.0.178";
        public int LongmyndMqttPort = 1883;
        public string CmdTopic = "cmd/longmynd/";

        // general
        public int TS_Port = 1234;

        // address Longmynd sends the TS to and OpenTuner listens on: empty = this PC's first detected IPv4 address
        // (Auto), a multicast address (224.0.0.0 - 239.255.255.255) makes OpenTuner join that group
        public string TS_Address = "";

        // diagnostics: only receive the TS on TS_Address:TS_Port (e.g. streamed by a MiniTiouner), no Longmynd control
        public bool TestMode = false;

        public byte DefaultInterface = 0;   // 0 = ws 1 = mqtt

        public uint Offset1 = 9750000;
        public uint DefaultVolume = 50;
        public bool DefaultMuted = true;

    }
}
