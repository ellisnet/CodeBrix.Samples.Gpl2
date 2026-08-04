//
// Nuked OPL3 emulator, version 1.8.
// Copyright (C) 2013-2020 Nuke.YKT
// Copyright (c) 2026 Jeremy Ellis and contributors (C# port)
//
// Ported to C# for Wolfenstein.Brix from Nuked-OPL3
// (github.com/nukeykt/Nuked-OPL3, commit cfedb09), files opl3.c and
// opl3.h. The port is intended to be bit-exact with the original for
// the same register stream (the OPL3_Generate4Ch and stereo-extension
// paths are omitted; the channel-sample-delay quirk is kept enabled,
// matching the original's defaults).
//
// Nuked OPL3 is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as
// published by the Free Software Foundation, either version 2.1
// of the License, or (at your option) any later version.
//
// Nuked OPL3 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with Nuked OPL3. If not, see
// <https://www.gnu.org/licenses/>.
//

// Thanks (from the original source):
//     MAME Development Team (Jarek Burczynski, Tatsuyuki Satoh):
//         Feedback and Rhythm part calculation information.
//     forums.submarine.org.uk (carbon14, opl3):
//         Tremolo and phase generator calculation information.
//     OPLx decapsulated (Matthew Gambrell, Olli Niemitalo):
//         OPL2 ROMs.
//     siliconpr0n.org (John McMaster, digshadow):
//         YMF262 and VRC VII decaps and die shots.

// Porting notes:
//  - Field, local and table names deliberately mirror the C source
//    (snake_case) to ease side-by-side review against opl3.c.
//  - The C "int16_t *mod" slot modulation-input pointer is replaced by
//    a (mod_source, mod_slot) pair read through SlotMod(); the C
//    "uint8_t *trem" pointer by the tremoloEnabled flag; and the
//    "int16_t *out[4]" channel mix pointers by outSlots (null meaning
//    the always-zero chip->zeromod).
//  - The channel-sample-delay quirk (OPL_QUIRK_CHANNELSAMPLEDELAY) is
//    compiled in, matching the C default when the stereo extension is
//    disabled.

using System;

namespace Wolfenstein.Brix.GameEngine.Audio;

/// <summary>
/// A single emulated Yamaha YMF262 (OPL3) FM-synthesizer chip
/// (C# port of Nuked OPL3 v1.8). Call <see cref="Reset"/> before use.
/// </summary>
public sealed class NukedOpl3
{
    private const uint OPL_WRITEBUF_SIZE = 1024;
    private const uint OPL_WRITEBUF_DELAY = 2;

    private const int RSM_FRAC = 10;

    /* Channel types */

    private const byte ch_2op = 0;
    private const byte ch_4op = 1;
    private const byte ch_4op2 = 2;
    private const byte ch_drum = 3;

    /* Envelope key types */

    private const byte egk_norm = 0x01;
    private const byte egk_drum = 0x02;

    /*
        logsin table
    */

    private static readonly ushort[] logsinrom = {
        0x859, 0x6c3, 0x607, 0x58b, 0x52e, 0x4e4, 0x4a6, 0x471,
        0x443, 0x41a, 0x3f5, 0x3d3, 0x3b5, 0x398, 0x37e, 0x365,
        0x34e, 0x339, 0x324, 0x311, 0x2ff, 0x2ed, 0x2dc, 0x2cd,
        0x2bd, 0x2af, 0x2a0, 0x293, 0x286, 0x279, 0x26d, 0x261,
        0x256, 0x24b, 0x240, 0x236, 0x22c, 0x222, 0x218, 0x20f,
        0x206, 0x1fd, 0x1f5, 0x1ec, 0x1e4, 0x1dc, 0x1d4, 0x1cd,
        0x1c5, 0x1be, 0x1b7, 0x1b0, 0x1a9, 0x1a2, 0x19b, 0x195,
        0x18f, 0x188, 0x182, 0x17c, 0x177, 0x171, 0x16b, 0x166,
        0x160, 0x15b, 0x155, 0x150, 0x14b, 0x146, 0x141, 0x13c,
        0x137, 0x133, 0x12e, 0x129, 0x125, 0x121, 0x11c, 0x118,
        0x114, 0x10f, 0x10b, 0x107, 0x103, 0x0ff, 0x0fb, 0x0f8,
        0x0f4, 0x0f0, 0x0ec, 0x0e9, 0x0e5, 0x0e2, 0x0de, 0x0db,
        0x0d7, 0x0d4, 0x0d1, 0x0cd, 0x0ca, 0x0c7, 0x0c4, 0x0c1,
        0x0be, 0x0bb, 0x0b8, 0x0b5, 0x0b2, 0x0af, 0x0ac, 0x0a9,
        0x0a7, 0x0a4, 0x0a1, 0x09f, 0x09c, 0x099, 0x097, 0x094,
        0x092, 0x08f, 0x08d, 0x08a, 0x088, 0x086, 0x083, 0x081,
        0x07f, 0x07d, 0x07a, 0x078, 0x076, 0x074, 0x072, 0x070,
        0x06e, 0x06c, 0x06a, 0x068, 0x066, 0x064, 0x062, 0x060,
        0x05e, 0x05c, 0x05b, 0x059, 0x057, 0x055, 0x053, 0x052,
        0x050, 0x04e, 0x04d, 0x04b, 0x04a, 0x048, 0x046, 0x045,
        0x043, 0x042, 0x040, 0x03f, 0x03e, 0x03c, 0x03b, 0x039,
        0x038, 0x037, 0x035, 0x034, 0x033, 0x031, 0x030, 0x02f,
        0x02e, 0x02d, 0x02b, 0x02a, 0x029, 0x028, 0x027, 0x026,
        0x025, 0x024, 0x023, 0x022, 0x021, 0x020, 0x01f, 0x01e,
        0x01d, 0x01c, 0x01b, 0x01a, 0x019, 0x018, 0x017, 0x017,
        0x016, 0x015, 0x014, 0x014, 0x013, 0x012, 0x011, 0x011,
        0x010, 0x00f, 0x00f, 0x00e, 0x00d, 0x00d, 0x00c, 0x00c,
        0x00b, 0x00a, 0x00a, 0x009, 0x009, 0x008, 0x008, 0x007,
        0x007, 0x007, 0x006, 0x006, 0x005, 0x005, 0x005, 0x004,
        0x004, 0x004, 0x003, 0x003, 0x003, 0x002, 0x002, 0x002,
        0x002, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001,
        0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000
    };

    /*
        exp table
    */

