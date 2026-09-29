// ported from longmynd - https://github.com/myorangedragon/longmynd - Heather Lomond

using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace opentuner
{

    class stv0910
    {
        public const byte KHZ22ON = 0;
        public const byte KHZ22OFF = 1;
        public const byte DEMOD_HUNTING = 0;
        public const byte DEMOD_FOUND_HEADER = 1;
        public const byte DEMOD_S2 = 2;
        public const byte DEMOD_S = 3;

        const byte STV0910_PLL_LOCK_TIMEOUT = 100;

        const byte STV0910_SCAN_BLIND_BEST_GUESS = 0x15;
        const byte STV0910_DEMOD_STOP = 0x1C;

        public const byte STV0910_DEMOD_TOP = 1;
        public const byte STV0910_DEMOD_BOTTOM = 2;

        const byte STV0910_PUNCTURE_1_2 = 0x0d;
        const byte STV0910_PUNCTURE_2_3 = 0x12;
        const byte STV0910_PUNCTURE_3_4 = 0x15;
        const byte STV0910_PUNCTURE_5_6 = 0x18;
        const byte STV0910_PUNCTURE_6_7 = 0x19;
        const byte STV0910_PUNCTURE_7_8 = 0x1a;

        nim nim_device;

        byte[] stv0910_shadow_regs = new byte[stv0910_regs.STV0910_END_ADDR - stv0910_regs.STV0910_START_ADDR + 1];

        // Chip identification read at init (MID 0xF100: [7:4] chip ident, [3:0] release; DID 0xF101: device id)
        public byte ChipMid { get; private set; }
        public byte ChipDid { get; private set; }

        private bool _enableSerialTS = false;

        private byte stv0910_init_regs(bool EnableSerialTS)
        {
            _enableSerialTS = EnableSerialTS;

            byte val1 = 0;
            byte val2 = 0;
            byte err = 0;
            ushort i = 0;

            Log.Information("Flow: STV0910 init regs");
            
            err = nim_device.nim_read_demod(0xf100, ref val1);

            if (err == 0) err = nim_device.nim_read_demod(0xf101, ref val2);

            ChipMid = val1;
            ChipDid = val2;
            Log.Information("      Status: STV0910 MID = {0}, DID = {1}", val1.ToString("X2"), val2.ToString("X2"));

            if ( (val1 != 0x51) || (val2 != 0x20 ))
            {
                return Errors.ERROR_DEMOD_INIT;
            }

            // init all registers in the list
            do
            {
                if (err == 0) err = stv0910_write_reg(stv0910_regs_init.STV0910DefVal[i].reg, stv0910_regs_init.STV0910DefVal[i].val);
            } while (stv0910_regs_init.STV0910DefVal[i++].reg != stv0910_regs.RSTV0910_TSTTSRS);

            // serial ts for pico tuner
            if (_enableSerialTS)
            {
                if (err == 0) stv0910_write_reg(stv0910_regs.RSTV0910_P1_TSCFGH, 0x40);
                if (err == 0) stv0910_write_reg(stv0910_regs.RSTV0910_P2_TSCFGH, 0x40);
            }


            // reset lpdc decoder
            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_TSTRES0, 0x80);
            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_TSTRES0, 0x00);

            return err;
        }

        private byte stv0910_setup_equalisers( byte demod )
        {
            Log.Information("Flow: Setup equializers: {0}", demod);
            return 0;
        }

        private byte stv0910_setup_carrier_loop(byte demod, UInt32 halfscan_sr)
        {
            byte err = 0;
            Int64 temp;


            Log.Information("Flow: Setup carrier loop: {0}" , demod);

            err = stv0910_write_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_CFRINIT0 : stv0910_regs.RSTV0910_P1_CFRINIT0, 0);

            if (err == 0)
            {
                err = stv0910_write_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_CFRINIT0 : stv0910_regs.RSTV0910_P1_CFRINIT0, 0);
            }

            // 0.6 * SR seems to give +/- 0.5 SR lock
            temp = (Int64)halfscan_sr * 65536 / (MclkHz / 1000);

            // CFRUP / CFRLOW are 16 bit signed registers
            if (temp > 32767)
                temp = 32767;

            // Upper Limit
            if (err == 0)
            {
                err = stv0910_write_reg((demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_CFRUP0 : stv0910_regs.RSTV0910_P1_CFRUP0), (byte)(temp & 0xff));
                err = stv0910_write_reg((demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_CFRUP1 : stv0910_regs.RSTV0910_P1_CFRUP1), (byte)((temp >> 8) & 0xff));
            }
            // the lower value is the negative of the upper value
            temp = -temp;

            if (err == 0)
            {
                err = stv0910_write_reg((demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_CFRLOW0 : stv0910_regs.RSTV0910_P1_CFRLOW0), (byte)(temp & 0xff));
                err = stv0910_write_reg((demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_CFRLOW1 : stv0910_regs.RSTV0910_P1_CFRLOW1), (byte)((temp >> 8) & 0xff));
            }

            return err;

        }

        public byte stv0910_read_ts_status(byte demod, ref UInt32 info)
        {
            byte err;
            byte temp0 = 0;
            byte temp1 = 0;

            err = 0;

            err |= stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_TSSTATUS : stv0910_regs.RSTV0910_P1_TSSTATUS, ref temp0);
            err |= stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_TSSTATUS2 : stv0910_regs.RSTV0910_P1_TSSTATUS2, ref temp1);

            info = (UInt32)(temp0 | (temp1 << 8));

            if (err != 0) Log.Information("ERROR: STV0910 read multistream0\r\n");

            return (err);
        }

        // Decoded TSSTATUS bits - software equivalent of the (unused) TS_VALID/TS_ERR/hardware
        // BC3 NAND-derived signal on the schematic, read directly from the demod's own status
        // register instead. line_ok mirrors TS_VALID, error mirrors TS_ERR, nosync indicates the
        // TS FIFO output clock has no sync.
        public byte stv0910_read_ts_status_decoded(byte demod, ref bool line_ok, ref bool error, ref bool nosync)
        {
            byte err = 0;
            byte val = 0;

            err |= stv0910_read_reg_field(demod == STV0910_DEMOD_TOP ? stv0910_regs.FSTV0910_P2_TSFIFO_LINEOK : stv0910_regs.FSTV0910_P1_TSFIFO_LINEOK, ref val);
            line_ok = val != 0;

            err |= stv0910_read_reg_field(demod == STV0910_DEMOD_TOP ? stv0910_regs.FSTV0910_P2_TSFIFO_ERROR : stv0910_regs.FSTV0910_P1_TSFIFO_ERROR, ref val);
            error = val != 0;

            err |= stv0910_read_reg_field(demod == STV0910_DEMOD_TOP ? stv0910_regs.FSTV0910_P2_TSFIFO_NOSYNC : stv0910_regs.FSTV0910_P1_TSFIFO_NOSYNC, ref val);
            nosync = val != 0;

            if (err != 0) Log.Information("ERROR: STV0910 read ts status decoded");

            return err;
        }

        byte stv0910_setup_timing_loop(byte demod, UInt32 sr)
        {
            byte err = 0;
            ushort sr_reg = 0;

            Log.Information("Flow: Setup timing loop {0}", demod);


            sr_reg = (ushort)(((((UInt32)sr) << 16) + (MclkHz / 2000)) / (MclkHz / 1000)); // sr in kS, rounded

            if (err == 0)
            {
                    err = stv0910_write_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_SFRINIT1 : stv0910_regs.RSTV0910_P1_SFRINIT1, (byte)(sr_reg >> 8));
            }

            if (err == 0)
            {
                    err = stv0910_write_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_SFRINIT0 : stv0910_regs.RSTV0910_P1_SFRINIT0, (byte)(sr_reg & 0xFF));
            }


            return err;
        }

        // Receiver options that differ between longmynd's table and MiniTioune (see MinitiounerSettings): carrier loop 1 phase detector
        // algorithm (CARCFG.PH_DET_ALGO, the table has 0x46 = citroen 2) and the I/Q swap after the ADCs (TNRCFG2.TUN_IQSWAP, the table has 0x02 = off).
        public byte CarrierPhaseAlgo = 2;
        public bool MiniTiouneInit = false;

        // Register values of MiniTioune's startup (I2C capture, before the first tune) that differ from our table for timing loop, carrier loop, FEC and
        // packet delineator settings: { register, value }. Written after our table when MiniTiouneInit is set. Not included: GPIO, TS output and status registers.
        private static readonly ushort[][] MiniTiouneInitRegs = new ushort[][]
        {
            new ushort[] { 0xF201, 0x14 }, // P2_NOSCFG (table 0x34)
            new ushort[] { 0xF210, 0x04 }, // P2_DEMOD (table 0x00)
            new ushort[] { 0xF23D, 0x28 }, // P2_CARFREQ (table 0x79)
            new ushort[] { 0xF253, 0x13 }, // P2_TMGTHRISE (table 0x1E)
            new ushort[] { 0xF25A, 0x81 }, // P2_TMGCFG2 (table 0x80)
            new ushort[] { 0xF25D, 0x00 }, // P2_TMGCFG3 (table 0x06)
            new ushort[] { 0xF2B1, 0xF3 }, // P2_MODCODLST1 (table 0xFC)
            new ushort[] { 0xF2B2, 0xFF }, // P2_MODCODLST2 (table 0x00)
            new ushort[] { 0xF2B3, 0xFF }, // P2_MODCODLST3 (table 0x00)
            new ushort[] { 0xF2B4, 0xFF }, // P2_MODCODLST4 (table 0x00)
            new ushort[] { 0xF2B5, 0xFF }, // P2_MODCODLST5 (table 0x00)
            new ushort[] { 0xF2B6, 0xFF }, // P2_MODCODLST6 (table 0x00)
            new ushort[] { 0xF2B7, 0xFF }, // P2_MODCODLST7 (table 0xC0)
            new ushort[] { 0xF2B8, 0xFF }, // P2_MODCODLST8 (table 0x00)
            new ushort[] { 0xF2B9, 0xFF }, // P2_MODCODLST9 (table 0x00)
            new ushort[] { 0xF2BA, 0xFF }, // P2_MODCODLSTA (table 0xC0)
            new ushort[] { 0xF2BB, 0xFF }, // P2_MODCODLSTB (table 0x00)
            new ushort[] { 0xF2BC, 0xFF }, // P2_MODCODLSTC (table 0x00)
            new ushort[] { 0xF2BD, 0xFF }, // P2_MODCODLSTD (table 0x00)
            new ushort[] { 0xF2BE, 0xFF }, // P2_MODCODLSTE (table 0x00)
            new ushort[] { 0xF2BF, 0xFF }, // P2_MODCODLSTF (table 0x0F)
            new ushort[] { 0xF354, 0x41 }, // P2_HYSTTHRESH (table not set)
            new ushort[] { 0xF401, 0x14 }, // P1_NOSCFG (table 0x34)
            new ushort[] { 0xF410, 0x04 }, // P1_DEMOD (table 0x00)
            new ushort[] { 0xF43D, 0x28 }, // P1_CARFREQ (table 0x79)
            new ushort[] { 0xF453, 0x13 }, // P1_TMGTHRISE (table 0x1E)
            new ushort[] { 0xF45A, 0x81 }, // P1_TMGCFG2 (table 0x80)
            new ushort[] { 0xF45D, 0x00 }, // P1_TMGCFG3 (table 0x06)
            new ushort[] { 0xF4B1, 0xF3 }, // P1_MODCODLST1 (table 0xFC)
            new ushort[] { 0xF4B2, 0xFF }, // P1_MODCODLST2 (table 0x00)
            new ushort[] { 0xF4B3, 0xFF }, // P1_MODCODLST3 (table 0x00)
            new ushort[] { 0xF4B4, 0x3F }, // P1_MODCODLST4 (table 0x00)
            new ushort[] { 0xF4B5, 0xFF }, // P1_MODCODLST5 (table 0x00)
            new ushort[] { 0xF4B6, 0xFF }, // P1_MODCODLST6 (table 0x00)
            new ushort[] { 0xF4B7, 0xFF }, // P1_MODCODLST7 (table 0xC0)
            new ushort[] { 0xF4B8, 0xFF }, // P1_MODCODLST8 (table 0x00)
            new ushort[] { 0xF4B9, 0xFF }, // P1_MODCODLST9 (table 0x00)
            new ushort[] { 0xF4BA, 0xFF }, // P1_MODCODLSTA (table 0xC0)
            new ushort[] { 0xF4BB, 0xFF }, // P1_MODCODLSTB (table 0x00)
            new ushort[] { 0xF4BC, 0xFF }, // P1_MODCODLSTC (table 0x00)
            new ushort[] { 0xF4BD, 0xFF }, // P1_MODCODLSTD (table 0x00)
            new ushort[] { 0xF4BE, 0xFF }, // P1_MODCODLSTE (table 0x00)
            new ushort[] { 0xF4BF, 0xFF }, // P1_MODCODLSTF (table 0x0F)
            new ushort[] { 0xF4D8, 0x00 }, // P1_FFECFG (table 0x71)
            new ushort[] { 0xF53C, 0x00 }, // P1_PRVIT (table 0x2F)
            new ushort[] { 0xF554, 0x41 }, // P1_HYSTTHRESH (table not set)
            new ushort[] { 0xFA51, 0x20 }, // GAINLLR_NF18 (table 0x22)
            new ushort[] { 0xFA52, 0x20 }, // GAINLLR_NF19 (table 0x22)
            new ushort[] { 0xFA53, 0x20 }, // GAINLLR_NF20 (table 0x24)
            new ushort[] { 0xFA54, 0x20 }, // GAINLLR_NF21 (table 0x24)
            new ushort[] { 0xFA55, 0x20 }, // GAINLLR_NF22 (table 0x25)
            new ushort[] { 0xFA56, 0x20 }, // GAINLLR_NF23 (table 0x26)
            new ushort[] { 0xFA86, 0x1E }, // GENCFG (table 0x15)
        };
        public bool IqSwap = false;

        private byte stv0910_apply_receiver_options()
        {
            byte err = 0;
            byte carcfg = (byte)((0x46 & 0xFC) | (CarrierPhaseAlgo & 0x03));
            byte tnrcfg2 = (byte)(0x02 | (IqSwap ? 0x80 : 0x00));

            Log.Information("Flow: STV0910 carrier phase algorithm {0}, I/Q swap {1}", CarrierPhaseAlgo, IqSwap);

            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_P1_CARCFG, carcfg);
            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_P2_CARCFG, carcfg);
            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_P1_TNRCFG2, tnrcfg2);
            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_P2_TNRCFG2, tnrcfg2);

            if (MiniTiouneInit)
            {
                Log.Information("Flow: STV0910 MiniTioune init values ({0} registers)", MiniTiouneInitRegs.Length);
                foreach (ushort[] entry in MiniTiouneInitRegs)
                {
                    if (err == 0) err = stv0910_write_reg(entry[0], (byte)entry[1]);
                }
            }

            return err;
        }

        private byte stv0910_setup_clocks()
        {
            byte err = 0;

            UInt32 ndiv;
            byte odf;
            byte idf;
            UInt32 f_phi;
            UInt32 f_xtal;
            byte cp;
            byte _lock = 0;
            ushort timeout = 16;

            Log.Information("Flow: STV0910 set MCLK");

            idf = 1;
            f_xtal = nim.NIM_TUNER_XTAL / 1000; /* in MHz */
            f_phi = MclkHz / 1000000;

            if (MclkHz >= 100000000)
            {
                odf = 4;
                ndiv = (f_phi * odf * idf) / f_xtal;
            }
            else
            {
                // low symbol rate clock, 30 MHz * 71 / 4 / 13 = 40.96 MHz (NDIV 71 is the top of the CP = 7 range; the PLL core runs at
                // 266 MHz, close to the 270 MHz default). The timing loop minimum MCLK / 2048 is then 20 kS.
                ndiv = 11;
                idf = 2;
                odf = 4;
            }

            Log.Information("Flow: STV0910 MCLK {0} MHz (NDIV {1}, IDF {2}, ODF {3})", f_phi, ndiv, idf, odf);

            if (err == 0) err = stv0910_write_reg_field(stv0910_regs.FSTV0910_ODF, odf);
            if (err == 0) err = stv0910_write_reg_field(stv0910_regs.FSTV0910_IDF, idf);
            if (err == 0) err = stv0910_write_reg_field(stv0910_regs.FSTV0910_N_DIV, (byte)ndiv);

            /* Set CP according to NDIV */
            cp = 7;
            if (err == 0) err = stv0910_write_reg_field(stv0910_regs.FSTV0910_CP, cp);

            /* turn on all the clocks */
            if (err == 0) err = stv0910_write_reg_field(stv0910_regs.FSTV0910_STANDBY, 0);

            /* derive clocks from PLL */
            if (err == 0) err = stv0910_write_reg_field(stv0910_regs.FSTV0910_BYPASSPLLCORE, 0);

            /* wait for PLL to lock */
            do
            {
                timeout++;
                if (timeout == STV0910_PLL_LOCK_TIMEOUT)
                {
                    err = Errors.ERROR_DEMOD_PLL_TIMEOUT;
                    //printf("ERROR: STV0910 pll lock timeout\n");
                }
                if (err == 0) stv0910_read_reg_field(stv0910_regs.FSTV0910_PLLLOCK, ref _lock) ;
            } while ((err == 0) && (_lock== 0));

            //if (err != ERROR_NONE) printf("ERROR: STV0910 set MCLK\n");

            return err;
        }

        public stv0910(nim _nim_device)
        {
            nim_device = _nim_device;
        }


        public byte stv0910_init(bool EnableSerialTS)
        {
            byte err = 0;

            Log.Information("Flow: STV0910 init");

            // stop demodulators
            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_P1_DMDISTATE, 0x1c);
            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_P2_DMDISTATE, 0x1c);

            // non demodulator specific
            if (err == 0) err = stv0910_init_regs(EnableSerialTS);
            if (err == 0) err = stv0910_apply_receiver_options();
            if (err == 0) err = stv0910_setup_clocks();

            return err;
        }

        // setup receive of the demodulator
        // capture_range_khz: carrier search range of the derotator in kHz on each side of the tuned frequency,
        // 0 = automatic: 1.5 x symbol rate, but at least MinAutoCaptureKHz - for a low symbol rate 1.5 x SR is only
        // +-30..50 kHz, which a drifting LNB (or a spectrum click a few kHz off) easily leaves.
        private const UInt32 MinAutoCaptureKHz = 100;

        // Master clock. The timing loop cannot go below MCLK / 2048 (TMGCFG.TMG_MINFREQ = 11, the lowest setting): at 135 MHz
        // that is 65.9 kS, SFRINIT is clamped to it and a 25 kS signal is never found (measured: SFRINIT 0x0020, SFRUP 0x0024).
        // Below LowSrBelowKS the master clock is lowered to 30 MHz (minimum 14.6 kS), presumably what MiniTioune's "Low SR" does.
        // Both demodulators share the clock, so the other one is set up again when it changes.
        public uint MclkHz = 135000000;
        public bool AllowLowSrClock = true;
        private const uint LowSrMclkHz = 41250000;   // 30 MHz * 11 / 2 / 4, as MiniTioune (I2C capture of its "Low SR" switch); 41.25 MHz / 2048 = 20.1 kS
        private const UInt32 LowSrBelowKS = 50;      // 20 / 25 / 33 kS; 66 kS and above keep the normal clock and setup
        public bool LowSrProfile = true;      // MiniTioune's demodulator setup for low symbol rates (see stv0910_setup_low_sr)
        public bool LowSrManualSfr = true;    // manual SFRUP / SFRLOW (+-5 %, TMGCFG3 = 0x00) in the low symbol rate profile; off = the chip's automatic window
        public bool LowSrSrScan = false;      // DMDCFGMD.SCAN_ENABLE in the low symbol rate profile: 0x8B fixed rate (default: 20 .. 125 kS lock with it), 0x9B scanning (pushed the SFR out of its window at 20 kS)
        public bool LowSrDvbS1 = false;       // keep DVB-S1 search enabled in the low symbol rate profile (MiniTioune: DVB-S2 only)
        private readonly int[] lowsr_offset_hz = new int[2];
        private readonly int[] lowsr_cfr_up = new int[2];       // CFRUP (register units) of the low symbol rate profile, written again after the state machine reset
        private readonly byte[] lowsr_dmdcfgmd = new byte[2];
        private readonly bool[] lowsr_applied = new bool[2];

        // The tuner is set this far below the wanted frequency for a low symbol rate (1.5 x SR): a narrow carrier at zero IF sits on the DC
        // offset loop of the tuner. The derotator then looks for the carrier at +1.5 x SR. Used for the tuner frequency and the CFR window.
        public int LowSrOffsetKHz(uint sr_kS)
        {
            return (LowSrProfile && AllowLowSrClock && sr_kS < LowSrBelowKS) ? (int)Math.Round(1.5 * sr_kS) : 0;
        }

        // Offset in Hz that the demodulator (0 = top, 1 = bottom) currently expects the carrier to have, to be removed from CFR readings.
        public int AppliedLowSrOffsetHz(int index)
        {
            return lowsr_offset_hz[index];
        }
        private readonly UInt32[] last_sr = new UInt32[2];
        private readonly UInt32[] last_capture = new UInt32[2];
        private readonly bool[] receive_configured = new bool[2];

        private byte stv0910_configure_receive(byte demod, UInt32 sr, UInt32 capture_range_khz)
        {
            byte err = 0;
            int index = demod == STV0910_DEMOD_TOP ? 0 : 1;

            if (err == 0) err = stv0910_setup_equalisers(demod);
            if (err == 0) err = stv0910_setup_carrier_loop(demod, capture_range_khz > 0 ? capture_range_khz : Math.Max(Convert.ToUInt32(sr * 1.5), MinAutoCaptureKHz));
            if (err == 0) err = stv0910_setup_timing_loop(demod, sr);

            bool low = MclkHz < 100000000 && LowSrOffsetKHz(sr) > 0;
            if (err == 0 && low)
                err = stv0910_setup_low_sr(demod, sr);
            else if (err == 0 && lowsr_applied[index])
                err = stv0910_restore_standard_search(demod);

            lowsr_offset_hz[index] = low ? LowSrOffsetKHz(sr) * 1000 : 0;
            lowsr_applied[index] = low;

            return err;
        }

        // The demodulator setup MiniTioune writes for 25 kS (I2C capture of a locking MiniTiouner, values seen for 40.96 MHz MCLK):
        // DMDCFGMD 0x9B = DVB-S2 only, symbol rate scan on, CFR autoscan, tuner range 3; SFRINIT = SR, SFRUP / SFRLOW = +-5 % (manual);
        // carrier window CFRLOW = 0, CFRINIT = CFRIBASE = 1.5 x SR, CFRUP = 3 x SR; CARFREQ 0xAC, CARHDR 0x40, CAR2CFG 0x26.
        private byte stv0910_setup_low_sr(byte demod, UInt32 sr)
        {
            bool top = demod == STV0910_DEMOD_TOP;
            byte err = 0;
            double mclk = MclkHz;
            const int SfrMinUnits = 32;                        // 65536 / 2048: the timing loop minimum is MCLK / 2048 whatever the clock
            int sfr_init = Math.Max(SfrMinUnits, (int)Math.Round(sr * 1000.0 * 65536.0 / mclk));
            int sfr_up = Math.Max(sfr_init + 1, (int)Math.Round((sr * 1000.0 * 1.05) * 65536.0 / mclk));
            int sfr_low = Math.Max(SfrMinUnits, (int)Math.Round((sr * 1000.0 * 0.95) * 65536.0 / mclk));
            int cfr_init = (int)Math.Round(LowSrOffsetKHz(sr) * 1000.0 * 65536.0 / mclk);
            int cfr_up = 2 * cfr_init;
            int index = top ? 0 : 1;

            Log.Information("Flow: STV0910 low symbol rate setup {0}: SFR {1} ({2}..{3}), CFR init {4} up {5}", demod, sfr_init, sfr_low, sfr_up, cfr_init, cfr_up);

            ushort dmdcfgmd = top ? stv0910_regs.RSTV0910_P2_DMDCFGMD : stv0910_regs.RSTV0910_P1_DMDCFGMD;
            ushort sfrinit1 = top ? stv0910_regs.RSTV0910_P2_SFRINIT1 : stv0910_regs.RSTV0910_P1_SFRINIT1;
            ushort sfrinit0 = top ? stv0910_regs.RSTV0910_P2_SFRINIT0 : stv0910_regs.RSTV0910_P1_SFRINIT0;
            ushort sfrup1 = top ? stv0910_regs.RSTV0910_P2_SFRUP1 : stv0910_regs.RSTV0910_P1_SFRUP1;
            ushort sfrup0 = top ? stv0910_regs.RSTV0910_P2_SFRUP0 : stv0910_regs.RSTV0910_P1_SFRUP0;
            ushort sfrlow1 = top ? stv0910_regs.RSTV0910_P2_SFRLOW1 : stv0910_regs.RSTV0910_P1_SFRLOW1;
            ushort sfrlow0 = top ? stv0910_regs.RSTV0910_P2_SFRLOW0 : stv0910_regs.RSTV0910_P1_SFRLOW0;
            ushort cfrinit1 = top ? stv0910_regs.RSTV0910_P2_CFRINIT1 : stv0910_regs.RSTV0910_P1_CFRINIT1;
            ushort cfrinit0 = top ? stv0910_regs.RSTV0910_P2_CFRINIT0 : stv0910_regs.RSTV0910_P1_CFRINIT0;
            ushort cfribase1 = top ? stv0910_regs.RSTV0910_P2_CFRIBASE1 : stv0910_regs.RSTV0910_P1_CFRIBASE1;
            ushort cfribase0 = top ? stv0910_regs.RSTV0910_P2_CFRIBASE0 : stv0910_regs.RSTV0910_P1_CFRIBASE0;
            ushort cfrup1 = top ? stv0910_regs.RSTV0910_P2_CFRUP1 : stv0910_regs.RSTV0910_P1_CFRUP1;
            ushort cfrup0 = top ? stv0910_regs.RSTV0910_P2_CFRUP0 : stv0910_regs.RSTV0910_P1_CFRUP0;
            ushort cfrlow1 = top ? stv0910_regs.RSTV0910_P2_CFRLOW1 : stv0910_regs.RSTV0910_P1_CFRLOW1;
            ushort cfrlow0 = top ? stv0910_regs.RSTV0910_P2_CFRLOW0 : stv0910_regs.RSTV0910_P1_CFRLOW0;

            lowsr_cfr_up[index] = cfr_up;
            lowsr_dmdcfgmd[index] = (byte)((LowSrDvbS1 ? 0xCB : 0x8B) | (LowSrSrScan ? 0x10 : 0x00));
            if (err == 0) err = stv0910_write_reg(dmdcfgmd, lowsr_dmdcfgmd[index]);

            // MiniTioune's startup writes TMGCFG3 = 0x00, our table 0x06: bits 2 / 1 are AUTO_GUP / AUTO_GLOW. With them set the chip calculates SFRUP / SFRLOW
            // itself from SFRUPRATIO / SFRLOWRATIO (measured: 0x2D / 0x21 instead of our 0x2A / 0x26). TMGTHRISE 0x13 (table 0x1E) is MiniTioune's value as well.
            // CFRICFG stays 0xF9: with MiniTioune's 0xF8 (positive step direction) the carrier search was a slow linear ramp up from the lower window edge (about 20 s
            // per pass) instead of going back and forth around the window centre where the carrier is.
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_TMGCFG3 : stv0910_regs.RSTV0910_P1_TMGCFG3, (byte)(LowSrManualSfr ? 0x00 : 0x06));
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_TMGTHRISE : stv0910_regs.RSTV0910_P1_TMGTHRISE, 0x13);
            if (err == 0) err = stv0910_write_reg(sfrinit1, (byte)(sfr_init >> 8));
            if (err == 0) err = stv0910_write_reg(sfrinit0, (byte)(sfr_init & 0xFF));
            if (LowSrManualSfr)
            {
                if (err == 0) err = stv0910_write_reg(sfrup1, (byte)((sfr_up >> 8) & 0x7F));      // bit 7 = 0: manual limits
                if (err == 0) err = stv0910_write_reg(sfrup0, (byte)(sfr_up & 0xFF));
                if (err == 0) err = stv0910_write_reg(sfrlow1, (byte)((sfr_low >> 8) & 0x7F));
                if (err == 0) err = stv0910_write_reg(sfrlow0, (byte)(sfr_low & 0xFF));
            }
            if (err == 0) err = stv0910_write_reg(cfrinit1, (byte)(cfr_init >> 8));
            if (err == 0) err = stv0910_write_reg(cfrinit0, (byte)(cfr_init & 0xFF));
            if (err == 0) err = stv0910_write_reg(cfribase1, (byte)(cfr_init >> 8));
            if (err == 0) err = stv0910_write_reg(cfribase0, (byte)(cfr_init & 0xFF));
            if (err == 0) err = stv0910_write_reg(cfrup1, (byte)(cfr_up >> 8));
            if (err == 0) err = stv0910_write_reg(cfrup0, (byte)(cfr_up & 0xFF));
            if (err == 0) err = stv0910_write_reg(cfrlow1, 0x00);
            if (err == 0) err = stv0910_write_reg(cfrlow0, 0x00);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CARFREQ : stv0910_regs.RSTV0910_P1_CARFREQ, 0xAC);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CARHDR : stv0910_regs.RSTV0910_P1_CARHDR, 0x40);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CAR2CFG : stv0910_regs.RSTV0910_P1_CAR2CFG, 0x26);

            return err;
        }

        // Back to the values of the register table after a low symbol rate setup.
        private byte stv0910_restore_standard_search(byte demod)
        {
            bool top = demod == STV0910_DEMOD_TOP;
            byte err = 0;

            Log.Information("Flow: STV0910 back to the standard search setup {0}", demod);

            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_DMDCFGMD : stv0910_regs.RSTV0910_P1_DMDCFGMD, 0xC9);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_TMGCFG3 : stv0910_regs.RSTV0910_P1_TMGCFG3, (byte)(MiniTiouneInit ? 0x00 : 0x06));
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_TMGTHRISE : stv0910_regs.RSTV0910_P1_TMGTHRISE, (byte)(MiniTiouneInit ? 0x13 : 0x1E));
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_SFRUP1 : stv0910_regs.RSTV0910_P1_SFRUP1, 0x3F);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_SFRUP0 : stv0910_regs.RSTV0910_P1_SFRUP0, 0xFF);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_SFRLOW1 : stv0910_regs.RSTV0910_P1_SFRLOW1, 0x2E);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_SFRLOW0 : stv0910_regs.RSTV0910_P1_SFRLOW0, 0x39);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CFRIBASE1 : stv0910_regs.RSTV0910_P1_CFRIBASE1, 0x01);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CFRIBASE0 : stv0910_regs.RSTV0910_P1_CFRIBASE0, 0xF5);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CARFREQ : stv0910_regs.RSTV0910_P1_CARFREQ, (byte)(MiniTiouneInit ? 0x28 : 0x79));
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CARHDR : stv0910_regs.RSTV0910_P1_CARHDR, 0x1C);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CAR2CFG : stv0910_regs.RSTV0910_P1_CAR2CFG, 0x06);

            return err;
        }

        // Both demodulators run from the one master clock, so the low symbol rate clock is on as long as ANY tuner needs a rate below LowSrBelowKS. Before, the
        // last tuner to be set up decided: tuner 2 (1500 kS) switched the clock back to 135 MHz and tuner 1 lost its low symbol rate setup silently, and every low
        // rate on tuner 1 switched the clock away under tuner 2.
        private readonly bool[] wants_low_clock = new bool[2];

        // Changes the PLL of a running chip the way MiniTioune does it (I2C capture of the "Low SR" switch): SYNTCTRL 0xC2 (standby, PLL bypassed), both
        // demodulators reset and stopped (DMDISTATE 0x1F, 0x1C), NCOARSE / NCOARSE1 / NCOARSE2 = CP + IDF / NDIV / ODF, DEMOD 0x0C for the low clock
        // (bit 3 is not in the STV0913 datasheet), SYNTCTRL 0x42 (standby off), 0x02 (PLL on), then wait for the lock. Standard: IDF 1, NDIV 18, ODF 4
        // (135 MHz, NCOARSE 0x39); low: IDF 2, NDIV 11, ODF 4 (41.25 MHz, NCOARSE 0x3A). CP is 7 for both.
        private byte stv0910_switch_clock(bool low)
        {
            byte err = 0;

            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_SYNTCTRL, 0xC2);
            foreach (ushort dmdistate in new ushort[] { stv0910_regs.RSTV0910_P1_DMDISTATE, stv0910_regs.RSTV0910_P2_DMDISTATE })
            {
                if (err == 0) err = stv0910_write_reg(dmdistate, 0x1F);
                if (err == 0) err = stv0910_write_reg(dmdistate, 0x1C);
            }

            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_NCOARSE, (byte)(low ? 0x3A : 0x39));
            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_NCOARSE1, (byte)(low ? 11 : 18));
            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_NCOARSE2, 4);
            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_P1_DEMOD, (byte)(low ? 0x0C : 0x00));
            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_P2_DEMOD, (byte)(low ? 0x0C : 0x00));
            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_SYNTCTRL, 0x42);
            if (err == 0) err = stv0910_write_reg(stv0910_regs.RSTV0910_SYNTCTRL, 0x02);

            byte pll_lock = 0;
            for (int i = 0; i < 100 && err == 0 && pll_lock == 0; i++)
            {
                err = stv0910_read_reg_field(stv0910_regs.FSTV0910_PLLLOCK, ref pll_lock);
                if (pll_lock == 0) Thread.Sleep(1);
            }

            if (pll_lock == 0) Log.Information("Warning: STV0910 PLL not locked after the clock switch");

            return err;
        }

        private byte stv0910_select_clock(UInt32 sr, int index)
        {
            wants_low_clock[index] = sr < LowSrBelowKS;
            uint wanted = (wants_low_clock[0] || wants_low_clock[1]) ? LowSrMclkHz : 135000000;
            byte err = 0;

            if (wanted == MclkHz)
            {
                if (wants_low_clock[1 - index] && !wants_low_clock[index] && MclkHz == LowSrMclkHz)
                    Log.Information("Flow: STV0910 master clock stays at {0} MHz for the other demodulator (low symbol rate); {1} kS on this one runs at the low clock", MclkHz / 1000000, sr);

                return err;
            }

            Log.Information("Flow: STV0910 master clock {0} -> {1} MHz for {2} kS", MclkHz / 1000000.0, wanted / 1000000.0, sr);
            MclkHz = wanted;
            err = stv0910_switch_clock(wanted == LowSrMclkHz);

            // the other demodulator was programmed for the old clock (its SFR / CFR registers scale with it)
            int other = 1 - index;
            if (err == 0 && receive_configured[other])
            {
                byte other_demod = other == 0 ? STV0910_DEMOD_TOP : STV0910_DEMOD_BOTTOM;
                err = stv0910_configure_receive(other_demod, last_sr[other], last_capture[other]);
                if (err == 0) err = stv0910_start_scan(other_demod);
            }

            return err;
        }

        public byte stv0910_setup_receive(byte demod, UInt32 sr, UInt32 capture_range_khz = 0)
        {
            byte err = 0;
            int index = demod == STV0910_DEMOD_TOP ? 0 : 1;

            if (AllowLowSrClock)
                err = stv0910_select_clock(sr, index);

            if (err == 0) err = stv0910_configure_receive(demod, sr, capture_range_khz);

            if (err == 0)
            {
                last_sr[index] = sr;
                last_capture[index] = capture_range_khz;
                receive_configured[index] = true;
            }

            return err;
        }

        // DiSEqC tone burst ("mini-DiSEqC" A/B satellite switching) - a one-shot pulse, not a
        // persistent state like the continuous 22kHz tone below. Ported from the Linux kernel's
        // stv0910.c send_burst(): set DISEQC_MODE=3 (tone burst), precharge, write a trigger byte
        // to the TX FIFO, release precharge, then wait for TX_IDLE. Unverified against real
        // hardware - test carefully before relying on it.
        private byte stv0910_send_tone_burst(UInt32 diseqc_mode_field, UInt32 precharge_field, UInt32 fifo_full_field, ushort fifo_reg, UInt32 tx_idle_field)
        {
            byte err = 0;

            err |= stv0910_write_reg_field(diseqc_mode_field, 3); // ToneBurst mode
            err |= stv0910_write_reg_field(precharge_field, 1);

            byte fifo_full = 1;
            for (int i = 0; i < 100 && fifo_full != 0; i++)
            {
                stv0910_read_reg_field(fifo_full_field, ref fifo_full);
                if (fifo_full != 0) Thread.Sleep(1);
            }

            err |= stv0910_write_reg(fifo_reg, 0x00); // trigger byte (burst "A")
            err |= stv0910_write_reg_field(precharge_field, 0);

            byte tx_idle = 0;
            for (int i = 0; i < 100 && tx_idle == 0; i++)
            {
                stv0910_read_reg_field(tx_idle_field, ref tx_idle);
                if (tx_idle == 0) Thread.Sleep(1);
            }

            if (tx_idle == 0)
            {
                Log.Information("Tone burst: timed out waiting for TX_IDLE");
            }

            return err;
        }

        public byte stv0910_send_tone_burst_p1()
        {
            return stv0910_send_tone_burst(stv0910_regs.FSTV0910_P1_DISEQC_MODE, stv0910_regs.FSTV0910_P1_DIS_PRECHARGE,
                stv0910_regs.FSTV0910_P1_TX_FIFO_FULL, stv0910_regs.RSTV0910_P1_DISTXFIFO, stv0910_regs.FSTV0910_P1_TX_IDLE);
        }

        public byte stv0910_send_tone_burst_p2()
        {
            return stv0910_send_tone_burst(stv0910_regs.FSTV0910_P2_DISEQC_MODE, stv0910_regs.FSTV0910_P2_DIS_PRECHARGE,
                stv0910_regs.FSTV0910_P2_TX_FIFO_FULL, stv0910_regs.RSTV0910_P2_DISTXFIFO, stv0910_regs.FSTV0910_P2_TX_IDLE);
        }

        // Switches BOTH P1 and P2 together with the same flag - kept for reference/compat, no
        // longer called. Use stv0910_switch_22Khz(demod, ...) below for independent per-tuner
        // control (22K-A / 22K-B).
        public byte stv0910_switch_22Khz_p1(bool switch_flag)
        {
            byte err = 0;

            if (switch_flag)
            {
                err = stv0910_write_reg(stv0910_regs.RSTV0910_P1_DISTXCFG, KHZ22ON);
            }
            else
            {
                err = stv0910_write_reg(stv0910_regs.RSTV0910_P1_DISTXCFG, KHZ22OFF);
            }

            if (err != 0)
            {
                Log.Information("Error switching 22khz - P1");
            }

            if (err == 0)
            {
                if (switch_flag)
                {
                    err = stv0910_write_reg(stv0910_regs.RSTV0910_P2_DISTXCFG, KHZ22ON);
                }
                else
                {
                    err = stv0910_write_reg(stv0910_regs.RSTV0910_P2_DISTXCFG, KHZ22OFF);
                }

                if (err != 0)
                {
                    Log.Information("Error switching 22khz - P2");
                }

            }

            return err;
        }

        // Independent per-tuner continuous 22kHz tone (22K_TX1/22K_TX2) - same register/values
        // as stv0910_switch_22Khz_p1 above, just applied to one demod (P1 or P2) at a time.
        public byte stv0910_switch_22Khz(byte demod, bool switch_flag)
        {
            byte err = stv0910_write_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_DISTXCFG : stv0910_regs.RSTV0910_P1_DISTXCFG,
                switch_flag ? KHZ22ON : KHZ22OFF);

            if (err != 0)
            {
                Log.Information("Error switching 22khz - demod " + demod.ToString());
            }

            return err;
        }

        private byte stv0910_write_reg_field(UInt32 field, byte field_val)
        {
            byte err = 0;
            ushort reg = 0;
            byte val;

            reg = (ushort)(field >> 16);

            val = (byte)((stv0910_shadow_regs[reg - stv0910_regs.STV0910_START_ADDR] & ~(byte)(field & 0xff)) | (field_val << (byte)(((field >> 12) & 0x0f))));

            if (err == 0) err = nim_device.nim_write_demod(reg, val);
            if (err == 0) stv0910_shadow_regs[reg - stv0910_regs.STV0910_START_ADDR] = val;

            return err;
        }


        private byte stv0910_read_reg_field( UInt32 field, ref byte field_val)
        {
            byte err = 0;
            byte val = 0;

            if (err == 0) err = nim_device.nim_read_demod((ushort)(field >> 16), ref val);

            //Log.Information("Read REG Field {0} {1}", field.ToString(), val.ToString());

            UInt32 t1 = ((val) & (field & 0xff));
            Int32 t2 = (((Int32)field >> 12) & 0x0f);

            UInt32 t3 = t1 >> t2;

            field_val = (byte)t3;

            //Log.Information("-- Read REG Field {0} {1}", field.ToString(), field_val.ToString());

            return err;
        }

        byte stv0910_read_reg(ushort reg, ref byte val)
        {
            return nim_device.nim_read_demod(reg, ref val);
        }


        byte stv0910_write_reg(ushort reg, byte val)
        {
            stv0910_shadow_regs[reg - stv0910_regs.STV0910_START_ADDR] = val;
            return nim_device.nim_write_demod(reg, val);
        }

        public byte stv0910_read_matype(byte demod, ref UInt32 matype1, ref UInt32 matype2)
        {
            byte err;
            byte regval = 0;

            err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? Convert.ToUInt16(stv0910_regs.RSTV0910_P2_MATSTR0 - 1) : Convert.ToUInt16(stv0910_regs.RSTV0910_P1_MATSTR0 - 1), ref regval);
            matype1 = regval;

            err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_MATSTR0 : stv0910_regs.RSTV0910_P1_MATSTR0, ref regval);
            matype2 = regval;

            if (err != 0) Log.Information("ERROR: STV0910 read MATYPE");

            return err;
        }

        public byte stv0910_read_mer(byte demod, ref Int32 mer)
        {
            byte err = 0;
            byte high = 0;
            byte low = 0;

            err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_NOSRAMPOS : stv0910_regs.RSTV0910_P1_NOSRAMPOS, ref high);
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_NOSRAMVAL : stv0910_regs.RSTV0910_P1_NOSRAMVAL, ref low);

            if (((high >> 2) & 0x01) == 1)
            {
                /* Px_NOSRAM_CNRVAL is valid */
                if (((high >> 1) & 0x01) == 1)
                {
                    mer = (int)(((high & 0x01) << 8) | low) - 512;
                }
                else
                {
                    mer = (int)(((high & 0x01) << 8) | low); 
                }
            }
            else
            {
                mer = 0;
                if (err == 0) err = stv0910_write_reg_field(demod == STV0910_DEMOD_TOP ? stv0910_regs.FSTV0910_P2_NOSRAM_ACTIVATION : stv0910_regs.FSTV0910_P1_NOSRAM_ACTIVATION, 0x02);
            }

            if (err != 0) Log.Information("ERROR: STV0910 read DVBS2 MER\n");

            return err;

        }

        public byte stv0910_read_errors_ldpc_count(byte demod, ref UInt32 errors_ldpc_count)
        {
            /* -------------------------------------------------------------------------------------------------- */
            /*               demod: STV0910_DEMOD_TOP | STV0910_DEMOD_BOTTOM: which demodulator is being read      */
            /*   errors_ldpc_count: place to store the result                                                      */
            /*              return: error state                                                                    */
            /* -------------------------------------------------------------------------------------------------- */
            byte err;
            byte high = 0, low = 0;

            /* This parameter appears to be total, not for an individual demodulator */
            //(void)demod;

            err = stv0910_read_reg_field(stv0910_regs.FSTV0910_LDPC_ERRORS1, ref high);
            if (err == 0) err = stv0910_read_reg_field(stv0910_regs.FSTV0910_LDPC_ERRORS0, ref low);

            errors_ldpc_count = (UInt32)high << 8 | (UInt32)low;

            if (err != 0) Log.Information("ERROR: STV0910 read LDPC Errors Count\n");

            return err;
        }

        public byte  stv0910_read_modcod_and_type(byte demod, ref UInt32 modcod, ref bool short_frame, ref bool pilots, ref byte rolloff)
        {
            /* -------------------------------------------------------------------------------------------------- */
            /*   Note that MODCODs are different in DVBS and DVBS2. Also short_frame and pilots only valid for S2 */
            /*    demod: STV0910_DEMOD_TOP | STV0910_DEMOD_BOTTOM: which demodulator is being read                */
            /*   modcod: place to store the result                                                                */
            /*   return: error state                                                                              */
            /* -------------------------------------------------------------------------------------------------- */
            byte err;
            byte regval = 0;

            err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_DMDMODCOD : stv0910_regs.RSTV0910_P1_DMDMODCOD, ref regval);

            modcod = (UInt32)((regval & 0x7c) >> 2);
            if (((regval & 0x02) >> 1) == 1)
                short_frame = true;
            else
                short_frame = false;

            if ((regval & 0x01) == 1)
                pilots = true;
            else
                pilots = false;

            if (err != 0) Log.Information("ERROR: STV0910 read MODCOD\n");

            err = stv0910_read_reg_field(demod == STV0910_DEMOD_TOP ? stv0910_regs.FSTV0910_P2_ROLLOFF_STATUS : stv0910_regs.FSTV0910_P1_ROLLOFF_STATUS, ref regval);

            rolloff = regval;

            return err;
        }

        public byte stv0910_read_errors_bch_count(byte demod, ref UInt32 errors_bch_count)
        {
            byte err = 0;
            byte result = 0;

            /* This parameter appears to be total, not for an individual demodulator */
            //(void)demod;

            err = stv0910_read_reg_field(stv0910_regs.FSTV0910_BCH_ERRORS_COUNTER, ref result);

            errors_bch_count = (UInt32)result;

            if (err != 0) Log.Information("ERROR: STV0910 read BCH Errors Count\n");
            return err;
        }

        public byte stv0910_read_errors_bch_uncorrected(byte demod, ref bool errors_bch_uncorrected)
        {
            byte err = 0;
            byte result = 0;

            /* This parameter appears to be total, not for an individual demodulator */
            //(void)demod;

            err = stv0910_read_reg_field(stv0910_regs.FSTV0910_ERRORFLAG, ref result);

            if (result == 0)
            {
                errors_bch_uncorrected = true;
            }
            else
            {
                errors_bch_uncorrected = false;
            }

            if (err != 0) Log.Information("ERROR: STV0910 read BCH Errors Uncorrected\n");

            return err;
        }

        public byte stv0910_read_ber(byte demod, ref UInt32 ber)
        {
            byte err = 0;

            byte high = 0, mid_u = 0, mid_m = 0, mid_l = 0, low = 0;
            double cpt = 0;
            double errs = 0;

            /* first we trigger a buffer transfer and read the byte counter 40 bits */
            err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_FBERCPT4 : stv0910_regs.RSTV0910_P1_FBERCPT4, ref high);
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_FBERCPT3 : stv0910_regs.RSTV0910_P1_FBERCPT3, ref mid_u);
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_FBERCPT2 : stv0910_regs.RSTV0910_P1_FBERCPT2, ref mid_m);
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_FBERCPT1 : stv0910_regs.RSTV0910_P1_FBERCPT1, ref mid_l);
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_FBERCPT0 : stv0910_regs.RSTV0910_P1_FBERCPT0, ref low);
            cpt = (double)high * 256.0 * 256.0 * 256.0 * 256.0 + (double)mid_u * 256.0 * 256.0 * 256.0 + (double)mid_m * 256.0 * 256.0 +
                (double)mid_l * 256.0 + (double)low;

            /* we have already triggered the register buffer transfer, so now we we read the bit error from them */
            err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_FBERERR2 : stv0910_regs.RSTV0910_P1_FBERERR2, ref high);
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_FBERERR1 : stv0910_regs.RSTV0910_P1_FBERERR1, ref mid_m);
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_FBERERR0 : stv0910_regs.RSTV0910_P1_FBERERR0, ref low);
            errs = (double)high * 256.0 * 256.0 + (double)mid_m * 256.0 + (double)low;

            ber = (UInt32)(10000.0 * errs / (cpt * 8.0));

            if (err != 0) Log.Information("ERROR: STV0910 read BER\n");

            return err;
        }

        public byte stv0910_read_err_rate(byte demod, ref UInt32 vit_errs)
        {
            byte err = 0;
            byte val = 0;

            err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_VERROR : stv0910_regs.RSTV0910_P1_VERROR, ref val);
            /* 0=perfect, 0xff=6.23 %errors (errs/4096) */
            /* note there is a problem in the datasheet here as it says 255/2048=6.23% */
            /* to report an integer we will report in 100 * the percentage, so 623=6.23% */
            /* also want to round up to the nearest integer just to be pedantic */
            vit_errs = ((((UInt32)val) * 100000 / 4096) + 5) / 10;

            if (err != 0) Log.Information("ERROR: STV0910 read viterbi error rate\n");

            return err;
        }

        // Lock indicators of one demodulator: DSTATUS (bit 7 CAR_LOCK, bits 6:5 TMGLOCK_QUALITY, bit 3
        // LOCK_DEFINITIF, bit 0 OVADC_DETECT), DSTATUS2 (bit 7 DEMOD_DELOCK, bits 3..0 failure flags), LDI (carrier lock indicator accumulator, signed 8 bit) and TMGLOCK (timing lock
        // indicator accumulator, 16 bit).
        public byte stv0910_read_lock_indicators(byte demod, ref byte dstatus, ref byte dstatus2, ref sbyte ldi, ref ushort tmglock)
        {
            bool top = demod == STV0910_DEMOD_TOP;
            byte val = 0, high = 0, low = 0;
            byte err;

            err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_DSTATUS : stv0910_regs.RSTV0910_P1_DSTATUS, ref dstatus);
            // DSTATUS2 bits 3..0 (failure observation) are cleared by this read
            if (err == 0) err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_DSTATUS2 : stv0910_regs.RSTV0910_P1_DSTATUS2, ref dstatus2);
            if (err == 0) err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_LDI : stv0910_regs.RSTV0910_P1_LDI, ref val);
            if (err == 0) err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_TMGLOCK1 : stv0910_regs.RSTV0910_P1_TMGLOCK1, ref high);
            if (err == 0) err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_TMGLOCK0 : stv0910_regs.RSTV0910_P1_TMGLOCK0, ref low);

            ldi = unchecked((sbyte)val);
            tmglock = (ushort)((high << 8) | low);

            if (err != 0) Log.Information("ERROR: STV0910 read lock indicators");

            return err;
        }

        // DSTATUS2.DEMOD_DELOCK (bit 7) is set when LOCK_DEFINITIF goes through zero and is reset by an I2C write (datasheet).
        // NimThread writes it at every new lock, so afterwards it means "lock lost since the last lock". Bits 3..0 are read-only.
        public byte stv0910_clear_delock(byte demod)
        {
            byte err = stv0910_write_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_DSTATUS2 : stv0910_regs.RSTV0910_P1_DSTATUS2, 0x00);

            if (err != 0) Log.Information("ERROR: STV0910 clear DEMOD_DELOCK");

            return err;
        }

        // Stream flags of one demodulator: PDELSTATUS1 (packet delineator, bit 1 PKTDELIN_LOCK, bit 0 FIRST_LOCK, bit 3
        // BCH_ERROR_FLAG - bits 6, 4 and 3 are cleared by the read), the SPECINV_DEMOD bit of PLHMODCOD (bit 7, spectrum
        // inversion the demodulator found) and the raw BCHERR register (bit 4 ERRORFLAG, bits 3..0 BCH_ERRORS_COUNTER, chip-wide).
        public byte stv0910_read_stream_flags(byte demod, ref byte pdelstatus1, ref bool spectrum_inverted, ref byte bcherr)
        {
            bool top = demod == STV0910_DEMOD_TOP;
            byte plhmodcod = 0;
            byte err;

            err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_PDELSTATUS1 : stv0910_regs.RSTV0910_P1_PDELSTATUS1, ref pdelstatus1);
            if (err == 0) err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_PLHMODCOD : stv0910_regs.RSTV0910_P1_PLHMODCOD, ref plhmodcod);
            if (err == 0) err = stv0910_read_reg(stv0910_regs.RSTV0910_BCHERR, ref bcherr);

            spectrum_inverted = (plhmodcod & 0x80) != 0;

            if (err != 0) Log.Information("ERROR: STV0910 read stream flags");

            return err;
        }

        // Noise level of one demodulator, linear (modulus) and normalized to the signal: 0x4000 = noise as strong as the
        // signal. DVB-S2 uses NNOSPLHT (measured on PLHeader / pilots, the accurate one for S2), DVB-S uses NNOSDATAT
        // (measured on the data symbols). The MSB has to be read first, that latches the pair.
        public byte stv0910_read_noise(byte demod, bool dvbs2, ref ushort noise)
        {
            bool top = demod == STV0910_DEMOD_TOP;
            byte high = 0, low = 0;
            byte err;

            if (dvbs2)
            {
                err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_NNOSPLHT1 : stv0910_regs.RSTV0910_P1_NNOSPLHT1, ref high);
                if (err == 0) err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_NNOSPLHT0 : stv0910_regs.RSTV0910_P1_NNOSPLHT0, ref low);
            }
            else
            {
                err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_NNOSDATAT1 : stv0910_regs.RSTV0910_P1_NNOSDATAT1, ref high);
                if (err == 0) err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_NNOSDATAT0 : stv0910_regs.RSTV0910_P1_NNOSDATAT0, ref low);
            }

            noise = (ushort)((high << 8) | low);

            if (err != 0) Log.Information("ERROR: STV0910 read noise level");

            return err;
        }

        // TSBITRATE of one demodulator: TSFIFO_BITRATE, the raw bit rate of the stream leaving the packet delineator
        // (datasheet: bit rate = Mclk * TSFIFO_BITRATE / 16384, Mclk = 135 MHz, so one step is about 8.24 kbit/s).
        public byte stv0910_read_ts_bitrate(byte demod, ref ushort raw)
        {
            bool top = demod == STV0910_DEMOD_TOP;
            byte high = 0, low = 0;
            byte err;

            err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_TSBITRATE1 : stv0910_regs.RSTV0910_P1_TSBITRATE1, ref high);
            if (err == 0) err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_TSBITRATE0 : stv0910_regs.RSTV0910_P1_TSBITRATE0, ref low);

            raw = (ushort)((high << 8) | low);

            if (err != 0) Log.Information("ERROR: STV0910 read TS bitrate");

            return err;
        }

        // Debug aid: what the demodulator really holds for the symbol rate and carrier search (values in the order of
        // SearchRegisterNames), to compare with what the setup code wrote.
        public static readonly string[] SearchRegisterNames = { "SFRINIT1", "SFRINIT0", "SFRUP1", "SFRUP0", "SFRLOW1", "SFRLOW0", "CFRINIT1", "CFRINIT0", "CFRUP1", "CFRUP0", "CFRLOW1", "CFRLOW0", "TMGCFG", "TMGCFG2", "RTCS2", "CARCFG", "DMDISTATE", "CFRINC1", "CFRINC0", "SFRUPRATIO", "SFRLOWRATIO" };

        public byte stv0910_read_search_registers(byte demod, byte[] values)
        {
            bool top = demod == STV0910_DEMOD_TOP;
            ushort[] regs = top
                ? new ushort[] { stv0910_regs.RSTV0910_P2_SFRINIT1, stv0910_regs.RSTV0910_P2_SFRINIT0, stv0910_regs.RSTV0910_P2_SFRUP1, stv0910_regs.RSTV0910_P2_SFRUP0, stv0910_regs.RSTV0910_P2_SFRLOW1, stv0910_regs.RSTV0910_P2_SFRLOW0, stv0910_regs.RSTV0910_P2_CFRINIT1, stv0910_regs.RSTV0910_P2_CFRINIT0, stv0910_regs.RSTV0910_P2_CFRUP1, stv0910_regs.RSTV0910_P2_CFRUP0, stv0910_regs.RSTV0910_P2_CFRLOW1, stv0910_regs.RSTV0910_P2_CFRLOW0, stv0910_regs.RSTV0910_P2_TMGCFG, stv0910_regs.RSTV0910_P2_TMGCFG2, stv0910_regs.RSTV0910_P2_RTCS2, stv0910_regs.RSTV0910_P2_CARCFG, stv0910_regs.RSTV0910_P2_DMDISTATE, stv0910_regs.RSTV0910_P2_CFRINC1, stv0910_regs.RSTV0910_P2_CFRINC0, stv0910_regs.RSTV0910_P2_SFRUPRATIO, stv0910_regs.RSTV0910_P2_SFRLOWRATIO }
                : new ushort[] { stv0910_regs.RSTV0910_P1_SFRINIT1, stv0910_regs.RSTV0910_P1_SFRINIT0, stv0910_regs.RSTV0910_P1_SFRUP1, stv0910_regs.RSTV0910_P1_SFRUP0, stv0910_regs.RSTV0910_P1_SFRLOW1, stv0910_regs.RSTV0910_P1_SFRLOW0, stv0910_regs.RSTV0910_P1_CFRINIT1, stv0910_regs.RSTV0910_P1_CFRINIT0, stv0910_regs.RSTV0910_P1_CFRUP1, stv0910_regs.RSTV0910_P1_CFRUP0, stv0910_regs.RSTV0910_P1_CFRLOW1, stv0910_regs.RSTV0910_P1_CFRLOW0, stv0910_regs.RSTV0910_P1_TMGCFG, stv0910_regs.RSTV0910_P1_TMGCFG2, stv0910_regs.RSTV0910_P1_RTCS2, stv0910_regs.RSTV0910_P1_CARCFG, stv0910_regs.RSTV0910_P1_DMDISTATE, stv0910_regs.RSTV0910_P1_CFRINC1, stv0910_regs.RSTV0910_P1_CFRINC0, stv0910_regs.RSTV0910_P1_SFRUPRATIO, stv0910_regs.RSTV0910_P1_SFRLOWRATIO };
            byte err = 0;

            for (int i = 0; i < regs.Length && err == 0; i++)
            {
                byte val = 0;
                err = stv0910_read_reg(regs[i], ref val);
                values[i] = val;
            }

            if (err != 0) Log.Information("ERROR: STV0910 read search registers");

            return err;
        }

        // Debug aid: all 16 bit noise registers of one demodulator (MSB first), to find out which one MiniTioune shows.
        // Order: NNOSPLHT, NNOSPLH, NNOSDATAT, NNOSDATA, NNOSFRAME, NNOSRAD, NOSDATAT (absolute, not normalized).
        public byte stv0910_read_noise_candidates(byte demod, ushort[] values)
        {
            bool top = demod == STV0910_DEMOD_TOP;
            ushort[] msb = top
                ? new ushort[] { stv0910_regs.RSTV0910_P2_NNOSPLHT1, stv0910_regs.RSTV0910_P2_NNOSPLH1, stv0910_regs.RSTV0910_P2_NNOSDATAT1,
                                 stv0910_regs.RSTV0910_P2_NNOSDATA1, stv0910_regs.RSTV0910_P2_NNOSFRAME1, stv0910_regs.RSTV0910_P2_NNOSRAD1,
                                 stv0910_regs.RSTV0910_P2_NOSDATAT1 }
                : new ushort[] { stv0910_regs.RSTV0910_P1_NNOSPLHT1, stv0910_regs.RSTV0910_P1_NNOSPLH1, stv0910_regs.RSTV0910_P1_NNOSDATAT1,
                                 stv0910_regs.RSTV0910_P1_NNOSDATA1, stv0910_regs.RSTV0910_P1_NNOSFRAME1, stv0910_regs.RSTV0910_P1_NNOSRAD1,
                                 stv0910_regs.RSTV0910_P1_NOSDATAT1 };
            byte err = 0;

            for (int i = 0; i < msb.Length && err == 0; i++)
            {
                byte high = 0, low = 0;
                err = stv0910_read_reg(msb[i], ref high);
                if (err == 0) err = stv0910_read_reg((ushort)(msb[i] + 1), ref low); // the LSB follows the MSB in all these pairs
                values[i] = (ushort)((high << 8) | low);
            }

            if (err != 0) Log.Information("ERROR: STV0910 read noise candidates");

            return err;
        }

        // LDPC iterations of one demodulator (DVB-S2 only): STATUSITER = iterations used on the last frame,
        // STATUSMAXITER = maximum since the last read of that register.
        public byte stv0910_read_ldpc_iterations(byte demod, ref byte iterations, ref byte max_iterations)
        {
            bool top = demod == STV0910_DEMOD_TOP;
            byte err;

            err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_STATUSITER : stv0910_regs.RSTV0910_P1_STATUSITER, ref iterations);
            if (err == 0) err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_STATUSMAXITER : stv0910_regs.RSTV0910_P1_STATUSMAXITER, ref max_iterations);

            if (err != 0) Log.Information("ERROR: STV0910 read LDPC iterations");

            return err;
        }

        // PLLSTAT.PLLLOCK: the demodulator's internal PLL (system clock) is locked. Chip-wide.
        public byte stv0910_read_pll_lock(ref bool locked)
        {
            byte val = 0;
            byte err = stv0910_read_reg_field(stv0910_regs.FSTV0910_PLLLOCK, ref val);

            locked = val != 0;

            if (err != 0) Log.Information("ERROR: STV0910 read PLL lock");

            return err;
        }

        // Carrier (derotator) search range of one demodulator in Hz: CFRLOW / CFRUP, 16 bit signed, unit
        // 135 MHz / 2^16 (the same conversion stv0910_setup_carrier_loop uses when it writes them).
        public byte stv0910_read_carrier_range(byte demod, ref Int32 low_hz, ref Int32 up_hz)
        {
            bool top = demod == STV0910_DEMOD_TOP;
            byte up_high = 0, up_low = 0, low_high = 0, low_low = 0;
            byte err;

            err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_CFRUP1 : stv0910_regs.RSTV0910_P1_CFRUP1, ref up_high);
            if (err == 0) err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_CFRUP0 : stv0910_regs.RSTV0910_P1_CFRUP0, ref up_low);
            if (err == 0) err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_CFRLOW1 : stv0910_regs.RSTV0910_P1_CFRLOW1, ref low_high);
            if (err == 0) err = stv0910_read_reg(top ? stv0910_regs.RSTV0910_P2_CFRLOW0 : stv0910_regs.RSTV0910_P1_CFRLOW0, ref low_low);

            short up = unchecked((short)((up_high << 8) | up_low));
            short low = unchecked((short)((low_high << 8) | low_low));

            up_hz = (Int32)(up * (long)MclkHz / 65536);
            low_hz = (Int32)(low * (long)MclkHz / 65536);

            if (err != 0) Log.Information("ERROR: STV0910 read carrier range");

            return err;
        }

        public byte stv0910_read_sr(byte demod, ref UInt32 found_sr)
        {
            byte err = 0;

            double sr;
            byte val_h = 0, val_mu = 0, val_ml = 0, val_l = 0;

            err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_SFR3 : stv0910_regs.RSTV0910_P1_SFR3, ref val_h);  /* high byte */
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_SFR2 : stv0910_regs.RSTV0910_P1_SFR2, ref val_mu); /* mid upper */
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_SFR1 : stv0910_regs.RSTV0910_P1_SFR1, ref val_ml); /* mid lower */
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_SFR0 : stv0910_regs.RSTV0910_P1_SFR0, ref val_l);  /* low byte */

            sr = ((UInt32)val_h << 24) +
               ((UInt32)val_mu << 16) +
               ((UInt32)val_ml << 8) +
               ((UInt32)val_l);

            /* sr (MHz) = ckadc (MHz) * SFR/2^32. So in Symbols per Second we need */
            sr = (double)MclkHz * sr / 256.0 / 256.0 / 256.0 / 256.0;
            found_sr = (UInt32)sr;

            // read the symbol rate detection offset

            int temp = 0;
            byte tempc = 0;
            double tempf = 0;
            if (err == 0)
            {
                err |= stv0910_read_reg
                (
                    demod == STV0910_DEMOD_TOP
                    ? stv0910_regs.RSTV0910_P2_TMGREG2 : stv0910_regs.RSTV0910_P1_TMGREG2,            /* byte2 */
                    ref tempc
                );
                temp |= tempc << 24;

                err |= stv0910_read_reg
                (
                    demod == STV0910_DEMOD_TOP
                    ? stv0910_regs.RSTV0910_P2_TMGREG1 : stv0910_regs.RSTV0910_P1_TMGREG1,            /* byte1 */
                    ref tempc
                );
                temp |= tempc << 16;

                err |= stv0910_read_reg
                (
                    demod == STV0910_DEMOD_TOP
                    ? stv0910_regs.RSTV0910_P2_TMGREG0 : stv0910_regs.RSTV0910_P1_TMGREG0,            /* byte0 */
                    ref tempc
                );
                temp |= tempc << 8;

                temp = temp / 256;                                          // move to the bottom 24 bits 
                                                                            // and extend the sign
            }

            tempf = temp;                                                   // convert to double
            tempf = tempf * 1000 / (1 << 29);                               // calculate offset in symbols
            tempf = tempf * sr / 1000;                                      // multiply by nominal symbol rate
            found_sr = (uint)(sr + tempf);							// update the value

            if (err != 0) Log.Information("ERROR: STV0910 read symbol rate\n");

            return err;
        }

        public byte stv0910_read_car_freq(byte demod, ref Int32 cf)
        {
            /* -------------------------------------------------------------------------------------------------- */
            /* reads the current carrier frequency and return it (in Hz)                                          */
            /*    demod: STV0910_DEMOD_TOP | STV0910_DEMOD_BOTTOM: which demodulator is being read                */
            /* car_freq: signed place to store the answer                                                         */
            /*   return: error state                                                                              */
            /* -------------------------------------------------------------------------------------------------- */
            byte err;
            byte val_h = 0, val_m = 0, val_l = 0;
            double car_offset_freq;

            /* first off we read in the carrier offset as a signed number */
            err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ?
                             stv0910_regs.RSTV0910_P2_CFR2 : stv0910_regs.RSTV0910_P1_CFR2, ref val_h); /* high byte*/
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ?
                                                      stv0910_regs.RSTV0910_P2_CFR1 : stv0910_regs.RSTV0910_P1_CFR1, ref val_m); /* mid */
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ?
                                                      stv0910_regs.RSTV0910_P2_CFR0 : stv0910_regs.RSTV0910_P1_CFR0, ref val_l); /* low */
            /* since this is a 24 bit signed value, we need to build it as a 24 bit value, shift it up to the top
               to get a 32 bit signed value, then convert it to a double */
            car_offset_freq = (double)(Int32)((((UInt32)val_h << 16) + ((UInt32)val_m << 8) + ((UInt32)val_l)) << 8);
            /* carrier offset freq (MHz)= mclk (MHz) * CFR/2^24. But we have the extra 256 in there from the sign shift */
            /* so in Hz we need: */
            car_offset_freq = (double)MclkHz * car_offset_freq / 256.0 / 256.0 / 256.0 / 256.0;

            cf = (Int32)car_offset_freq;

            if (err != 0) Log.Information("ERROR: STV0910 read carrier frequency\n");

            return err;
        }

        public byte stv0910_read_puncture_rate(byte demod, ref byte rate)
        {

            /* -------------------------------------------------------------------------------------------------- */
            /* reads teh detected viterbi punctuation rate                                                        */
            /*   demod: STV0910_DEMOD_TOP | STV0910_DEMOD_BOTTOM: which demodulator is being read                 */
            /*   rate: place to store the result                                                                   */
            /*         The single byta, n, represents a rate=n/n+1                                                 */
            /* return: error code                                                                                 */
            /* -------------------------------------------------------------------------------------------------- */
            byte err;
            byte val = 0;

            err = stv0910_read_reg_field(demod == STV0910_DEMOD_TOP ? stv0910_regs.FSTV0910_P2_VIT_CURPUN : stv0910_regs.FSTV0910_P1_VIT_CURPUN, ref val);
            switch (val)
            {
                case STV0910_PUNCTURE_1_2: rate = 1; break;
                case STV0910_PUNCTURE_2_3: rate = 2; break;
                case STV0910_PUNCTURE_3_4: rate = 3; break;
                case STV0910_PUNCTURE_5_6: rate = 5; break;
                case STV0910_PUNCTURE_6_7: rate = 6; break;
                case STV0910_PUNCTURE_7_8: rate = 7; break;
                default: err = Errors.ERROR_VITERBI_PUNCTURE_RATE; break;
            }

            if (err != 0) Log.Information("ERROR: STV0910 read puncture rate");

            return err;

        }

        public byte stv0910_read_constellation(byte demod, ref byte i, ref byte q)
        {
            byte err = 0;

            err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_ISYMB : stv0910_regs.RSTV0910_P1_ISYMB, ref i);
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_QSYMB : stv0910_regs.RSTV0910_P1_QSYMB, ref q);

            if (err != 0) Log.Information("ERROR: STV0910 read constellation");

            return err;
        }

        /* -------------------------------------------------------------------------------------------------- */
        /* reads the AGC1 Gain registers in the Demodulator and returns the results                           */
        /* demod: STV0910_DEMOD_TOP | STV0910_DEMOD_BOTTOM: which demodulator is being read                  */
        /* agc: place to store the results                                                                    */
        /* return: error state                                                                                */
        /* -------------------------------------------------------------------------------------------------- */

        public byte stv0910_read_agc1_gain(byte demod, ref ushort agc)
        {

            byte err = 0;
            byte agc_low = 0, agc_high = 0;

            err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_AGCIQIN0 : stv0910_regs.RSTV0910_P1_AGCIQIN0, ref agc_low);
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_AGCIQIN1 : stv0910_regs.RSTV0910_P1_AGCIQIN1, ref agc_high);
            if (err == 0) agc = (ushort)((ushort)agc_high << 8 | (ushort)agc_low);

            if (err != 0) Log.Information("ERROR: STV0910 read agc1 gain\n");

            return err;
        }

        /* -------------------------------------------------------------------------------------------------- */
        public byte stv0910_read_agc2_gain(byte demod, ref ushort agc)
        {
            /* -------------------------------------------------------------------------------------------------- */
            /* reads the AGC2 Gain registers in the Demodulator and returns the results                           */
            /*  demod: STV0910_DEMOD_TOP | STV0910_DEMOD_BOTTOM: which demodulator is being read                  */
            /* agc: place to store the results                                                                    */
            /* return: error state                                                                                */
            /* -------------------------------------------------------------------------------------------------- */
            byte err;
            byte agc_low = 0, agc_high = 0;

            err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_AGC2I0 : stv0910_regs.RSTV0910_P1_AGC2I0, ref agc_low);
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_AGC2I1 : stv0910_regs.RSTV0910_P1_AGC2I1, ref agc_high);
            if (err == 0) agc = (ushort)((ushort)agc_high << 8 | (ushort)agc_low);

            if (err != 0) Log.Information("ERROR: STV0910 read agc2 gain\n");

            return err;
        }

        public byte stv0910_read_power(byte demod, ref byte power_i, ref byte power_q)
        {
            byte err = 0;

            err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_POWERI : stv0910_regs.RSTV0910_P1_POWERI, ref power_i);
            if (err == 0) err = stv0910_read_reg(demod == STV0910_DEMOD_TOP ? stv0910_regs.RSTV0910_P2_POWERQ : stv0910_regs.RSTV0910_P1_POWERQ, ref power_q);

            if (err != 0) Log.Information("ERROR: STV0910 read power");

            return err;
        }

        // MiniTioune (I2C captures) never starts an acquisition with 0x15 alone: the state machine is reset first (DMDISTATE 0x1F), then the carrier registers
        // (CARCFG, CFRUP / CFRLOW, DMDCFGMD, DMDCFG2, RTC, CARFREQ, CARHDR) are written again, then 0x1F once more and only then 0x15. Without the reset the loops
        // start from what the previous tune left in them - measured: at 20 kS carrier and SFR were right, TMGLOCK stayed at 0 (MiniTioune: 44 .. 54).
        private byte stv0910_rewrite_low_sr_carrier(byte demod)
        {
            bool top = demod == STV0910_DEMOD_TOP;
            int index = top ? 0 : 1;
            int cfr_up = lowsr_cfr_up[index];
            byte err = 0;

            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CARCFG : stv0910_regs.RSTV0910_P1_CARCFG, (byte)((0x46 & 0xFC) | (CarrierPhaseAlgo & 0x03)));
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CFRUP1 : stv0910_regs.RSTV0910_P1_CFRUP1, (byte)(cfr_up >> 8));
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CFRUP0 : stv0910_regs.RSTV0910_P1_CFRUP0, (byte)(cfr_up & 0xFF));
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CFRLOW1 : stv0910_regs.RSTV0910_P1_CFRLOW1, 0x00);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CFRLOW0 : stv0910_regs.RSTV0910_P1_CFRLOW0, 0x00);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_DMDCFGMD : stv0910_regs.RSTV0910_P1_DMDCFGMD, lowsr_dmdcfgmd[index]);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_DMDCFG2 : stv0910_regs.RSTV0910_P1_DMDCFG2, 0x3B);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_RTC : stv0910_regs.RSTV0910_P1_RTC, 0x68);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CARFREQ : stv0910_regs.RSTV0910_P1_CARFREQ, 0xAC);
            if (err == 0) err = stv0910_write_reg(top ? stv0910_regs.RSTV0910_P2_CARHDR : stv0910_regs.RSTV0910_P1_CARHDR, 0x40);

            return err;
        }

        public byte stv0910_start_scan(byte demod)
        {
            byte err = 0;
            bool top = demod == STV0910_DEMOD_TOP;
            ushort dmdistate = top ? stv0910_regs.RSTV0910_P2_DMDISTATE : stv0910_regs.RSTV0910_P1_DMDISTATE;

            Log.Information("Flow: STV0910 start scan");

            if (err == 0) err = stv0910_write_reg(dmdistate, 0x1F);
            if (err == 0 && lowsr_applied[top ? 0 : 1]) err = stv0910_rewrite_low_sr_carrier(demod);
            if (err == 0) err = stv0910_write_reg(dmdistate, 0x1F);
            if (err == 0) err = stv0910_write_reg(dmdistate, STV0910_SCAN_BLIND_BEST_GUESS);

            if (err != 0) Log.Information("ERROR: STV0910 start scan");

            return err;
        }

        // Stops the demodulator (issue #9 "Stop TS"): same DMDISTATE value stv0910_init() sets at
        // start, so the tuner sits idle until the next stv0910_start_scan() - no lock, no TS, nothing
        // to reconfigure on the tuner chip itself.
        public byte stv0910_stop_demod(byte demod)
        {
            bool top = demod == STV0910_DEMOD_TOP;
            ushort dmdistate = top ? stv0910_regs.RSTV0910_P2_DMDISTATE : stv0910_regs.RSTV0910_P1_DMDISTATE;

            byte err = stv0910_write_reg(dmdistate, STV0910_DEMOD_STOP);

            if (err != 0) Log.Information("ERROR: STV0910 stop demod");

            return err;
        }

        public byte stv0910_read_scan_state(byte demod, ref byte state)
        {
            byte err = 0;

            if (err == 0) err = stv0910_read_reg_field((demod == STV0910_DEMOD_TOP ?
                                            stv0910_regs.FSTV0910_P2_HEADER_MODE : stv0910_regs.FSTV0910_P1_HEADER_MODE), ref state);

            return err;
        }

    }
}
