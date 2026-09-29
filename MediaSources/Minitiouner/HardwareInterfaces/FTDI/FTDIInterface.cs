
// mostly ported from longmynd - https://github.com/myorangedragon/longmynd - Heather Lomond

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FTD2XX_NET;
using System.Threading;
using FlyleafLib.MediaFramework.MediaDevice;
using opentuner.MediaSources.Minitiouner;
using opentuner.MediaSources.Minitiouner.HardwareInterfaces;
using Serilog;

namespace opentuner
{
    public class FTDIInterface : MTHardwareInterface
    {
        public const byte TS1 = 0;
        public const byte TS2 = 1;

        // ###### I2C Library defines ######
        const byte I2C_Dir_SDAin_SCLin = 0x00;
        const byte I2C_Dir_SDAin_SCLout = 0x01;
        const byte I2C_Dir_SDAout_SCLout = 0x03;
        const byte I2C_Dir_SDAout_SCLin = 0x02;
        const byte I2C_Data_SDAhi_SCLhi = 0x03;
        const byte I2C_Data_SDAlo_SCLhi = 0x01;
        const byte I2C_Data_SDAlo_SCLlo = 0x00;
        const byte I2C_Data_SDAhi_SCLlo = 0x02;

        // MPSSE clocking commands
        const byte MSB_FALLING_EDGE_CLOCK_BYTE_IN = 0x24;
        const byte MSB_RISING_EDGE_CLOCK_BYTE_IN = 0x20;
        const byte MSB_FALLING_EDGE_CLOCK_BYTE_OUT = 0x11;
        const byte MSB_DOWN_EDGE_CLOCK_BIT_IN = 0x26;
        const byte MSB_UP_EDGE_CLOCK_BYTE_IN = 0x20;
        const byte MSB_UP_EDGE_CLOCK_BYTE_OUT = 0x10;
        const byte MSB_RISING_EDGE_CLOCK_BIT_IN = 0x22;
        const byte MSB_FALLING_EDGE_CLOCK_BIT_OUT = 0x13;

        // Clock
        const uint ClockDivisor = 0x0095;

        // Sending and receiving
        static uint NumBytesToSend = 0;
        uint NumBytesSent = 0;
        static uint NumBytesRead = 0;
        static byte[] MPSSEbuffer = new byte[500];
        static byte[] InputBuffer = new byte[500];
        static byte[] InputBuffer2 = new byte[500];
        static uint BytesAvailable = 0;
        static byte I2C_Status = 0;
        public bool Running = true;

        FTD2XX_NET.FTDI.FT_STATUS ftStatus = FTD2XX_NET.FTDI.FT_STATUS.FT_OK;
        FTD2XX_NET.FTDI ftdiDevice_i2c = new FTD2XX_NET.FTDI();

        // Second, physically separate FT2232H chip on MiniTiounerPro V2 boards ("MiniTiouner_Pro_TS1
        // A") - carries I2C-EXT (unused so far) and the EXTERN-0..7 GPIO outputs (AC0-AC7, driving
        // U12/ULN2803 -> 8 LED headers, see schematic sheet 2/5). Deliberately kept separate from
        // ftdiDevice_i2c/MPSSEbuffer/etc. above: it's a different USB device, and EXTERN writes need
        // to be safe to call from the UI thread at any time without racing NimThread's I2C traffic
        // on the static MPSSEbuffer.
        FTD2XX_NET.FTDI ftdiDevice_aux = new FTD2XX_NET.FTDI();
        bool aux_available = false;
        byte aux_gpio_highbyte_value = 0x00;
        const byte AUX_GPIO_HIGHBYTE_DIRECTION = 0xFF; // EXTERN-0..7 are all outputs
        FTD2XX_NET.FTDI ftdiDevice_ts = new FTD2XX_NET.FTDI();
        FTD2XX_NET.FTDI ftdiDevice_ts2 = new FTD2XX_NET.FTDI();

        // high byte
        // Default GPIO value 0x67 = 0b01100111 = LNB-2 Bias Off, NIM not reset (bit7/SEL_LNB2
        // and bit3/EN_LNB2 both 0 - see hw_set_polarization_supply). Was 0x6f (bit3=1, i.e.
        // EN_LNB2 briefly "on" at connect) before the EN_LNB2 pin mapping fix below moved
        // ENABLE from bit4 to bit3 - the old default value was tuned for the old (wrong) mapping.
        byte ftdi_gpio_highbyte_value = 0x67;

        // Default GPIO direction 0xf9 = 0b11111001 = LNB-2 pins (bit3, bit7), LED1/LED2 (bit5/6)
        // and NIM Reset (bit0) are outputs, TS2SYNC (bit1) and the unused bit2 are inputs.
        // (Was 0xf1/bit3-as-input before the EN_LNB2 pin mapping fix below - AC3 needs to be an
        // output for EN_LNB2 to actually drive anything.)
        byte ftdi_gpio_highbyte_direction = 0xf9;

        // low byte
        byte ftdi_gpio_lowbyte_value = 0x00;

        // Default GPIO direction 0x3F = 0b00111111 = AD0-AD5 (I2C bit-bang SCL/SDA, unused
        // AD2/AD3, SEL_LNB1/EN_LNB1) are outputs, AD6/AD7 are inputs. AD6/AD7 are NOT spare
        // GPIOs - per the schematic (sheet 3) they're the midpoint of a resistor divider that
        // also carries the LNB1/LNB2 indicator LEDs' (D216/D215) current: LNB{1,2} supply rail
        // -> R49/R47 (470R) -> AD6/AD7 -> R57/R2 (1k) + LED -> GND. Configuring them as outputs
        // (the previous 0xFF, all-output default) makes the FT2232H actively pull that node to
        // 0V, diverting most of the LED's current straight to the pin instead of through the
        // LED - the LED then stays dark/dim regardless of the real LNB voltage, independent of
        // whether EN_LNB1/EN_LNB2 are actually being set correctly.
        byte ftdi_gpio_lowbyte_direction = 0x3F;

