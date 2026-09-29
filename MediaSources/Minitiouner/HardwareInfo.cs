using System;
using System.Collections.Generic;
using Serilog;

namespace opentuner.MediaSources.Minitiouner
{
    // One FTDI device (= one channel of an FT2232H, or a foreign device) as the driver lists it.
    public class FtdiDeviceInfo
    {
        public int Index;
        public string Type;         // e.g. FT_DEVICE_2232H
        public string Description;
        public string Serial;
        public uint LocId;
        public bool IsOpen;         // opened by some program (OpenTuner itself once connected)
        public string Role;         // I2C / TS / TS2 / AUX, "-" = not used by OpenTuner
        public string Board;        // board type the description belongs to, "" = unknown
    }

    // Read-only view of what OpenTuner sees on the USB side, for the "Hardware Info" window.
    // Uses the driver's device list, which does not open the devices, so it also works while
    // OpenTuner is connected (the channels are then open and can't be opened a second time).
    public static class HardwareInfo
    {
        public static List<FtdiDeviceInfo> EnumerateFtdiDevices(out string error)
        {
            error = null;
            var result = new List<FtdiDeviceInfo>();

            try
            {
                var ftdi = new FTD2XX_NET.FTDI();
                uint count = 0;
                var status = ftdi.GetNumberOfDevices(ref count);
                if (status != FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                {
                    error = "FTDI driver: " + status;
                    return result;
                }

                if (count == 0)
                    return result;

                var nodes = new FTD2XX_NET.FTDI.FT_DEVICE_INFO_NODE[count];
                status = ftdi.GetDeviceList(nodes);
                if (status != FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                {
                    error = "FTDI driver: " + status;
                    return result;
                }

                for (int i = 0; i < nodes.Length; i++)
                {
                    var node = nodes[i];
                    var info = new FtdiDeviceInfo
                    {
                        Index = i,
                        Type = node.Type.ToString(),
                        Description = node.Description ?? "",
                        Serial = node.SerialNumber ?? "",
                        LocId = node.LocId,
                        IsOpen = (node.Flags & 1) != 0
                    };

                    if (node.Type == FTD2XX_NET.FTDI.FT_DEVICE.FT_DEVICE_2232H)
                        Classify(info.Description, out info.Role, out info.Board);
                    else
                    {
                        info.Role = "-";
                        info.Board = "";
                    }

                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Hardware info: FTDI device list failed");
                error = ex.Message;
            }

            return result;
        }

        // Same description rules as FTDIInterface.hw_detect (which assigns the ports at connect time).
        // Kept separate on purpose so the overview cannot influence the detection.
        public static void Classify(string description, out string role, out string board)
        {
            role = "-";
            board = "";

            if (string.IsNullOrEmpty(description))
                return;

            if (description.Contains("NIM DB tuner B")) { role = "TS2"; board = "BATC V2 Minitiouner"; }
            else if (description.Contains("NIM tuner A")) { role = "I2C"; board = "BATC V2 Minitiouner"; }
            else if (description.Contains("NIM tuner B")) { role = "TS"; board = "BATC V2 Minitiouner"; }
            else if (description.Contains("MiniTiouner_Pro_TS2 A")) { role = "I2C"; board = "Minitiouner Pro 2"; }
            else if (description.Contains("MiniTiouner_Pro_TS2 B")) { role = "TS"; board = "Minitiouner Pro 2"; }
            else if (description.Contains("MiniTiouner_Pro_TS1 B")) { role = "TS2"; board = "Minitiouner Pro 2"; }
            else if (description.Contains("MiniTiouner_Pro_TS1 A")) { role = "AUX"; board = "Minitiouner Pro 2"; }
            else if (description.Contains("MiniTiouner-Express A")) { role = "I2C"; board = "Minitiouner Express"; }
            else if (description.Contains("MiniTiouner-Express B")) { role = "TS"; board = "Minitiouner Express"; }
            else if (description.Contains("Minitiouner S A")) { role = "I2C"; board = "Minitiouner-S"; }
            else if (description.Contains("Minitiouner S B")) { role = "TS"; board = "Minitiouner-S"; }
            else if (description.Contains("MiniTiouner A")) { role = "I2C"; board = "Minitiouner-S"; }
            else if (description.Contains("MiniTiouner B")) { role = "TS"; board = "Minitiouner-S"; }
        }
    }
}