    private static readonly ushort[] exprom = {
        0x7fa, 0x7f5, 0x7ef, 0x7ea, 0x7e4, 0x7df, 0x7da, 0x7d4,
        0x7cf, 0x7c9, 0x7c4, 0x7bf, 0x7b9, 0x7b4, 0x7ae, 0x7a9,
        0x7a4, 0x79f, 0x799, 0x794, 0x78f, 0x78a, 0x784, 0x77f,
        0x77a, 0x775, 0x770, 0x76a, 0x765, 0x760, 0x75b, 0x756,
        0x751, 0x74c, 0x747, 0x742, 0x73d, 0x738, 0x733, 0x72e,
        0x729, 0x724, 0x71f, 0x71a, 0x715, 0x710, 0x70b, 0x706,
        0x702, 0x6fd, 0x6f8, 0x6f3, 0x6ee, 0x6e9, 0x6e5, 0x6e0,
        0x6db, 0x6d6, 0x6d2, 0x6cd, 0x6c8, 0x6c4, 0x6bf, 0x6ba,
        0x6b5, 0x6b1, 0x6ac, 0x6a8, 0x6a3, 0x69e, 0x69a, 0x695,
        0x691, 0x68c, 0x688, 0x683, 0x67f, 0x67a, 0x676, 0x671,
        0x66d, 0x668, 0x664, 0x65f, 0x65b, 0x657, 0x652, 0x64e,
        0x649, 0x645, 0x641, 0x63c, 0x638, 0x634, 0x630, 0x62b,
        0x627, 0x623, 0x61e, 0x61a, 0x616, 0x612, 0x60e, 0x609,
        0x605, 0x601, 0x5fd, 0x5f9, 0x5f5, 0x5f0, 0x5ec, 0x5e8,
        0x5e4, 0x5e0, 0x5dc, 0x5d8, 0x5d4, 0x5d0, 0x5cc, 0x5c8,
        0x5c4, 0x5c0, 0x5bc, 0x5b8, 0x5b4, 0x5b0, 0x5ac, 0x5a8,
        0x5a4, 0x5a0, 0x59c, 0x599, 0x595, 0x591, 0x58d, 0x589,
        0x585, 0x581, 0x57e, 0x57a, 0x576, 0x572, 0x56f, 0x56b,
        0x567, 0x563, 0x560, 0x55c, 0x558, 0x554, 0x551, 0x54d,
        0x549, 0x546, 0x542, 0x53e, 0x53b, 0x537, 0x534, 0x530,
        0x52c, 0x529, 0x525, 0x522, 0x51e, 0x51b, 0x517, 0x514,
        0x510, 0x50c, 0x509, 0x506, 0x502, 0x4ff, 0x4fb, 0x4f8,
        0x4f4, 0x4f1, 0x4ed, 0x4ea, 0x4e7, 0x4e3, 0x4e0, 0x4dc,
        0x4d9, 0x4d6, 0x4d2, 0x4cf, 0x4cc, 0x4c8, 0x4c5, 0x4c2,
        0x4be, 0x4bb, 0x4b8, 0x4b5, 0x4b1, 0x4ae, 0x4ab, 0x4a8,
        0x4a4, 0x4a1, 0x49e, 0x49b, 0x498, 0x494, 0x491, 0x48e,
        0x48b, 0x488, 0x485, 0x482, 0x47e, 0x47b, 0x478, 0x475,
        0x472, 0x46f, 0x46c, 0x469, 0x466, 0x463, 0x460, 0x45d,
        0x45a, 0x457, 0x454, 0x451, 0x44e, 0x44b, 0x448, 0x445,
        0x442, 0x43f, 0x43c, 0x439, 0x436, 0x433, 0x430, 0x42d,
        0x42a, 0x428, 0x425, 0x422, 0x41f, 0x41c, 0x419, 0x416,
        0x414, 0x411, 0x40e, 0x40b, 0x408, 0x406, 0x403, 0x400
    };

    /*
        freq mult table multiplied by 2

        1/2, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 12, 12, 15, 15
    */

    private static readonly byte[] mt = {
        1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 20, 24, 24, 30, 30
    };

    /*
        ksl table
    */

    private static readonly byte[] kslrom = {
        0, 32, 40, 45, 48, 51, 53, 55, 56, 58, 59, 60, 61, 62, 63, 64
    };

    private static readonly byte[] kslshift = {
        8, 1, 2, 0
    };

    /*
        envelope generator constants
    */

    private static readonly byte[,] eg_incstep = {
        { 0, 0, 0, 0 },
        { 1, 0, 0, 0 },
        { 1, 0, 1, 0 },
        { 1, 1, 1, 0 }
    };

    /*
        address decoding
    */

    private static readonly sbyte[] ad_slot = {
        0, 1, 2, 3, 4, 5, -1, -1, 6, 7, 8, 9, 10, 11, -1, -1,
        12, 13, 14, 15, 16, 17, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1
    };

    private static readonly byte[] ch_slot = {
        0, 1, 2, 6, 7, 8, 12, 13, 14, 18, 19, 20, 24, 25, 26, 30, 31, 32
    };

    /* What the C "int16_t *mod" slot modulation-input pointer points at. */
    private enum ModSource : byte
    {
        Zero = 0,     /* &chip->zeromod */
        Feedback,     /* &mod_slot->fbmod */
        SlotOut       /* &mod_slot->out */
    }

    /* struct _opl3_slot */
    private sealed class Slot
    {
        public Channel channel;
        public short @out;
        public short fbmod;
        public ModSource mod_source;    /* int16_t *mod (with mod_slot) */
        public Slot mod_slot;
        public short prout;
        public ushort eg_rout;
        public ushort eg_out;
        public byte eg_inc;
        public byte eg_gen;
        public byte eg_rate;
        public byte eg_ksl;
        public bool tremoloEnabled;     /* uint8_t *trem */
        public byte reg_vib;
        public byte reg_type;
        public byte reg_ksr;
        public byte reg_mult;
        public byte reg_ksl;
        public byte reg_tl;
        public byte reg_ar;
        public byte reg_dr;
        public byte reg_sl;
        public byte reg_rr;
        public byte reg_wf;
        public byte key;
        public uint pg_reset;
        public uint pg_phase;
        public ushort pg_phase_out;
        public byte slot_num;

        public void Clear()
        {
            channel = null;
            @out = 0;
            fbmod = 0;
            mod_source = ModSource.Zero;
            mod_slot = null;
            prout = 0;
            eg_rout = 0;
            eg_out = 0;
            eg_inc = 0;
            eg_gen = 0;
            eg_rate = 0;
            eg_ksl = 0;
            tremoloEnabled = false;
            reg_vib = 0;
            reg_type = 0;
            reg_ksr = 0;
            reg_mult = 0;
            reg_ksl = 0;
            reg_tl = 0;
            reg_ar = 0;
            reg_dr = 0;
            reg_sl = 0;
            reg_rr = 0;
            reg_wf = 0;
            key = 0;
            pg_reset = 0;
            pg_phase = 0;
            pg_phase_out = 0;
            slot_num = 0;
        }
    }

    /* struct _opl3_channel */
    private sealed class Channel
    {
        public readonly Slot[] slotz = new Slot[2];         /* Don't use "slots" keyword to avoid conflict with Qt applications */
        public Channel pair;
        public readonly Slot[] outSlots = new Slot[4];      /* int16_t *out[4]; null means &chip->zeromod */
        public byte chtype;
        public ushort f_num;
        public byte block;
        public byte fb;
        public byte con;
        public byte alg;
        public byte ksv;
        public ushort cha, chb;
        public ushort chc, chd;
        public byte ch_num;

        public void Clear()
        {
            slotz[0] = null;
            slotz[1] = null;
            pair = null;
            outSlots[0] = null;
            outSlots[1] = null;
            outSlots[2] = null;
            outSlots[3] = null;
            chtype = 0;
            f_num = 0;
            block = 0;
            fb = 0;
            con = 0;
            alg = 0;
            ksv = 0;
            cha = 0;
            chb = 0;
            chc = 0;
            chd = 0;
            ch_num = 0;
        }
    }

    /* struct _opl3_writebuf */
    private struct WriteBuf
    {
        public ulong time;
        public ushort reg;
        public byte data;
    }