        // high byte pins (U6/FT2232HL-1, MiniTiounerPro V2 schematic sheet 4)
        const byte FTDI_GPIO_PINID_NIM_RESET = 0;
        const byte FTDI_GPIO_PINID_TS2SYNC = 1;
        const byte FTDI_GPIO_PINID_LED1 = 5;      // AC5, hw_ts_led - confirmed NOT an LNB pin
        const byte FTDI_GPIO_PINID_LED2 = 6;      // AC6, hw_ts_led - confirmed NOT an LNB pin

        // LNB-2/LNB-B: AC4 = EN_LNB2, AC7 = SEL_LNB2 (confirmed directly against the schematic by
        // the user - a previous "fix" in this file had this mismapped to bit3, which is not
        // connected to EN_LNB2 at all, so the real enable bit (bit4) was never actually driven).
        const byte FTDI_GPIO_PINID_LNB2_BIAS_ENABLE = 4;
        const byte FTDI_GPIO_PINID_LNB2_BIAS_VSEL = 7;

        // LNB-1/LNB-A: AD4 = SEL_LNB1, AD5 = EN_LNB1 - on the LOW byte, not the high byte (was
        // previously applied to lnb_num==0 using highbyte writes instead - see
        // hw_set_polarization_supply, which had LNB-A and LNB-B's byte swapped outright).
        const byte FTDI_GPIO_PINID_LNB1_BIAS_VSEL = 4;
        const byte FTDI_GPIO_PINID_LNB1_BIAS_ENABLE = 5;

        public override bool RequireSerialTS => false;

        public override string GetName => "FTDI Module";

