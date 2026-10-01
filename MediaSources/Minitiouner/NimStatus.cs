using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace opentuner
{
    public class TunerStatus
    {
        // nim specific
        public bool lna_top_ok { get; set; }
        public bool lna_bottom_ok { get; set; }
        public UInt32 errors_ldpc_count { get; set; }
        public byte chip_mid { get; set; }          // MID / DID registers, read once at init
        public byte chip_did { get; set; }
        public bool pll_locked { get; set; }        // PLLSTAT.PLLLOCK
        public uint mclk_hz { get; set; } = 135000000;  // demodulator master clock (lowered for low symbol rates), for the TS bit rate
        public byte bcherr { get; set; }            // raw BCHERR register: bit 4 ERRORFLAG, bits 3..0 BCH_ERRORS_COUNTER (chip-wide)

        // average time between two status polls in ms (NimThread loop incl. its 200 ms pause)
        public uint refresh_ms { get; set; }

        // software-measured time from scan start (or lost lock) to lock in ms, -1 = not locked yet
        public double T1P2_lock_time_ms { get; set; } = -1;
        public double T2P1_lock_time_ms { get; set; } = -1;


        // tuner 1 - demod 2 (TS2)(P2)

        public byte T1P2_demod_status { get; set; }
        public byte T1P2_dstatus { get; set; }      // DSTATUS: CAR_LOCK / TMGLOCK_QUALITY / LOCK_DEFINITIF
        public byte T1P2_dstatus2 { get; set; }     // DSTATUS2: DEMOD_DELOCK / AGC1..GAMMA failure flags
        public Int32 T1P2_carrier_low_hz { get; set; }      // derotator search range CFRLOW..CFRUP in Hz
        public Int32 T1P2_carrier_up_hz { get; set; }
        public byte T1P2_ldpc_iterations { get; set; }      // STATUSITER: iterations on the last frame
        public byte T1P2_ldpc_max_iterations { get; set; }  // STATUSMAXITER: maximum since the last read
        public sbyte T1P2_ldi { get; set; }         // carrier lock indicator accumulator
        public ushort T1P2_tmglock { get; set; }    // timing lock indicator accumulator
        public byte T1P2_pdelstatus1 { get; set; }  // PDELSTATUS1: PKTDELIN_LOCK / FIRST_LOCK / BCH_ERROR_FLAG (bits 3 and 6..4 cleared by the read)
        public bool T1P2_spectrum_inverted { get; set; }    // PLHMODCOD.SPECINV_DEMOD
        public ushort T1P2_ts_bitrate_raw { get; set; }     // TSBITRATE: bit rate = 135 MHz * raw / 16384
        public ushort T1P2_noise { get; set; }      // NNOSPLHT (S2) / NNOSDATAT (S): noise amplitude, 0x4000 = as strong as the signal
        public UInt32 T1P2_ts_status { get; set; }
        public UInt32 T1P2_stream_format { get; set; }
        public ushort T1P2_lna_gain { get; set; }
        public byte T1P2_power_i { get; set; }
        public byte T1P2_power_q { get; set; }
        public Int32 T1P2_mer { get; set; }
        public bool T1P2_short_frame { get; set; }
        public bool T1P2_pilots { get; set; }
        public Int32 T1P2_frequency_carrier_offset { get; set; }
        public UInt32 T1P2_symbol_rate { get; set;  }
        public UInt32 T1P2_modcode { get; set; }
        public byte T1P2_puncture_rate { get; set; }
        public bool T1P2_errors_bch_uncorrected { get; set; }
        public UInt32 T1P2_viterbi_error_rate { get; set; }
        public UInt32 T1P2_ber { get; set; }
        public UInt32 T1P2_errors_bch_count { get; set; }
        public ushort T1P2_agc1_gain { get; set; }
        public ushort T1P2_agc2_gain { get; set; }
        public short T1P2_input_power_level { get; set; }
        public bool T1P2_build_queue { get; set; }
        public bool T1P2_reset { get; set; }
        public byte[,] T1P2_constellation { get; set; }
        public sbyte[] T1P2_equalizer_dfe { get; set; }     // equalizer coefficients (Chip tab), null = not locked
        public sbyte[] T1P2_equalizer_ffe { get; set; }
        public int[] T1P2_iq_compensation { get; set; }     // DC I, DC Q, amplitude, quadrature (Chip tab), null = not locked
        public byte T1P2_rf_input { get; set; }
        public uint T1P2_requested_frequency { get; set; }

        public byte T1P2_rolloff { get; set; }

        // decoded TSSTATUS - software equivalent of the schematic's (unused) BC3_1/BC3_2
        // hardware TS_VALID/TS_ERR signals, read directly via I2C instead.
        public bool T1P2_ts_line_ok { get; set; }
        public bool T1P2_ts_error { get; set; }
        public bool T1P2_ts_nosync { get; set; }

        // tuner 2 - demod 1 (TS1)(P1)
        public byte T2P1_demod_status { get; set; }
        public byte T2P1_dstatus { get; set; }
        public byte T2P1_dstatus2 { get; set; }
        public Int32 T2P1_carrier_low_hz { get; set; }
        public Int32 T2P1_carrier_up_hz { get; set; }
        public byte T2P1_ldpc_iterations { get; set; }
        public byte T2P1_ldpc_max_iterations { get; set; }
        public sbyte T2P1_ldi { get; set; }
        public ushort T2P1_tmglock { get; set; }
        public byte T2P1_pdelstatus1 { get; set; }
        public bool T2P1_spectrum_inverted { get; set; }
        public ushort T2P1_ts_bitrate_raw { get; set; }
        public ushort T2P1_noise { get; set; }
        public UInt32 T2P1_ts_status { get; set; }
        public UInt32 T2P1_stream_format { get; set; }
        public ushort T2P1_lna_gain { get; set; }
        public byte T2P1_power_i { get; set; }
        public byte T2P1_power_q { get; set; }
        public Int32 T2P1_mer { get; set; }
        public bool T2P1_short_frame { get; set; }
        public bool T2P1_pilots { get; set; }
        public Int32 T2P1_frequency_carrier_offset { get; set; }
        public UInt32 T2P1_symbol_rate { get; set; }
        public UInt32 T2P1_modcode { get; set; }
        public byte T2P1_puncture_rate { get; set; }
        public bool T2P1_errors_bch_uncorrected { get; set; }
        public UInt32 T2P1_viterbi_error_rate { get; set; }
        public UInt32 T2P1_ber { get; set; }
        public UInt32 T2P1_errors_bch_count { get; set; }
        public ushort T2P1_agc1_gain { get; set; }
        public ushort T2P1_agc2_gain { get; set; }
        public short T2P1_input_power_level { get; set; }
        public bool T2P1_build_queue { get; set; }
        public bool T2P1_reset { get; set; }
        public byte[,] T2P1_constellation { get; set; }
        public sbyte[] T2P1_equalizer_dfe { get; set; }
        public sbyte[] T2P1_equalizer_ffe { get; set; }
        public int[] T2P1_iq_compensation { get; set; }
        public byte T2P1_rf_input { get; set; }
        public uint T2P1_requested_frequency { get; set; }
        public byte T2P1_rolloff { get; set; }

        public bool T2P1_ts_line_ok { get; set; }
        public bool T2P1_ts_error { get; set; }
        public bool T2P1_ts_nosync { get; set; }

    }
}