    /* struct _opl3_chip (instance state) */
    private readonly Channel[] channel = new Channel[18];
    private readonly Slot[] slot = new Slot[36];
    private ushort timer;
    private ulong eg_timer;
    private byte eg_timerrem;
    private byte eg_state;
    private byte eg_add;
    private byte eg_timer_lo;
    private byte newm;
    private byte nts;
    private byte rhy;
    private byte vibpos;
    private byte vibshift;
    private byte tremolo;
    private byte tremolopos;
    private byte tremoloshift;
    private uint noise;
    /* int16_t zeromod is represented by ModSource.Zero / null outSlots entries. */
    private readonly int[] mixbuff = new int[4];
    private byte rm_hh_bit2;
    private byte rm_hh_bit3;
    private byte rm_hh_bit7;
    private byte rm_hh_bit8;
    private byte rm_tc_bit3;
    private byte rm_tc_bit5;

    /* OPL3L */
    private int rateratio;
    private int samplecnt;
    private readonly short[] oldsamples = new short[4];
    private readonly short[] samples = new short[4];

    private ulong writebuf_samplecnt;
    private uint writebuf_cur;
    private uint writebuf_last;
    private ulong writebuf_lasttime;
    private readonly WriteBuf[] writebuf = new WriteBuf[OPL_WRITEBUF_SIZE];

    /// <summary>
    /// Creates a chip instance. The instance is all-zero (like a freshly
    /// allocated opl3_chip); call <see cref="Reset"/> before generating samples.
    /// </summary>
    public NukedOpl3()
    {
        for (var ii = 0; ii < 36; ii++)
        {
            slot[ii] = new Slot();
        }
        for (var ii = 0; ii < 18; ii++)
        {
            channel[ii] = new Channel();
        }
    }

    /* Reads the slot's modulation input (the C *slot->mod). */
    private static short SlotMod(Slot slot)
    {
        switch (slot.mod_source)
        {
            case ModSource.Feedback:
                return slot.mod_slot.fbmod;
            case ModSource.SlotOut:
                return slot.mod_slot.@out;
            default:
                return 0;
        }
    }

    /* Repoints the slot's modulation input (the C slot->mod = ...). */
    private static void SetMod(Slot slot, ModSource source, Slot sourceSlot)
    {
        slot.mod_source = source;
        slot.mod_slot = sourceSlot;
    }

    /*
        Envelope generator
    */

    private static short EnvelopeCalcExp(uint level)
    {
        if (level > 0x1fff)
        {
            level = 0x1fff;
        }
        return (short)((exprom[level & 0xff] << 1) >> (int)(level >> 8));
    }

    private static short EnvelopeCalcSin0(ushort phase, ushort envelope)
    {
        ushort @out = 0;
        ushort neg = 0;
        phase &= 0x3ff;
        if ((phase & 0x200) != 0)
        {
            neg = 0xffff;
        }
        if ((phase & 0x100) != 0)
        {
            @out = logsinrom[(phase & 0xff) ^ 0xff];
        }
        else
        {
            @out = logsinrom[phase & 0xff];
        }
        return (short)(EnvelopeCalcExp((uint)(@out + (envelope << 3))) ^ neg);
    }

    private static short EnvelopeCalcSin1(ushort phase, ushort envelope)
    {
        ushort @out = 0;
        phase &= 0x3ff;
        if ((phase & 0x200) != 0)
        {
            @out = 0x1000;
        }
        else if ((phase & 0x100) != 0)
        {
            @out = logsinrom[(phase & 0xff) ^ 0xff];
        }
        else
        {
            @out = logsinrom[phase & 0xff];
        }
        return EnvelopeCalcExp((uint)(@out + (envelope << 3)));
    }

    private static short EnvelopeCalcSin2(ushort phase, ushort envelope)
    {
        ushort @out = 0;
        phase &= 0x3ff;
        if ((phase & 0x100) != 0)
        {
            @out = logsinrom[(phase & 0xff) ^ 0xff];
        }
        else
        {
            @out = logsinrom[phase & 0xff];
        }
        return EnvelopeCalcExp((uint)(@out + (envelope << 3)));
    }

    private static short EnvelopeCalcSin3(ushort phase, ushort envelope)
    {
        ushort @out = 0;
        phase &= 0x3ff;
        if ((phase & 0x100) != 0)
        {
            @out = 0x1000;
        }
        else
        {
            @out = logsinrom[phase & 0xff];
        }
        return EnvelopeCalcExp((uint)(@out + (envelope << 3)));
    }

    private static short EnvelopeCalcSin4(ushort phase, ushort envelope)
    {
        ushort @out = 0;
        ushort neg = 0;
        phase &= 0x3ff;
        if ((phase & 0x300) == 0x100)
        {
            neg = 0xffff;
        }
        if ((phase & 0x200) != 0)
        {
            @out = 0x1000;
        }
        else if ((phase & 0x80) != 0)
        {
            @out = logsinrom[((phase ^ 0xff) << 1) & 0xff];
        }
        else
        {
            @out = logsinrom[(phase << 1) & 0xff];
        }
        return (short)(EnvelopeCalcExp((uint)(@out + (envelope << 3))) ^ neg);
    }

    private static short EnvelopeCalcSin5(ushort phase, ushort envelope)
    {
        ushort @out = 0;
        phase &= 0x3ff;
        if ((phase & 0x200) != 0)
        {
            @out = 0x1000;
        }
        else if ((phase & 0x80) != 0)
        {
            @out = logsinrom[((phase ^ 0xff) << 1) & 0xff];
        }
        else
        {
            @out = logsinrom[(phase << 1) & 0xff];
        }
        return EnvelopeCalcExp((uint)(@out + (envelope << 3)));
    }

    private static short EnvelopeCalcSin6(ushort phase, ushort envelope)
    {
        ushort neg = 0;
        phase &= 0x3ff;
        if ((phase & 0x200) != 0)
        {
            neg = 0xffff;
        }
        return (short)(EnvelopeCalcExp((uint)(envelope << 3)) ^ neg);
    }

    private static short EnvelopeCalcSin7(ushort phase, ushort envelope)
    {
        ushort @out = 0;
        ushort neg = 0;
        phase &= 0x3ff;
        if ((phase & 0x200) != 0)
        {
            neg = 0xffff;
            phase = (ushort)((phase & 0x1ff) ^ 0x1ff);
        }
        @out = (ushort)(phase << 3);
        return (short)(EnvelopeCalcExp((uint)(@out + (envelope << 3))) ^ neg);
    }

    /* The C envelope_sin function-pointer table. */
    private static short EnvelopeSin(byte wf, ushort phase, ushort envelope)
    {
        switch (wf)
        {
            case 0:
                return EnvelopeCalcSin0(phase, envelope);
            case 1:
                return EnvelopeCalcSin1(phase, envelope);
            case 2:
                return EnvelopeCalcSin2(phase, envelope);
            case 3:
                return EnvelopeCalcSin3(phase, envelope);
            case 4:
                return EnvelopeCalcSin4(phase, envelope);
            case 5:
                return EnvelopeCalcSin5(phase, envelope);
            case 6:
                return EnvelopeCalcSin6(phase, envelope);
            default:
                return EnvelopeCalcSin7(phase, envelope);
        }
    }

    /* enum envelope_gen_num */
    private const byte envelope_gen_num_attack = 0;
    private const byte envelope_gen_num_decay = 1;
    private const byte envelope_gen_num_sustain = 2;
    private const byte envelope_gen_num_release = 3;