        private byte Receive_Data_i2c(uint BytesToRead)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                return Receive_Data_i2c_inner(BytesToRead);
            }
            finally
            {
                opentuner.Utilities.I2cStats.Add(t0);
            }
        }

        private byte Receive_Data_i2c_inner(uint BytesToRead)
        {
            uint NumBytesInQueue = 0;
            uint QueueTimeOut = 0;
            uint Buffer1Index = 0;
            uint Buffer2Index = 0;
            uint TotalBytesRead = 0;
            bool QueueTimeoutFlag = false;
            uint NumBytesRxd = 0;

            // Keep looping until all requested bytes are received or we've tried 5000 times (value can be chosen as required)
            while ((TotalBytesRead < BytesToRead) && (QueueTimeoutFlag == false))
            {
                ftStatus = ftdiDevice_i2c.GetRxBytesAvailable(ref NumBytesInQueue);       // Check bytes available

                if ((NumBytesInQueue > 0) && (ftStatus == FTD2XX_NET.FTDI.FT_STATUS.FT_OK))
                {
                    ftStatus = ftdiDevice_i2c.Read(InputBuffer, NumBytesInQueue, ref NumBytesRxd);  // if any available read them

                    if ((NumBytesInQueue == NumBytesRxd) && (ftStatus == FTD2XX_NET.FTDI.FT_STATUS.FT_OK))
                    {
                        Buffer1Index = 0;

                        while (Buffer1Index < NumBytesRxd)
                        {
                            InputBuffer2[Buffer2Index] = InputBuffer[Buffer1Index];     // copy into main overall application buffer
                            Buffer1Index++;
                            Buffer2Index++;
                        }
                        TotalBytesRead = TotalBytesRead + NumBytesRxd;                  // Keep track of total
                    }
                    else
                        return 1;

                    QueueTimeOut++;
                    if (QueueTimeOut == 5000)
                        QueueTimeoutFlag = true;
                    else
                        Thread.Sleep(0);                                                // Avoids running Queue status checks back to back
                }
            }
            // returning globals NumBytesRead and the buffer InputBuffer2
            NumBytesRead = TotalBytesRead;

            if (QueueTimeoutFlag == true)
                return 1;
            else
                return 0;
        }


        //###################################################################################################################################
        // Write a buffer of data and check that it got sent without error

        private byte Send_Data_i2c(uint BytesToSend)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                return Send_Data_i2c_inner(BytesToSend);
            }
            finally
            {
                opentuner.Utilities.I2cStats.Add(t0);
            }
        }

        private byte Send_Data_i2c_inner(uint BytesToSend)
        {
            NumBytesToSend = BytesToSend;

            // Send data. This will return once all sent or if times out
            ftStatus = ftdiDevice_i2c.Write(MPSSEbuffer, NumBytesToSend, ref NumBytesSent);

            // Ensure that call completed OK and that all bytes sent as requested
            if ((NumBytesSent != NumBytesToSend) || (ftStatus != FTD2XX_NET.FTDI.FT_STATUS.FT_OK))
                return 1;   // error   calling function can check NumBytesSent to see how many got sent
            else
                return 0;   // success
        }


        private byte FlushBuffer(FTD2XX_NET.FTDI ftdi)
        {
            ftStatus = ftdi.GetRxBytesAvailable(ref BytesAvailable);	 // Get the number of bytes in the receive buffer
            if (ftStatus != FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                return 1;

            if (BytesAvailable > 0)
            {
                ftStatus = ftdi.Read(InputBuffer, BytesAvailable, ref NumBytesRead);  	//Read out the data from receive buffer
                if (ftStatus != FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                    return 1;       // error
                else
                    return 0;       // all bytes successfully read
            }
            else
            {
                return 0;           // there were no bytes to read
            }
        }


        // ***********************************************
        private byte ftdi_set_mpsse_mode(FTD2XX_NET.FTDI ftdi)
        {

            Log.Information("Flow: FTDI set mpsse mode");

            NumBytesToSend = 0;

            // ***** Initial device configuration ****

            ftStatus = FTD2XX_NET.FTDI.FT_STATUS.FT_OK;
            ftStatus |= ftdi.SetTimeouts(5000, 5000);
            ftStatus |= ftdi.SetLatency(16);
            //ftStatus |= ftdi.SetFlowControl(FTDI.FT_FLOW_CONTROL.FT_FLOW_RTS_CTS, 0x00, 0x00);
            ftStatus |= ftdi.SetBitMode(0x00, 0x00);
            ftStatus |= ftdi.SetBitMode(0x00, 0x02);         // MPSSE mode        

            Thread.Sleep(10);

            if (ftStatus != FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                return 1; // error();


            return 0;
        }

        private byte ftdi_set_ftdi_io(FTD2XX_NET.FTDI ftdi)
        {
            //***** Flush the buffer *****
            I2C_Status = FlushBuffer(ftdi);

            //***** Synchronize the MPSSE interface by sending bad command 0xAA *****
            NumBytesToSend = 0;
            MPSSEbuffer[NumBytesToSend++] = 0xAA;
            I2C_Status = Send_Data_i2c(NumBytesToSend);
            if (I2C_Status != 0) return 1; // error();
            I2C_Status = Receive_Data_i2c(2);
            if (I2C_Status != 0) return 1; //error();

            if ((InputBuffer2[0] == 0xFA) && (InputBuffer2[1] == 0xAA))
            {
                //MessageBox.Show("MPSSE Synced");
            }
            else
            {
                return 1;            //error();
            }

            //***** Synchronize the MPSSE interface by sending bad command 0xAB *****
            NumBytesToSend = 0;
            MPSSEbuffer[NumBytesToSend++] = 0xAB;
            I2C_Status = Send_Data_i2c(NumBytesToSend);
            if (I2C_Status != 0) return 1; // error();
            I2C_Status = Receive_Data_i2c(2);
            if (I2C_Status != 0) return 1; //error();

            if ((InputBuffer2[0] == 0xFA) && (InputBuffer2[1] == 0xAB))
            {
                //MessageBox.Show("MPSSE Synced");
            }
            else
            {
                return 1;            //error();
            }

            NumBytesToSend = 0;

            MPSSEbuffer[NumBytesToSend++] = 0x8A; 	// Disable clock divide by 5 for 60Mhz master clock
            MPSSEbuffer[NumBytesToSend++] = 0x97;	// Disable adaptive clocking
            MPSSEbuffer[NumBytesToSend++] = 0x8D;   // Disable 3 phase data clocking

            I2C_Status = Send_Data_i2c(NumBytesToSend);
            if (I2C_Status != 0)
            {
                return 1;            //error();
            }

            NumBytesToSend = 0;

            // Use the live default value/direction fields (set near their declarations above)
            // instead of separate hardcoded literals here - this used to send its own stale
            // 0x00/0xFF/0x6F/0xF1 regardless of what those fields were configured to, silently
            // overwriting the real defaults for one initial MPSSE transaction at connect time.
            MPSSEbuffer[NumBytesToSend++] = 0x80; // set ouput, low byte
            MPSSEbuffer[NumBytesToSend++] = ftdi_gpio_lowbyte_value;
            MPSSEbuffer[NumBytesToSend++] = ftdi_gpio_lowbyte_direction;

            MPSSEbuffer[NumBytesToSend++] = 0x82; // set output, high byte
            MPSSEbuffer[NumBytesToSend++] = ftdi_gpio_highbyte_value;
            MPSSEbuffer[NumBytesToSend++] = ftdi_gpio_highbyte_direction;

            MPSSEbuffer[NumBytesToSend++] = 0x86; 	//Command to set clock divisor
            MPSSEbuffer[NumBytesToSend++] = (byte)(ClockDivisor & 0x00FF);	//Set 0xValueL of clock divisor
            MPSSEbuffer[NumBytesToSend++] = (byte)((ClockDivisor >> 8) & 0x00FF);	//Set 0xValueH of clock divisor

            I2C_Status = Send_Data_i2c(NumBytesToSend);
            if (I2C_Status != 0)
            {
                return 1;            //error();
            }

            Thread.Sleep(30);

            NumBytesToSend = 0;

            MPSSEbuffer[NumBytesToSend++] = 0x85; 			// loopback off
            I2C_Status = Send_Data_i2c(NumBytesToSend);
            NumBytesToSend = 0;

            if (I2C_Status != 0)
            {
                return 1;            //error();
            }

            Thread.Sleep(30);

            return 0;
        }

        byte ftdi_nim_reset()
        {
            // byte err = I2C_SetGPIOValuesHigh(0, 0);
            byte err = ftdi_gpio_write_highbyte(0, false);

            Thread.Sleep(10);

            if (err != 0)
                return 1;

            ftdi_gpio_write_highbyte(0, true);
            //err = I2C_SetGPIOValuesHigh(1, 1);
            Thread.Sleep(10);

            return err;
        }

        // The I2C bit-bang sequences below need SCL(bit0)/SDA(bit1) direction to switch between
        // output (driving) and input (released, so the slave can pull SDA low for ACK - DO/DI
        // are tied together externally to form one bidirectional SDA line, per the FTDI MPSSE
        // I2C app note) several times per byte. That part of the direction byte is fixed by the
        // I2C protocol itself, encoded in the low nibble below (0x3=SCL+SDA out, 0x1=SCL out/SDA
        // in, 0x0=both in). The HIGH nibble (bits 4-7: EN_LNB1/SEL_LNB1/AD6/AD7 etc.) must NOT be
        // hardcoded - it needs to reflect whatever ftdi_gpio_lowbyte_direction currently is, or
        // every single I2C transaction (i.e. constantly, every ~200ms NIM status poll) silently
        // forces AD6/AD7 back to output regardless of ftdi_gpio_lowbyte_direction.
        byte I2cDir(byte low_nibble) => (byte)((ftdi_gpio_lowbyte_direction & 0xF0) | low_nibble);

        byte ftdi_i2c_set_start()
        {
            int count;

            // FTDI_STOP_START_REPEATS = 4

            for (count = 0; count < 4; count++)
            {
                MPSSEbuffer[NumBytesToSend++] = 0x80;
                MPSSEbuffer[NumBytesToSend++] = (byte)(0x03 | ftdi_gpio_lowbyte_value);
                MPSSEbuffer[NumBytesToSend++] = I2cDir(0x03);
            }

            for (count = 0; count < 4; count++)
            {
                MPSSEbuffer[NumBytesToSend++] = 0x80;
                MPSSEbuffer[NumBytesToSend++] = (byte)(0x01 | ftdi_gpio_lowbyte_value);
                MPSSEbuffer[NumBytesToSend++] = I2cDir(0x03);
            }

            return 0;
        }

        byte ftdi_i2c_set_stop()
        {
            int count;

            // FTDI_STOP_START_REPEATS = 4

            for (count = 0; count < 4; count++)
            {
                MPSSEbuffer[NumBytesToSend++] = 0x80;
                MPSSEbuffer[NumBytesToSend++] = (byte)(0x01 | ftdi_gpio_lowbyte_value);
                MPSSEbuffer[NumBytesToSend++] = I2cDir(0x03);
            }

            for (count = 0; count < 4; count++)
            {
                MPSSEbuffer[NumBytesToSend++] = 0x80;
                MPSSEbuffer[NumBytesToSend++] = (byte)(0x03 | ftdi_gpio_lowbyte_value);
                MPSSEbuffer[NumBytesToSend++] = I2cDir(0x03);
            }

            MPSSEbuffer[NumBytesToSend++] = 0x80;
            MPSSEbuffer[NumBytesToSend++] = (byte)(0x03 | ftdi_gpio_lowbyte_value);
            MPSSEbuffer[NumBytesToSend++] = I2cDir(0x00);
            return 0;
        }


        public byte ftdi_i2c_send_byte_check_ack(byte b)
        {
            byte err;

            MPSSEbuffer[NumBytesToSend++] = 0x80; // low byte
            MPSSEbuffer[NumBytesToSend++] = (byte)(0x00 | ftdi_gpio_lowbyte_value); // value
            MPSSEbuffer[NumBytesToSend++] = I2cDir(0x03); // direction

            MPSSEbuffer[NumBytesToSend++] = 0x11; // clock data bytes out
            MPSSEbuffer[NumBytesToSend++] = 0x00; // length l
            MPSSEbuffer[NumBytesToSend++] = 0x00; // length h
            MPSSEbuffer[NumBytesToSend++] = b;    // byte

            MPSSEbuffer[NumBytesToSend++] = 0x80; // low byte
            MPSSEbuffer[NumBytesToSend++] = (byte)(0x00 | ftdi_gpio_lowbyte_value); // value
            MPSSEbuffer[NumBytesToSend++] = I2cDir(0x01); // direction

            MPSSEbuffer[NumBytesToSend++] = 0x27; // ?
            MPSSEbuffer[NumBytesToSend++] = 0x00; // ?
            MPSSEbuffer[NumBytesToSend++] = 0x87; // ?

            err = Send_Data_i2c(NumBytesToSend);
            NumBytesToSend = 0;

            if (err == 0)
                err = Receive_Data_i2c(1);

            uint i = NumBytesRead;

            if (err == 0 && (InputBuffer2[0] & 0x01) != 0)
            {
                err = 18;
            }

            return err;
        }

        public byte ftdi_i2c_read_byte_send_nak(ref byte b)
        {
            byte err;

            MPSSEbuffer[NumBytesToSend++] = 0x80;
            MPSSEbuffer[NumBytesToSend++] = (byte)(0x00 | ftdi_gpio_lowbyte_value);
            MPSSEbuffer[NumBytesToSend++] = I2cDir(0x03);

            MPSSEbuffer[NumBytesToSend++] = 0x80;
            MPSSEbuffer[NumBytesToSend++] = (byte)(0x00 | ftdi_gpio_lowbyte_value);
            MPSSEbuffer[NumBytesToSend++] = I2cDir(0x01);

            MPSSEbuffer[NumBytesToSend++] = 0x25; // ?
            MPSSEbuffer[NumBytesToSend++] = 0x00; // ?
            MPSSEbuffer[NumBytesToSend++] = 0x00; // ?
            MPSSEbuffer[NumBytesToSend++] = 0x87; // ?


            err = Send_Data_i2c(NumBytesToSend);
            NumBytesToSend = 0;

            if (err == 0)
                err = Receive_Data_i2c(1);

            b = InputBuffer2[0];

            return err;

        }


        byte ftdi_i2c_output()
        {
            byte err;

            err = Send_Data_i2c(NumBytesToSend);
            NumBytesToSend = 0;

            return err;
        }

        public override byte nim_read_reg8(byte addr, byte reg, ref byte val)
        {
            byte err = 0;
            int i = 0;
            int timeout = 0;

            do
            {
                for (i = 0; i < 10; i++)
                {
                    err = ftdi_i2c_set_start();
                    err |= ftdi_i2c_send_byte_check_ack(addr);
                    err |= ftdi_i2c_send_byte_check_ack(reg);
                    err |= ftdi_i2c_set_stop();
                    err |= ftdi_i2c_output();
                    if (err == 0) break;
                }

                if (err == 0)
                {
                    for (i = 0; i < 10; i++)
                    {
                        err = ftdi_i2c_set_start();
                        err |= ftdi_i2c_send_byte_check_ack((byte)(addr | 0x01));
                        err |= ftdi_i2c_read_byte_send_nak(ref val);
                        err |= ftdi_i2c_set_stop();
                        err |= ftdi_i2c_output();
                        if (err == 0) break;
                    }
                }


            } while (err != 0 && (timeout != 100));

            return err;
        }

        public override byte nim_write_reg8(byte addr, byte reg, byte val)
        {
            byte err = 0;
            int i;
            int timeout = 0;

            do
            {
                for (i = 0; i < 10; i++)
                {
                    err = ftdi_i2c_set_start();
                    err |= ftdi_i2c_send_byte_check_ack(addr);
                    err |= ftdi_i2c_send_byte_check_ack(reg);
                    err |= ftdi_i2c_send_byte_check_ack(val);
                    err |= ftdi_i2c_set_stop();
                    err |= ftdi_i2c_output();

                    if (err == 0) break;
                }

            } while ((err != 0) && timeout != 100);

            return err;
        }

        public override byte nim_write_reg16(byte addr, ushort reg, byte val)
        {

            byte err = 0;
            short i;
            short timeout = 0;

            do
            {
                for (i = 0; i < 10; i++)
                {
                    err = ftdi_i2c_set_start();
                    err |= ftdi_i2c_send_byte_check_ack(addr);
                    err |= ftdi_i2c_send_byte_check_ack((byte)(reg >> 8));
                    err |= ftdi_i2c_send_byte_check_ack((byte)(reg & 0xFF));
                    err |= ftdi_i2c_send_byte_check_ack(val);
                    err |= ftdi_i2c_set_stop();
                    err |= ftdi_i2c_output();

                    if (err == 0)
                        break;
                }

                timeout += 1;
            } while (err != 0 && timeout != 100);

            if (err != 0)
            {
                //MessageBox.Show("Error Write Reg 16");
            }

            return err;
        }

        public override byte nim_read_reg16(byte addr, ushort reg, ref byte val)
        {

            byte err = 0;
            int i = 0;
            int timeout = 0;

            do
            {
                for (i = 0; i < 10; i++)
                {
                    err = ftdi_i2c_set_start();
                    err |= ftdi_i2c_send_byte_check_ack(addr);
                    err |= ftdi_i2c_send_byte_check_ack((byte)(reg >> 8));
                    err |= ftdi_i2c_send_byte_check_ack((byte)(reg & 0xff));
                    if (err == 0)
                        break;
                }

                if (err == 0)
                {
                    for (i = 0; i < 10; i++)
                    {
                        err = ftdi_i2c_set_start();
                        err |= ftdi_i2c_send_byte_check_ack((byte)(addr | 0x01));
                        err |= ftdi_i2c_read_byte_send_nak(ref val);
                        err |= ftdi_i2c_set_stop();
                        err |= ftdi_i2c_output();
                        if (err == 0)
                            break;
                    }
                }

                timeout += 1;

            } while (err != 0 && timeout != 100);

            if (err != 0)
            {
                //MessageBox.Show("Error Read Reg 16");
            }

            return err;
        }

        // Generic raw I2C write - addr is the 7-bit I2C address, shifted here into the
        // 8-bit write-address byte the same way NIM_TUNER_ADDR/NIM_DEMOD_ADDR already are.
        // Unlike nim_write_reg8/16, there's no register byte: the whole payload is written
        // as one continuous START..STOP transaction, one byte at a time via the same
        // ftdi_i2c_send_byte_check_ack() used by the nim_write_* functions above (each call
        // flushes/resets the shared MPSSEbuffer itself, so arbitrary payload lengths are
        // safe here despite the buffer's fixed 500-byte size).
        public override byte i2c_write_raw(byte addr, byte[] data)
        {
            byte err = 0;
            int timeout = 0;
            byte write_addr = (byte)(addr << 1);

            do
            {
                for (int i = 0; i < 10; i++)
                {
                    err = ftdi_i2c_set_start();
                    err |= ftdi_i2c_send_byte_check_ack(write_addr);

                    for (int d = 0; d < data.Length && err == 0; d++)
                    {
                        err |= ftdi_i2c_send_byte_check_ack(data[d]);
                    }

                    err |= ftdi_i2c_set_stop();
                    err |= ftdi_i2c_output();

                    if (err == 0) break;
                }

                timeout += 1;
            } while (err != 0 && timeout != 100);

            return err;
        }

        // get a list of all detected ft2232 devices
        public List<FTDIDevice> detect_all_ftdi()
        {
            uint device_count = 0;
            List<FTDIDevice> ftdi_devices = new List<FTDIDevice>();

            try
            {
                ftStatus = ftdiDevice_i2c.GetNumberOfDevices(ref device_count);
                Log.Information("Number of FTDI Devices: " + device_count.ToString());

                for (uint c = 0; c < device_count; c++)
                {
                    FTD2XX_NET.FTDI ftdi_device = new FTD2XX_NET.FTDI();
                    ftdi_device.OpenByIndex(c);

                    FTD2XX_NET.FTDI.FT_DEVICE device = new FTD2XX_NET.FTDI.FT_DEVICE();
                    ftdi_device.GetDeviceType(ref device);

                    // is this a ft2232 device?
                    if (device.ToString() != "FT_DEVICE_2232H")
                    {
                        ftdi_device.Close();
                        continue;
                    }

                    FTDIDevice detected_ftdi_device = new FTDIDevice();

                    // device index
                    detected_ftdi_device.device_index = c;

                    // device serial number
                    ftdi_device.GetSerialNumber(out string SerialNumber);
                    detected_ftdi_device.device_serial_number = SerialNumber;

                    // lets get description
                    ftdi_device.GetDescription(out string DeviceName);
                    detected_ftdi_device.device_description = DeviceName;

                    ftdi_device.Close();

                    ftdi_devices.Add(detected_ftdi_device);
                }
            }
            catch (Exception ex)
            {
                Log.Error("FTDI Detect Error: " + ex.Message);
            }

            return ftdi_devices;
        }

        public override byte hw_detect(ref uint i2c_port, ref uint ts_port, ref uint ts_port2, ref uint aux_port, ref string detectedDeviceName, string i2c_serial, string ts_serial, string ts2_serial, string aux_serial)
        {
            byte err = 0;

            uint devcount = 0;

            ts_port = 99;
            i2c_port = 99;
            ts_port2 = 99;
            aux_port = 99;

            detectedDeviceName = "Manual";

            try
            {
                ftStatus = ftdiDevice_i2c.GetNumberOfDevices(ref devcount);
                Log.Information("Number of FTDI Devices: " + devcount.ToString());

                // we need atleast two ports
                if (devcount < 2)
                {
                    Log.Error("Not enough FTDI devices detected");
                    return 1;
                }

                for (uint c = 0; c < devcount; c++)
                {
                    FTD2XX_NET.FTDI ftdi_device = new FTD2XX_NET.FTDI();
                    ftdi_device.OpenByIndex(c);

                    FTD2XX_NET.FTDI.FT_DEVICE device = new FTD2XX_NET.FTDI.FT_DEVICE();
                    ftdi_device.GetDeviceType(ref device);

                    ftdi_device.GetSerialNumber(out string SerialNumber);

                    Log.Information("Serial Number: " + SerialNumber.ToString());

                    if (SerialNumber == i2c_serial)
                    {
                        i2c_port = c;
                        ftdi_device.Close();
                        continue;
                    }

                    if (SerialNumber == ts_serial)
                    {
                        ts_port = c;
                        ftdi_device.Close();
                        continue;
                    }

                    if (SerialNumber == ts2_serial)
                    {
                        ts_port2 = c;
                        ftdi_device.Close();
                        continue;
                    }

                    if (!string.IsNullOrEmpty(aux_serial) && SerialNumber == aux_serial)
                    {
                        aux_port = c;
                        ftdi_device.Close();
                        continue;
                    }

                    ftdi_device.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Error("FTDI Error: " + ex.Message);
                return 1;
            }


            return err;
        }

        // Summary of the last description based detection (boards found / used / ignored), for the log and
        // the Hardware Info window.
        public string DetectionSummary { get; private set; } = "";

        // Chip serial number of the board to use when several boards are connected ("" = automatic:
        // most TS streams first). From the Minitiouner settings.
        public string PreferredBoardSerial { get; set; } = "";

        public override byte hw_detect(ref uint i2c_port, ref uint ts_port, ref uint ts_port2, ref uint aux_port, ref string detectedDeviceName)
        {
            Log.Information("**** FTDI Port(s) Detection ****");

            ts_port = 99;
            i2c_port = 99;
            ts_port2 = 99;
            aux_port = 99;
            DetectionSummary = "";

            var devices = HardwareInfo.EnumerateFtdiDevices(out string list_error);
            Log.Information("Number of FTDI Devices: " + devices.Count.ToString());

            if (list_error != null)
            {
                Log.Error("FTDI Error: " + list_error);
                return 1;
            }

            // we need atleast two ports
            if (devices.Count < 2)
            {
                Log.Error("Not enough FTDI devices detected");
                return 1;
            }

            foreach (var d in devices)
            {
                Log.Information(d.Index.ToString() + ": " + d.Type + ", serial " + d.Serial + ", description \"" + d.Description + "\""
                    + (d.Role != "-" ? " -> " + d.Role + " of " + d.Board : " (skipped)"));
            }

            // group the channels per board instead of taking the last match per role
            var warnings = new List<string>();
            var boards = BoardDetection.Detect(devices, warnings);
            foreach (var w in warnings)
                Log.Warning("FTDI board detection: " + w);

            var board = BoardDetection.Select(boards, PreferredBoardSerial);

            var summary = new StringBuilder();
            summary.Append(boards.Count + " board(s) found");
            foreach (var b in boards)
                summary.Append("; " + (b == board ? "USED " : "ignored ") + b.Name + ", " + b.TsCount + " TS" + (b.Complete ? "" : ", incomplete"));
            foreach (var w in warnings)
                summary.Append("; " + w);
            DetectionSummary = summary.ToString();
            Log.Information("FTDI boards: " + DetectionSummary);

            if (boards.Count > 1)
                Log.Warning("FTDI board detection: " + boards.Count + " boards connected, only " + (board != null ? board.Name : "none") + " is used (PreferredBoardSerial = \"" + PreferredBoardSerial + "\")");

            if (board == null)
                return 0;   // nothing usable: ports stay 99, the caller falls back to the first devices

            i2c_port = board.I2c;
            ts_port = board.Ts;
            ts_port2 = board.Ts2;
            aux_port = board.Aux;
            detectedDeviceName = board.Type;

            Log.Information(" **** ");
            return 0;
        }

        public override bool AuxAvailable => aux_available;

        public override byte hw_init(uint i2c_device, uint ts_device, uint ts_device2, uint aux_device)
        {
            byte err = 0;
            uint devcount = 0;

            // get devices
            try
            {
                ftStatus = ftdiDevice_i2c.GetNumberOfDevices(ref devcount);
                Log.Information("Number of FTDI Devices: " + devcount.ToString());
            }
            catch (Exception ex)
            {
                Log.Error("FTDI Error: " + ex.Message);
                return 1;
            }

            ftStatus = ftdiDevice_i2c.OpenByIndex(i2c_device);

            if (ftStatus != FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
            {
                return 1;
            }

            ftStatus = ftdiDevice_ts.OpenByIndex(ts_device);

            if (ftStatus != FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
            {
                return 1;
            }
            ftStatus = ftdiDevice_ts.SetTimeouts(250, 500);
            if (ftStatus != FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
            {
                return 1;
            }

            if (ts_device2 != 99)
            {
                ftStatus = ftdiDevice_ts2.OpenByIndex(ts_device2);

                if (ftStatus != FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                {
                    return 1;
                }
                ftStatus = ftdiDevice_ts2.SetTimeouts(250, 500);
                if (ftStatus != FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                {
                    return 1;
                }
            }


            err = ftdi_set_mpsse_mode(ftdiDevice_i2c);
            if (err == 0) err = ftdi_set_ftdi_io(ftdiDevice_i2c);
            if (err == 0) err = ftdi_nim_reset();

            // AUX chip (EXTERN-0..7) is optional - a board without it (or an older MiniTiouner
            // variant) just runs without the Switches panel, same as any other undetected port.
            if (aux_device != 99)
            {
                byte aux_err = ftdi_aux_init(aux_device);
                if (aux_err != 0)
                {
                    Log.Information("AUX (EXTERN-0..7) init failed, continuing without it: " + aux_err.ToString());
                }
                aux_available = aux_err == 0;
            }

            return err;
        }

        public override void hw_close()
        {
        }

        // Minimal MPSSE bring-up for the AUX chip - unlike ftdiDevice_i2c this one only ever
        // drives plain GPIO output (EXTERN-0..7 on the high byte), so it doesn't need the full
        // I2C sync/ack machinery (Send_Data_i2c/Receive_Data_i2c/MPSSEbuffer) that's hardwired to
        // ftdiDevice_i2c - deliberately self-contained with its own small local buffer instead.
        byte ftdi_aux_init(uint aux_device)
        {
            ftStatus = ftdiDevice_aux.OpenByIndex(aux_device);
            if (ftStatus != FTD2XX_NET.FTDI.FT_STATUS.FT_OK) return 1;

            byte err = ftdi_set_mpsse_mode(ftdiDevice_aux);
            if (err != 0) return err;

            byte[] init = new byte[]
            {
                0x8A, // disable clock divide by 5
                0x97, // disable adaptive clocking
                0x8D, // disable 3 phase data clocking
                0x80, 0x00, 0x00,                                 // low byte: value 0, all inputs (unused)
                0x82, aux_gpio_highbyte_value, AUX_GPIO_HIGHBYTE_DIRECTION, // high byte: EXTERN-0..7, all outputs, off
                0x86, (byte)(ClockDivisor & 0xFF), (byte)((ClockDivisor >> 8) & 0xFF), // clock divisor
            };

            uint sent = 0;
            ftStatus = ftdiDevice_aux.Write(init, (uint)init.Length, ref sent);

            return (ftStatus == FTD2XX_NET.FTDI.FT_STATUS.FT_OK && sent == init.Length) ? (byte)0 : (byte)1;
        }

        public override byte aux_gpio_write(byte value)
        {
            if (!aux_available) return 1;

            aux_gpio_highbyte_value = value;

            byte[] cmd = new byte[] { 0x82, aux_gpio_highbyte_value, AUX_GPIO_HIGHBYTE_DIRECTION };
            uint sent = 0;
            var st = ftdiDevice_aux.Write(cmd, (uint)cmd.Length, ref sent);

            return (st == FTD2XX_NET.FTDI.FT_STATUS.FT_OK && sent == cmd.Length) ? (byte)0 : (byte)1;
        }

        byte ftdi_gpio_write_lowbyte(byte pin_id, bool pin_value)
        {
            Log.Information("Flow: FTDI GPIO Write: pin {0} -> value {1}", pin_id, pin_value);

            Log.Information("ftdi_gpio_value: before: " + Convert.ToString(ftdi_gpio_lowbyte_value, 2));

            if (pin_value)
            {
                ftdi_gpio_lowbyte_value |= (byte)(1 << pin_id);
            }
            else
            {
                ftdi_gpio_lowbyte_value &= (byte)(~(1 << pin_id));
            }

            Log.Information("ftdi_gpio_value: after: " + Convert.ToString(ftdi_gpio_lowbyte_value, 2));

            NumBytesToSend = 0;
            MPSSEbuffer[NumBytesToSend++] = 0x80; // configure low bytes of mpsse port
            MPSSEbuffer[NumBytesToSend++] = ftdi_gpio_lowbyte_value;
            MPSSEbuffer[NumBytesToSend++] = ftdi_gpio_lowbyte_direction;

            I2C_Status = Send_Data_i2c(NumBytesToSend);

            NumBytesToSend = 0;

            return I2C_Status;
        }

        byte ftdi_gpio_write_highbyte(byte pin_id, bool pin_value)
        {
            Log.Information("Flow: FTDI GPIO Write: pin {0} -> value {1}", pin_id, pin_value);

            Log.Information("ftdi_gpio_highbyte_value: before: " + Convert.ToString(ftdi_gpio_highbyte_value, 2).PadLeft(8,'0'));

            if (pin_value)
            {
                ftdi_gpio_highbyte_value |= (byte)(1 << pin_id);
            }
            else
            {
                ftdi_gpio_highbyte_value &= (byte)(~(1 << pin_id));
            }

            Log.Information("ftdi_gpio_value: after: " + Convert.ToString(ftdi_gpio_highbyte_value, 2).PadLeft(8,'0'));

            NumBytesToSend = 0;
            MPSSEbuffer[NumBytesToSend++] = 0x82; // aka. MPSSE_CMD_SET_DATA_BITS_HIGHBYTE 
            MPSSEbuffer[NumBytesToSend++] = ftdi_gpio_highbyte_value;
            MPSSEbuffer[NumBytesToSend++] = ftdi_gpio_highbyte_direction;

            I2C_Status = Send_Data_i2c(NumBytesToSend);

            NumBytesToSend = 0;

            return I2C_Status;
        }

        public override byte transport_read(int device, ref byte[] data, ref uint bytesRead)
        {
            byte err = 0;

            FTD2XX_NET.FTDI.FT_STATUS ts_ftdi_status = FTD2XX_NET.FTDI.FT_STATUS.FT_OK;

            if (device == TS2)
            {
                ts_ftdi_status = ftdiDevice_ts.Read(data, Convert.ToUInt32(data.Length), ref bytesRead);
            }
            else
            {
                ts_ftdi_status = ftdiDevice_ts2.Read(data, Convert.ToUInt32(data.Length), ref bytesRead);
            }

            //Log.Information(ts_ftdi_status.ToString());

            if (ts_ftdi_status != FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                err = 1;

            return err;
        }

        private byte ftdi_ts_available(int device, ref uint bytes_available)
        {
            byte err = 0;

            FTD2XX_NET.FTDI.FT_STATUS ts_ftdi_status = FTD2XX_NET.FTDI.FT_STATUS.FT_OK;

            if (device == TS2)
                ts_ftdi_status = ftdiDevice_ts.GetRxBytesAvailable(ref bytes_available);
            else
                ts_ftdi_status = ftdiDevice_ts2.GetRxBytesAvailable(ref bytes_available);

            if (ts_ftdi_status != FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                err = 1;

            return err;
        }

        public override byte transport_flush(int device)
        {
            byte err = 0;

            FTD2XX_NET.FTDI.FT_STATUS ts_ftdi_status = FTD2XX_NET.FTDI.FT_STATUS.FT_OK;

            if (device == TS2)
            {
                ts_ftdi_status = ftdiDevice_ts.Purge(FTD2XX_NET.FTDI.FT_PURGE.FT_PURGE_RX);
            }
            else
            {
                ts_ftdi_status = ftdiDevice_ts2.Purge(FTD2XX_NET.FTDI.FT_PURGE.FT_PURGE_RX);
            }

            if (ts_ftdi_status != FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                err = 1;

            return err;
        }


        public override byte hw_ts_led(int led, bool setting)
        {
            byte err = 0;

            switch (led)
            {
                case 0:
                    ftdi_gpio_write_highbyte(FTDI_GPIO_PINID_LED1, !setting);
                    break;
                case 1:
                    ftdi_gpio_write_highbyte(FTDI_GPIO_PINID_LED2, !setting);
                    break;
            }


            return err;
        }

        // on minitiouner pro 2 there are 2 outputs for the 2 different lnb switching - longmynd originally only catered for 1 output, the pro 2 needs two outputs. 
        // need to confirm express and S versions.
        // lnb_num 0 = LNB-A/LNB1 (AD4/AD5, low byte), 1 = LNB-B/LNB2 (AC3/AC7, high byte) - see
        // the pin constants above. supply_horizontal selects SEL (false=13.3V/Vertical,
        // true=18.3V/Horizontal per the RT5047 datasheet); supply_enable drives EN.
        public override byte hw_set_polarization_supply(byte lnb_num, bool supply_enable, bool supply_horizontal)
        {
            byte err = 0;

            if (supply_enable)
            {
                if (lnb_num == 0)
                {
                    ftdi_gpio_write_lowbyte(FTDI_GPIO_PINID_LNB1_BIAS_VSEL, supply_horizontal);
                    ftdi_gpio_write_lowbyte(FTDI_GPIO_PINID_LNB1_BIAS_ENABLE, true);
                }
                else
                {
                    ftdi_gpio_write_highbyte(FTDI_GPIO_PINID_LNB2_BIAS_VSEL, supply_horizontal);
                    ftdi_gpio_write_highbyte(FTDI_GPIO_PINID_LNB2_BIAS_ENABLE, true);
                }
            }
            else
            {
                // disable
                if (lnb_num == 0)
                {
                    ftdi_gpio_write_lowbyte(FTDI_GPIO_PINID_LNB1_BIAS_ENABLE, false);
                    ftdi_gpio_write_lowbyte(FTDI_GPIO_PINID_LNB1_BIAS_VSEL, false);
                }
                else
                {
                    ftdi_gpio_write_highbyte(FTDI_GPIO_PINID_LNB2_BIAS_ENABLE, false);
                    ftdi_gpio_write_highbyte(FTDI_GPIO_PINID_LNB2_BIAS_VSEL, false);
                }
            }

            return err;
        }
    }

    /*
    public class FTDIDevice
    {
        public uint device_index { get; set; }
        public string device_serial_number { get; set; }
        public string device_description { get; set; }
    }
    */

}