    private static void EnvelopeUpdateKSL(Slot slot)
    {
        short ksl = (short)((kslrom[slot.channel.f_num >> 6] << 2)
                   - ((0x08 - slot.channel.block) << 5));
        if (ksl < 0)
        {
            ksl = 0;
        }
        slot.eg_ksl = (byte)ksl;
    }

    private void EnvelopeCalc(Slot slot)
    {
        byte nonzero;
        byte rate;
        byte rate_hi;
        byte rate_lo;
        byte reg_rate = 0;
        byte ks;
        byte eg_shift, shift;
        ushort eg_rout;
        short eg_inc;
        byte eg_off;
        byte reset = 0;
        slot.eg_out = (ushort)(slot.eg_rout + (slot.reg_tl << 2)
                     + (slot.eg_ksl >> kslshift[slot.reg_ksl])
                     + (slot.tremoloEnabled ? tremolo : (byte)0));
        if (slot.key != 0 && slot.eg_gen == envelope_gen_num_release)
        {
            reset = 1;
            reg_rate = slot.reg_ar;
        }
        else
        {
            switch (slot.eg_gen)
            {
                case envelope_gen_num_attack:
                    reg_rate = slot.reg_ar;
                    break;
                case envelope_gen_num_decay:
                    reg_rate = slot.reg_dr;
                    break;
                case envelope_gen_num_sustain:
                    if (slot.reg_type == 0)
                    {
                        reg_rate = slot.reg_rr;
                    }
                    break;
                case envelope_gen_num_release:
                    reg_rate = slot.reg_rr;
                    break;
            }
        }
        slot.pg_reset = reset;
        ks = (byte)(slot.channel.ksv >> ((slot.reg_ksr ^ 1) << 1));
        nonzero = (byte)(reg_rate != 0 ? 1 : 0);
        rate = (byte)(ks + (reg_rate << 2));
        rate_hi = (byte)(rate >> 2);
        rate_lo = (byte)(rate & 0x03);
        if ((rate_hi & 0x10) != 0)
        {
            rate_hi = 0x0f;
        }
        eg_shift = (byte)(rate_hi + eg_add);
        shift = 0;
        if (nonzero != 0)
        {
            if (rate_hi < 12)
            {
                if (eg_state != 0)
                {
                    switch (eg_shift)
                    {
                        case 12:
                            shift = 1;
                            break;
                        case 13:
                            shift = (byte)((rate_lo >> 1) & 0x01);
                            break;
                        case 14:
                            shift = (byte)(rate_lo & 0x01);
                            break;
                        default:
                            break;
                    }
                }
            }
            else
            {
                shift = (byte)((rate_hi & 0x03) + eg_incstep[rate_lo, eg_timer_lo]);
                if ((shift & 0x04) != 0)
                {
                    shift = 0x03;
                }
                if (shift == 0)
                {
                    shift = eg_state;
                }
            }
        }
        eg_rout = slot.eg_rout;
        eg_inc = 0;
        eg_off = 0;
        /* Instant attack */
        if (reset != 0 && rate_hi == 0x0f)
        {
            eg_rout = 0x00;
        }
        /* Envelope off */
        if ((slot.eg_rout & 0x1f8) == 0x1f8)
        {
            eg_off = 1;
        }
        if (slot.eg_gen != envelope_gen_num_attack && reset == 0 && eg_off != 0)
        {
            eg_rout = 0x1ff;
        }
        switch (slot.eg_gen)
        {
            case envelope_gen_num_attack:
                if (slot.eg_rout == 0)
                {
                    slot.eg_gen = envelope_gen_num_decay;
                }
                else if (slot.key != 0 && shift > 0 && rate_hi != 0x0f)
                {
                    eg_inc = (short)(~slot.eg_rout >> (4 - shift));
                }
                break;
            case envelope_gen_num_decay:
                if ((slot.eg_rout >> 4) == slot.reg_sl)
                {
                    slot.eg_gen = envelope_gen_num_sustain;
                }
                else if (eg_off == 0 && reset == 0 && shift > 0)
                {
                    eg_inc = (short)(1 << (shift - 1));
                }
                break;
            case envelope_gen_num_sustain:
            case envelope_gen_num_release:
                if (eg_off == 0 && reset == 0 && shift > 0)
                {
                    eg_inc = (short)(1 << (shift - 1));
                }
                break;
        }
        slot.eg_rout = (ushort)((eg_rout + eg_inc) & 0x1ff);
        /* Key off */
        if (reset != 0)
        {
            slot.eg_gen = envelope_gen_num_attack;
        }
        if (slot.key == 0)
        {
            slot.eg_gen = envelope_gen_num_release;
        }
    }

    private static void EnvelopeKeyOn(Slot slot, byte type)
    {
        slot.key |= type;
    }

    private static void EnvelopeKeyOff(Slot slot, byte type)
    {
        slot.key = (byte)(slot.key & ~type);
    }

    /*
        Phase Generator
    */

    private void PhaseGenerate(Slot slot)
    {
        ushort f_num;
        uint basefreq;
        byte rm_xor, n_bit;
        uint noise;
        ushort phase;

        f_num = slot.channel.f_num;
        if (slot.reg_vib != 0)
        {
            sbyte range;
            byte vibpos;

            range = (sbyte)((f_num >> 7) & 7);
            vibpos = this.vibpos;

            if ((vibpos & 3) == 0)
            {
                range = 0;
            }
            else if ((vibpos & 1) != 0)
            {
                range >>= 1;
            }
            range >>= vibshift;

            if ((vibpos & 4) != 0)
            {
                range = (sbyte)-range;
            }
            f_num = (ushort)(f_num + range);
        }
        basefreq = (uint)((f_num << slot.channel.block) >> 1);
        phase = (ushort)(slot.pg_phase >> 9);
        if (slot.pg_reset != 0)
        {
            slot.pg_phase = 0;
        }
        slot.pg_phase += (basefreq * mt[slot.reg_mult]) >> 1;
        /* Rhythm mode */
        noise = this.noise;
        slot.pg_phase_out = phase;
        if (slot.slot_num == 13) /* hh */
        {
            rm_hh_bit2 = (byte)((phase >> 2) & 1);
            rm_hh_bit3 = (byte)((phase >> 3) & 1);
            rm_hh_bit7 = (byte)((phase >> 7) & 1);
            rm_hh_bit8 = (byte)((phase >> 8) & 1);
        }
        if (slot.slot_num == 17 && (rhy & 0x20) != 0) /* tc */
        {
            rm_tc_bit3 = (byte)((phase >> 3) & 1);
            rm_tc_bit5 = (byte)((phase >> 5) & 1);
        }
        if ((rhy & 0x20) != 0)
        {
            rm_xor = (byte)((rm_hh_bit2 ^ rm_hh_bit7)
                   | (rm_hh_bit3 ^ rm_tc_bit5)
                   | (rm_tc_bit3 ^ rm_tc_bit5));
            switch (slot.slot_num)
            {
                case 13: /* hh */
                    slot.pg_phase_out = (ushort)(rm_xor << 9);
                    if ((rm_xor ^ (noise & 1)) != 0)
                    {
                        slot.pg_phase_out |= 0xd0;
                    }
                    else
                    {
                        slot.pg_phase_out |= 0x34;
                    }
                    break;
                case 16: /* sd */
                    slot.pg_phase_out = (ushort)(((uint)rm_hh_bit8 << 9)
                                       | ((rm_hh_bit8 ^ (noise & 1)) << 8));
                    break;
                case 17: /* tc */
                    slot.pg_phase_out = (ushort)((rm_xor << 9) | 0x80);
                    break;
                default:
                    break;
            }
        }
        n_bit = (byte)(((noise >> 14) ^ noise) & 0x01);
        this.noise = (noise >> 1) | ((uint)n_bit << 22);
    }

    /*
        Slot
    */

    private static void SlotWrite20(Slot slot, byte data)
    {
        if (((data >> 7) & 0x01) != 0)
        {
            slot.tremoloEnabled = true;     /* slot->trem = &slot->chip->tremolo; */
        }
        else
        {
            slot.tremoloEnabled = false;    /* slot->trem = (uint8_t*)&slot->chip->zeromod; */
        }
        slot.reg_vib = (byte)((data >> 6) & 0x01);
        slot.reg_type = (byte)((data >> 5) & 0x01);
        slot.reg_ksr = (byte)((data >> 4) & 0x01);
        slot.reg_mult = (byte)(data & 0x0f);
    }

    private static void SlotWrite40(Slot slot, byte data)
    {
        slot.reg_ksl = (byte)((data >> 6) & 0x03);
        slot.reg_tl = (byte)(data & 0x3f);
        EnvelopeUpdateKSL(slot);
    }

    private static void SlotWrite60(Slot slot, byte data)
    {
        slot.reg_ar = (byte)((data >> 4) & 0x0f);
        slot.reg_dr = (byte)(data & 0x0f);
    }

    private static void SlotWrite80(Slot slot, byte data)
    {
        slot.reg_sl = (byte)((data >> 4) & 0x0f);
        if (slot.reg_sl == 0x0f)
        {
            slot.reg_sl = 0x1f;
        }
        slot.reg_rr = (byte)(data & 0x0f);
    }

    private void SlotWriteE0(Slot slot, byte data)
    {
        slot.reg_wf = (byte)(data & 0x07);
        if (newm == 0x00)
        {
            slot.reg_wf &= 0x03;
        }
    }

    private static void SlotGenerate(Slot slot)
    {
        slot.@out = EnvelopeSin(slot.reg_wf, (ushort)(slot.pg_phase_out + SlotMod(slot)), slot.eg_out);
    }

    private static void SlotCalcFB(Slot slot)
    {
        if (slot.channel.fb != 0x00)
        {
            slot.fbmod = (short)((slot.prout + slot.@out) >> (0x09 - slot.channel.fb));
        }
        else
        {
            slot.fbmod = 0;
        }
        slot.prout = slot.@out;
    }

    /*
        Channel
    */

    private void ChannelUpdateRhythm(byte data)
    {
        Channel channel6;
        Channel channel7;
        Channel channel8;
        byte chnum;

        rhy = (byte)(data & 0x3f);
        if ((rhy & 0x20) != 0)
        {
            channel6 = channel[6];
            channel7 = channel[7];
            channel8 = channel[8];
            channel6.outSlots[0] = channel6.slotz[1];
            channel6.outSlots[1] = channel6.slotz[1];
            channel6.outSlots[2] = null;
            channel6.outSlots[3] = null;
            channel7.outSlots[0] = channel7.slotz[0];
            channel7.outSlots[1] = channel7.slotz[0];
            channel7.outSlots[2] = channel7.slotz[1];
            channel7.outSlots[3] = channel7.slotz[1];
            channel8.outSlots[0] = channel8.slotz[0];
            channel8.outSlots[1] = channel8.slotz[0];
            channel8.outSlots[2] = channel8.slotz[1];
            channel8.outSlots[3] = channel8.slotz[1];
            for (chnum = 6; chnum < 9; chnum++)
            {
                channel[chnum].chtype = ch_drum;
            }
            ChannelSetupAlg(channel6);
            ChannelSetupAlg(channel7);
            ChannelSetupAlg(channel8);
            /* hh */
            if ((rhy & 0x01) != 0)
            {
                EnvelopeKeyOn(channel7.slotz[0], egk_drum);
            }
            else
            {
                EnvelopeKeyOff(channel7.slotz[0], egk_drum);
            }
            /* tc */
            if ((rhy & 0x02) != 0)
            {
                EnvelopeKeyOn(channel8.slotz[1], egk_drum);
            }
            else
            {
                EnvelopeKeyOff(channel8.slotz[1], egk_drum);
            }
            /* tom */
            if ((rhy & 0x04) != 0)
            {
                EnvelopeKeyOn(channel8.slotz[0], egk_drum);
            }
            else
            {
                EnvelopeKeyOff(channel8.slotz[0], egk_drum);
            }
            /* sd */
            if ((rhy & 0x08) != 0)
            {
                EnvelopeKeyOn(channel7.slotz[1], egk_drum);
            }
            else
            {
                EnvelopeKeyOff(channel7.slotz[1], egk_drum);
            }
            /* bd */
            if ((rhy & 0x10) != 0)
            {
                EnvelopeKeyOn(channel6.slotz[0], egk_drum);
                EnvelopeKeyOn(channel6.slotz[1], egk_drum);
            }
            else
            {
                EnvelopeKeyOff(channel6.slotz[0], egk_drum);
                EnvelopeKeyOff(channel6.slotz[1], egk_drum);
            }
        }
        else
        {
            for (chnum = 6; chnum < 9; chnum++)
            {
                channel[chnum].chtype = ch_2op;
                ChannelSetupAlg(channel[chnum]);
                EnvelopeKeyOff(channel[chnum].slotz[0], egk_drum);
                EnvelopeKeyOff(channel[chnum].slotz[1], egk_drum);
            }
        }
    }

    private void ChannelWriteA0(Channel channel, byte data)
    {
        if (newm != 0 && channel.chtype == ch_4op2)
        {
            return;
        }
        channel.f_num = (ushort)((channel.f_num & 0x300) | data);
        channel.ksv = (byte)((channel.block << 1)
                     | ((channel.f_num >> (0x09 - nts)) & 0x01));
        EnvelopeUpdateKSL(channel.slotz[0]);
        EnvelopeUpdateKSL(channel.slotz[1]);
        if (newm != 0 && channel.chtype == ch_4op)
        {
            channel.pair.f_num = channel.f_num;
            channel.pair.ksv = channel.ksv;
            EnvelopeUpdateKSL(channel.pair.slotz[0]);
            EnvelopeUpdateKSL(channel.pair.slotz[1]);
        }
    }

    private void ChannelWriteB0(Channel channel, byte data)
    {
        if (newm != 0 && channel.chtype == ch_4op2)
        {
            return;
        }
        channel.f_num = (ushort)((channel.f_num & 0xff) | ((data & 0x03) << 8));
        channel.block = (byte)((data >> 2) & 0x07);
        channel.ksv = (byte)((channel.block << 1)
                     | ((channel.f_num >> (0x09 - nts)) & 0x01));
        EnvelopeUpdateKSL(channel.slotz[0]);
        EnvelopeUpdateKSL(channel.slotz[1]);
        if (newm != 0 && channel.chtype == ch_4op)
        {
            channel.pair.f_num = channel.f_num;
            channel.pair.block = channel.block;
            channel.pair.ksv = channel.ksv;
            EnvelopeUpdateKSL(channel.pair.slotz[0]);
            EnvelopeUpdateKSL(channel.pair.slotz[1]);
        }
    }

    private static void ChannelSetupAlg(Channel channel)
    {
        if (channel.chtype == ch_drum)
        {
            if (channel.ch_num == 7 || channel.ch_num == 8)
            {
                SetMod(channel.slotz[0], ModSource.Zero, null);
                SetMod(channel.slotz[1], ModSource.Zero, null);
                return;
            }
            switch (channel.alg & 0x01)
            {
                case 0x00:
                    SetMod(channel.slotz[0], ModSource.Feedback, channel.slotz[0]);
                    SetMod(channel.slotz[1], ModSource.SlotOut, channel.slotz[0]);
                    break;
                case 0x01:
                    SetMod(channel.slotz[0], ModSource.Feedback, channel.slotz[0]);
                    SetMod(channel.slotz[1], ModSource.Zero, null);
                    break;
            }
            return;
        }
        if ((channel.alg & 0x08) != 0)
        {
            return;
        }
        if ((channel.alg & 0x04) != 0)
        {
            channel.pair.outSlots[0] = null;
            channel.pair.outSlots[1] = null;
            channel.pair.outSlots[2] = null;
            channel.pair.outSlots[3] = null;
            switch (channel.alg & 0x03)
            {
                case 0x00:
                    SetMod(channel.pair.slotz[0], ModSource.Feedback, channel.pair.slotz[0]);
                    SetMod(channel.pair.slotz[1], ModSource.SlotOut, channel.pair.slotz[0]);
                    SetMod(channel.slotz[0], ModSource.SlotOut, channel.pair.slotz[1]);
                    SetMod(channel.slotz[1], ModSource.SlotOut, channel.slotz[0]);
                    channel.outSlots[0] = channel.slotz[1];
                    channel.outSlots[1] = null;
                    channel.outSlots[2] = null;
                    channel.outSlots[3] = null;
                    break;
                case 0x01:
                    SetMod(channel.pair.slotz[0], ModSource.Feedback, channel.pair.slotz[0]);
                    SetMod(channel.pair.slotz[1], ModSource.SlotOut, channel.pair.slotz[0]);
                    SetMod(channel.slotz[0], ModSource.Zero, null);
                    SetMod(channel.slotz[1], ModSource.SlotOut, channel.slotz[0]);
                    channel.outSlots[0] = channel.pair.slotz[1];
                    channel.outSlots[1] = channel.slotz[1];
                    channel.outSlots[2] = null;
                    channel.outSlots[3] = null;
                    break;
                case 0x02:
                    SetMod(channel.pair.slotz[0], ModSource.Feedback, channel.pair.slotz[0]);
                    SetMod(channel.pair.slotz[1], ModSource.Zero, null);
                    SetMod(channel.slotz[0], ModSource.SlotOut, channel.pair.slotz[1]);
                    SetMod(channel.slotz[1], ModSource.SlotOut, channel.slotz[0]);
                    channel.outSlots[0] = channel.pair.slotz[0];
                    channel.outSlots[1] = channel.slotz[1];
                    channel.outSlots[2] = null;
                    channel.outSlots[3] = null;
                    break;
                case 0x03:
                    SetMod(channel.pair.slotz[0], ModSource.Feedback, channel.pair.slotz[0]);
                    SetMod(channel.pair.slotz[1], ModSource.Zero, null);
                    SetMod(channel.slotz[0], ModSource.SlotOut, channel.pair.slotz[1]);
                    SetMod(channel.slotz[1], ModSource.Zero, null);
                    channel.outSlots[0] = channel.pair.slotz[0];
                    channel.outSlots[1] = channel.slotz[0];
                    channel.outSlots[2] = channel.slotz[1];
                    channel.outSlots[3] = null;
                    break;
            }
        }
        else
        {
            switch (channel.alg & 0x01)
            {
                case 0x00:
                    SetMod(channel.slotz[0], ModSource.Feedback, channel.slotz[0]);
                    SetMod(channel.slotz[1], ModSource.SlotOut, channel.slotz[0]);
                    channel.outSlots[0] = channel.slotz[1];
                    channel.outSlots[1] = null;
                    channel.outSlots[2] = null;
                    channel.outSlots[3] = null;
                    break;
                case 0x01:
                    SetMod(channel.slotz[0], ModSource.Feedback, channel.slotz[0]);
                    SetMod(channel.slotz[1], ModSource.Zero, null);
                    channel.outSlots[0] = channel.slotz[0];
                    channel.outSlots[1] = channel.slotz[1];
                    channel.outSlots[2] = null;
                    channel.outSlots[3] = null;
                    break;
            }
        }
    }

    private void ChannelUpdateAlg(Channel channel)
    {
        channel.alg = channel.con;
        if (newm != 0)
        {
            if (channel.chtype == ch_4op)
            {
                channel.pair.alg = (byte)(0x04 | (channel.con << 1) | (channel.pair.con));
                channel.alg = 0x08;
                ChannelSetupAlg(channel.pair);
            }
            else if (channel.chtype == ch_4op2)
            {
                channel.alg = (byte)(0x04 | (channel.pair.con << 1) | (channel.con));
                channel.pair.alg = 0x08;
                ChannelSetupAlg(channel);
            }
            else
            {
                ChannelSetupAlg(channel);
            }
        }
        else
        {
            ChannelSetupAlg(channel);
        }
    }

    private void ChannelWriteC0(Channel channel, byte data)
    {
        channel.fb = (byte)((data & 0x0e) >> 1);
        channel.con = (byte)(data & 0x01);
        ChannelUpdateAlg(channel);
        if (newm != 0)
        {
            channel.cha = (ushort)(((data >> 4) & 0x01) != 0 ? 0xffff : 0);
            channel.chb = (ushort)(((data >> 5) & 0x01) != 0 ? 0xffff : 0);
            channel.chc = (ushort)(((data >> 6) & 0x01) != 0 ? 0xffff : 0);
            channel.chd = (ushort)(((data >> 7) & 0x01) != 0 ? 0xffff : 0);
        }
        else
        {
            channel.cha = channel.chb = unchecked((ushort)~0);
            // TODO: Verify on real chip if DAC2 output is disabled in compat mode
            channel.chc = channel.chd = 0;
        }
    }

    private void ChannelKeyOn(Channel channel)
    {
        if (newm != 0)
        {
            if (channel.chtype == ch_4op)
            {
                EnvelopeKeyOn(channel.slotz[0], egk_norm);
                EnvelopeKeyOn(channel.slotz[1], egk_norm);
                EnvelopeKeyOn(channel.pair.slotz[0], egk_norm);
                EnvelopeKeyOn(channel.pair.slotz[1], egk_norm);
            }
            else if (channel.chtype == ch_2op || channel.chtype == ch_drum)
            {
                EnvelopeKeyOn(channel.slotz[0], egk_norm);
                EnvelopeKeyOn(channel.slotz[1], egk_norm);
            }
        }
        else
        {
            EnvelopeKeyOn(channel.slotz[0], egk_norm);
            EnvelopeKeyOn(channel.slotz[1], egk_norm);
        }
    }

    private void ChannelKeyOff(Channel channel)
    {
        if (newm != 0)
        {
            if (channel.chtype == ch_4op)
            {
                EnvelopeKeyOff(channel.slotz[0], egk_norm);
                EnvelopeKeyOff(channel.slotz[1], egk_norm);
                EnvelopeKeyOff(channel.pair.slotz[0], egk_norm);
                EnvelopeKeyOff(channel.pair.slotz[1], egk_norm);
            }
            else if (channel.chtype == ch_2op || channel.chtype == ch_drum)
            {
                EnvelopeKeyOff(channel.slotz[0], egk_norm);
                EnvelopeKeyOff(channel.slotz[1], egk_norm);
            }
        }
        else
        {
            EnvelopeKeyOff(channel.slotz[0], egk_norm);
            EnvelopeKeyOff(channel.slotz[1], egk_norm);
        }
    }

    private void ChannelSet4Op(byte data)
    {
        byte bit;
        byte chnum;
        for (bit = 0; bit < 6; bit++)
        {
            chnum = bit;
            if (bit >= 3)
            {
                chnum += 9 - 3;
            }
            if (((data >> bit) & 0x01) != 0)
            {
                channel[chnum].chtype = ch_4op;
                channel[chnum + 3].chtype = ch_4op2;
                ChannelUpdateAlg(channel[chnum]);
            }
            else
            {
                channel[chnum].chtype = ch_2op;
                channel[chnum + 3].chtype = ch_2op;
                ChannelUpdateAlg(channel[chnum]);
                ChannelUpdateAlg(channel[chnum + 3]);
            }
        }
    }

    private static short ClipSample(int sample)
    {
        if (sample > 32767)
        {
            sample = 32767;
        }
        else if (sample < -32768)
        {
            sample = -32768;
        }
        return (short)sample;
    }

    private void ProcessSlot(Slot slot)
    {
        SlotCalcFB(slot);
        EnvelopeCalc(slot);
        PhaseGenerate(slot);
        SlotGenerate(slot);
    }

    /* Reads a channel mix input (the C *channel->out[i]); null means &chip->zeromod. */
    private static short SlotOut(Slot slot)
    {
        return slot != null ? slot.@out : (short)0;
    }

    private void Generate4Ch(Span<short> buf4)
    {
        Channel channel;
        Slot[] outSlots;
        Span<int> mix = stackalloc int[2];
        byte ii;
        short accm;
        byte shift = 0;

        buf4[1] = ClipSample(mixbuff[1]);
        buf4[3] = ClipSample(mixbuff[3]);

        /* OPL_QUIRK_CHANNELSAMPLEDELAY: Some FM channels are output one
           sample later on the left side than the right. */
        for (ii = 0; ii < 15; ii++)
        {
            ProcessSlot(slot[ii]);
        }

        mix[0] = mix[1] = 0;
        for (ii = 0; ii < 18; ii++)
        {
            channel = this.channel[ii];
            outSlots = channel.outSlots;
            accm = (short)(SlotOut(outSlots[0]) + SlotOut(outSlots[1])
                 + SlotOut(outSlots[2]) + SlotOut(outSlots[3]));
            mix[0] += (short)(accm & channel.cha);
            mix[1] += (short)(accm & channel.chc);
        }
        mixbuff[0] = mix[0];
        mixbuff[2] = mix[1];

        for (ii = 15; ii < 18; ii++)
        {
            ProcessSlot(slot[ii]);
        }

        buf4[0] = ClipSample(mixbuff[0]);
        buf4[2] = ClipSample(mixbuff[2]);

        for (ii = 18; ii < 33; ii++)
        {
            ProcessSlot(slot[ii]);
        }

        mix[0] = mix[1] = 0;
        for (ii = 0; ii < 18; ii++)
        {
            channel = this.channel[ii];
            outSlots = channel.outSlots;
            accm = (short)(SlotOut(outSlots[0]) + SlotOut(outSlots[1])
                 + SlotOut(outSlots[2]) + SlotOut(outSlots[3]));
            mix[0] += (short)(accm & channel.chb);
            mix[1] += (short)(accm & channel.chd);
        }
        mixbuff[1] = mix[0];
        mixbuff[3] = mix[1];

        for (ii = 33; ii < 36; ii++)
        {
            ProcessSlot(slot[ii]);
        }

        if ((timer & 0x3f) == 0x3f)
        {
            tremolopos = (byte)((tremolopos + 1) % 210);
        }
        if (tremolopos < 105)
        {
            tremolo = (byte)(tremolopos >> tremoloshift);
        }
        else
        {
            tremolo = (byte)((210 - tremolopos) >> tremoloshift);
        }

        if ((timer & 0x3ff) == 0x3ff)
        {
            vibpos = (byte)((vibpos + 1) & 7);
        }

        timer++;

        if (eg_state != 0)
        {
            while (shift < 13 && ((eg_timer >> shift) & 1) == 0)
            {
                shift++;
            }
            if (shift > 12)
            {
                eg_add = 0;
            }
            else
            {
                eg_add = (byte)(shift + 1);
            }
            eg_timer_lo = (byte)(eg_timer & 0x3u);
        }

        if (eg_timerrem != 0 || eg_state != 0)
        {
            if (eg_timer == 0xfffffffffUL)
            {
                eg_timer = 0;
                eg_timerrem = 1;
            }
            else
            {
                eg_timer++;
                eg_timerrem = 0;
            }
        }

        eg_state ^= 1;

        while (true)
        {
            ref var wb = ref writebuf[writebuf_cur];
            if (wb.time > writebuf_samplecnt)
            {
                break;
            }
            if ((wb.reg & 0x200) == 0)
            {
                break;
            }
            wb.reg &= 0x1ff;
            WriteReg(wb.reg, wb.data);
            writebuf_cur = (writebuf_cur + 1) % OPL_WRITEBUF_SIZE;
        }
        writebuf_samplecnt++;
    }

    private void Generate(short[] buf, int offset)
    {
        Span<short> samples = stackalloc short[4];
        Generate4Ch(samples);
        buf[offset] = samples[0];
        buf[offset + 1] = samples[1];
    }

    private void Generate4ChResampled(Span<short> buf4)
    {
        while (samplecnt >= rateratio)
        {
            oldsamples[0] = samples[0];
            oldsamples[1] = samples[1];
            oldsamples[2] = samples[2];
            oldsamples[3] = samples[3];
            Generate4Ch(samples);
            samplecnt -= rateratio;
        }
        buf4[0] = (short)((oldsamples[0] * (rateratio - samplecnt)
                          + samples[0] * samplecnt) / rateratio);
        buf4[1] = (short)((oldsamples[1] * (rateratio - samplecnt)
                          + samples[1] * samplecnt) / rateratio);
        buf4[2] = (short)((oldsamples[2] * (rateratio - samplecnt)
                          + samples[2] * samplecnt) / rateratio);
        buf4[3] = (short)((oldsamples[3] * (rateratio - samplecnt)
                          + samples[3] * samplecnt) / rateratio);
        samplecnt += 1 << RSM_FRAC;
    }

    /// <summary>
    /// Generates one resampled stereo sample pair (OPL3_GenerateResampled),
    /// writing left to buf[offset] and right to buf[offset + 1].
    /// </summary>
    public void GenerateResampled(short[] buf, int offset)
    {
        Span<short> samples = stackalloc short[4];
        Generate4ChResampled(samples);
        buf[offset] = samples[0];
        buf[offset + 1] = samples[1];
    }

    /// <summary>
    /// Resets the chip to its power-on state and sets the output sample
    /// rate (OPL3_Reset).
    /// </summary>
    public void Reset(uint sampleRate)
    {
        Slot slot;
        Channel channel;
        byte slotnum;
        byte channum;
        byte local_ch_slot;

        /* memset(chip, 0, sizeof(opl3_chip)); */
        for (slotnum = 0; slotnum < 36; slotnum++)
        {
            this.slot[slotnum].Clear();
        }
        for (channum = 0; channum < 18; channum++)
        {
            this.channel[channum].Clear();
        }
        timer = 0;
        eg_timer = 0;
        eg_timerrem = 0;
        eg_state = 0;
        eg_add = 0;
        eg_timer_lo = 0;
        newm = 0;
        nts = 0;
        rhy = 0;
        vibpos = 0;
        vibshift = 0;
        tremolo = 0;
        tremolopos = 0;
        tremoloshift = 0;
        noise = 0;
        Array.Clear(mixbuff, 0, mixbuff.Length);
        rm_hh_bit2 = 0;
        rm_hh_bit3 = 0;
        rm_hh_bit7 = 0;
        rm_hh_bit8 = 0;
        rm_tc_bit3 = 0;
        rm_tc_bit5 = 0;
        rateratio = 0;
        samplecnt = 0;
        Array.Clear(oldsamples, 0, oldsamples.Length);
        Array.Clear(samples, 0, samples.Length);
        writebuf_samplecnt = 0;
        writebuf_cur = 0;
        writebuf_last = 0;
        writebuf_lasttime = 0;
        Array.Clear(writebuf, 0, writebuf.Length);

        for (slotnum = 0; slotnum < 36; slotnum++)
        {
            slot = this.slot[slotnum];
            SetMod(slot, ModSource.Zero, null);     /* slot->mod = &chip->zeromod; */
            slot.eg_rout = 0x1ff;
            slot.eg_out = 0x1ff;
            slot.eg_gen = envelope_gen_num_release;
            slot.tremoloEnabled = false;            /* slot->trem = (uint8_t*)&chip->zeromod; */
            slot.slot_num = slotnum;
        }
        for (channum = 0; channum < 18; channum++)
        {
            channel = this.channel[channum];
            local_ch_slot = ch_slot[channum];
            channel.slotz[0] = this.slot[local_ch_slot];
            channel.slotz[1] = this.slot[local_ch_slot + 3];
            this.slot[local_ch_slot].channel = channel;
            this.slot[local_ch_slot + 3].channel = channel;
            if ((channum % 9) < 3)
            {
                channel.pair = this.channel[channum + 3];
            }
            else if ((channum % 9) < 6)
            {
                channel.pair = this.channel[channum - 3];
            }
            channel.outSlots[0] = null;             /* channel->out[0..3] = &chip->zeromod; */
            channel.outSlots[1] = null;
            channel.outSlots[2] = null;
            channel.outSlots[3] = null;
            channel.chtype = ch_2op;
            channel.cha = 0xffff;
            channel.chb = 0xffff;
            channel.ch_num = channum;
            ChannelSetupAlg(channel);
        }
        noise = 1;
        rateratio = (int)((sampleRate << RSM_FRAC) / 49716);
        tremoloshift = 4;
        vibshift = 1;
    }

    /// <summary>
    /// Writes a chip register immediately (OPL3_WriteReg).
    /// </summary>
    public void WriteReg(ushort reg, byte v)
    {
        byte high = (byte)((reg >> 8) & 0x01);
        byte regm = (byte)(reg & 0xff);
        switch (regm & 0xf0)
        {
            case 0x00:
                if (high != 0)
                {
                    switch (regm & 0x0f)
                    {
                        case 0x04:
                            ChannelSet4Op(v);
                            break;
                        case 0x05:
                            newm = (byte)(v & 0x01);
                            break;
                    }
                }
                else
                {
                    switch (regm & 0x0f)
                    {
                        case 0x08:
                            nts = (byte)((v >> 6) & 0x01);
                            break;
                    }
                }
                break;
            case 0x20:
            case 0x30:
                if (ad_slot[regm & 0x1f] >= 0)
                {
                    SlotWrite20(slot[18 * high + ad_slot[regm & 0x1f]], v);
                }
                break;
            case 0x40:
            case 0x50:
                if (ad_slot[regm & 0x1f] >= 0)
                {
                    SlotWrite40(slot[18 * high + ad_slot[regm & 0x1f]], v);
                }
                break;
            case 0x60:
            case 0x70:
                if (ad_slot[regm & 0x1f] >= 0)
                {
                    SlotWrite60(slot[18 * high + ad_slot[regm & 0x1f]], v);
                }
                break;
            case 0x80:
            case 0x90:
                if (ad_slot[regm & 0x1f] >= 0)
                {
                    SlotWrite80(slot[18 * high + ad_slot[regm & 0x1f]], v);
                }
                break;
            case 0xe0:
            case 0xf0:
                if (ad_slot[regm & 0x1f] >= 0)
                {
                    SlotWriteE0(slot[18 * high + ad_slot[regm & 0x1f]], v);
                }
                break;
            case 0xa0:
                if ((regm & 0x0f) < 9)
                {
                    ChannelWriteA0(channel[9 * high + (regm & 0x0f)], v);
                }
                break;
            case 0xb0:
                if (regm == 0xbd && high == 0)
                {
                    tremoloshift = (byte)((((v >> 7) ^ 1) << 1) + 2);
                    vibshift = (byte)(((v >> 6) & 0x01) ^ 1);
                    ChannelUpdateRhythm(v);
                }
                else if ((regm & 0x0f) < 9)
                {
                    ChannelWriteB0(channel[9 * high + (regm & 0x0f)], v);
                    if ((v & 0x20) != 0)
                    {
                        ChannelKeyOn(channel[9 * high + (regm & 0x0f)]);
                    }
                    else
                    {
                        ChannelKeyOff(channel[9 * high + (regm & 0x0f)]);
                    }
                }
                break;
            case 0xc0:
                if ((regm & 0x0f) < 9)
                {
                    ChannelWriteC0(channel[9 * high + (regm & 0x0f)], v);
                }
                break;
        }
    }

    /// <summary>
    /// Queues a register write to be applied after a small delay measured
    /// in chip samples (OPL3_WriteRegBuffered).
    /// </summary>
    public void WriteRegBuffered(ushort reg, byte v)
    {
        ulong time1, time2;
        uint writebuf_last;

        writebuf_last = this.writebuf_last;
        ref var wb = ref writebuf[writebuf_last];

        if ((wb.reg & 0x200) != 0)
        {
            WriteReg((ushort)(wb.reg & 0x1ff), wb.data);

            writebuf_cur = (writebuf_last + 1) % OPL_WRITEBUF_SIZE;
            writebuf_samplecnt = wb.time;
        }

        wb.reg = (ushort)(reg | 0x200);
        wb.data = v;
        time1 = writebuf_lasttime + OPL_WRITEBUF_DELAY;
        time2 = writebuf_samplecnt;

        if (time1 < time2)
        {
            time1 = time2;
        }

        wb.time = time1;
        writebuf_lasttime = time1;
        this.writebuf_last = (writebuf_last + 1) % OPL_WRITEBUF_SIZE;
    }

    /// <summary>
    /// Generates numSamples resampled stereo sample pairs
    /// (OPL3_GenerateStream), written interleaved (left, right) starting
    /// at buffer[offset].
    /// </summary>
    public void GenerateStream(short[] buffer, int offset, uint numSamples)
    {
        uint i;

        for (i = 0; i < numSamples; i++)
        {
            GenerateResampled(buffer, offset);
            offset += 2;
        }
    }
}
