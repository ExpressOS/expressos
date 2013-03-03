using System;

namespace ExpressOS.Kernel.Arch
{
    public struct L4Handle
    {
        public uint _value;
        // From L4.Fisaco.OC
        public const int L4_CAP_SHIFT = 12;
        public const uint L4_INVALID_CAP = ~0U << (L4_CAP_SHIFT - 1);
        public const uint L4_INVALID_CAP_BIT = 1 << (L4_CAP_SHIFT - 1);

        public static L4Handle Invalid
        {
            get
            {
                return new L4Handle(L4_INVALID_CAP);
            }
        }

        public bool isInvalid
        {
            get
            {
                return (_value & L4_INVALID_CAP_BIT) != 0;
            }
        }

        public L4Handle(IntPtr value)
        {
            this._value = (uint)(value.ToInt64());
        }

        public L4Handle(uint value)
        {
            this._value = value;
        }

        public static bool operator==(L4Handle lhs, L4Handle rhs)
        {
            return lhs._value == rhs._value;
        }

        public static bool operator!=(L4Handle lhs, L4Handle rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return (int)_value;
        }

        public override bool Equals(object obj)
        {
            return false;
        }
    }

    public struct ExceptionRegisters
    {
        public int gs;      /**< gs register */
        public int fs;      /**< fs register */

        public int edi;     /**< edi register */
        public int esi;     /**< esi register */
        public int ebp;     /**< ebp register */
        public int pfa;     /**< page fault address */
        public int ebx;     /**< ebx register */
        public int edx;     /**< edx register */
        public int ecx;     /**< ecx register */
        public int eax;     /**< eax register */

        public int trapno;  /**< trap number */
        public int err;     /**< error code */

        public uint ip;     /**< instruction pointer */
        public int dummy1;  /**< dummy \internal */
        public int flags;   /**< eflags */
        public int sp;      /**< stack pointer */

        public const uint L4_UTCB_EXCEPTION_REGS_SIZE = 16;
    }

    public struct Msgtag
    {
        public uint raw;

        public const uint L4_PROTO_NONE = 0;
        public const uint L4_PROTO_ALLOW_SYSCALL = 1;   ///< Allow an alien the system call
        public const uint L4_PROTO_PF_EXCEPTION = 1;   ///< Make an exception out of a page fault
        public const uint L4_PROTO_IRQ = 0xffff; ///< IRQ message
        public const uint L4_PROTO_PAGE_FAULT = 0xfffe; ///< Page fault message
        public const uint L4_PROTO_PREEMPTION = 0xfffd; ///< Preemption message
        public const uint L4_PROTO_SYS_EXCEPTION = 0xfffc; ///< System exception
        public const uint L4_PROTO_EXCEPTION = 0xfffb; ///< Exception
        public const uint L4_PROTO_SIGMA0 = 0xfffa; ///< Sigma0 protocol
        public const uint L4_PROTO_IO_PAGE_FAULT = 0xfff8; ///< I/O page fault message

        public const uint L4_MSGTAG_ERROR = 0x8000;

        public const uint L4_ITEM_MAP = 8;
        public Msgtag(uint label, uint words, uint items, uint flags)
        {
            this.raw = (uint)((label << 16) | (words & 0x3f) | ((items & 0x3f) << 6) | (flags & 0xf000));
        }

        public unsafe int ErrorCode()
        {
            if (!HasError)
                return 0;
            return (int)(NativeMethods.l4api_utcb_tcr()->error & ThreadRegister.L4_IPC_ERROR_MASK);
        }

        public int Label
        {
            get
            {
                return (int)(raw >> 16);
            }
            set
            {
                raw = (uint)((raw & 0xffff) | ((uint)value << 16));
            }
        }

        public int Words
        {
            get
            {
                return (int)(raw & 0x3f);
            }
        }

        public int Items
        {
            get
            {
                return (int)((raw >> 6) & 0x3f);
            }
        }

        public bool HasError
        {
            get
            {
                return (raw & L4_MSGTAG_ERROR) == L4_MSGTAG_ERROR;
            }
        }
    }

    public struct MessageRegisters
    {
        public const int L4_UTCB_GENERIC_BUFFERS_SIZE = 63;
        public int mr0;
        public int mr1;
        public int mr2;
        public int mr3;
        public int mr4;
        public int mr5;
        public int mr6;
        public int mr7;
        public int mr8;
        public int mr9;
        public int mr10;
        public int mr11;
        public int mr12;
        public int mr13;
        public int mr14;
        public int mr15;
        public int mr16;
        public int mr17;
        public int mr18;
        public int mr19;
        public int mr20;
        public int mr21;
        public int mr22;
        public int mr23;
        public int mr24;
        public int mr25;
        public int mr26;
        public int mr27;
        public int mr28;
        public int mr29;
        public int mr30;
        public int mr31;
        public int mr32;
        public int mr33;
        public int mr34;
        public int mr35;
        public int mr36;
        public int mr37;
        public int mr38;
        public int mr39;
        public int mr40;
        public int mr41;
        public int mr42;
        public int mr43;
        public int mr44;
        public int mr45;
        public int mr46;
        public int mr47;
        public int mr48;
        public int mr49;
        public int mr50;
        public int mr51;
        public int mr52;
        public int mr53;
        public int mr54;
        public int mr55;
        public int mr56;
        public int mr57;
        public int mr58;
        public int mr59;
        public int mr60;
        public int mr61;
        public int mr62;
    }

    public struct ThreadRegister
    {
        public const int L4_IPC_ERROR_MASK = 0x1f;
        public const int L4_IPC_RETIMEOUT  = 0x03;
        public const int L4_IPC_SETIMEOUT  = 0x02;

        /// System call error codes
        public uint error;
        /// Message transfer timeout
        public Timeout xfer;
        /// User values (ignored and preserved by the kernel)
        public int user0;
        public int user1;
        public int user2;
    }

    public struct L4FPage
    {
        public const uint L4_FPAGE_RWX = 7;
        public const uint L4_FPAGE_FAULT_NONE = 0;
        public const uint L4_FPAGE_FAULT_READ = 1;
        public const uint L4_FPAGE_FAULT_WRITE = 1 << 1;
        public const uint L4_FPAGE_FAULT_EXEC = 1 << 2;

        public const int L4_MWORD_BITS = 32;
        public const int L4_FPAGE_RIGHTS_SHIFT = 0;  ///< Access permissions shift
        public const int L4_FPAGE_TYPE_SHIFT = 4;  ///< Flexpage type shift (memory, IO port, obj...)
        public const int L4_FPAGE_SIZE_SHIFT = 6;  ///< Flexpage size shift (log2-based)
        public const int L4_FPAGE_ADDR_SHIFT = 12; ///< Page address shift

        public const int L4_FPAGE_RIGHTS_BITS = 4;   ///< Access permissions size
        public const int L4_FPAGE_TYPE_BITS = 2;   ///< Flexpage type size (memory, IO port, obj...)
        public const int L4_FPAGE_SIZE_BITS = 6;   ///< Flexpage size size (log2-based)
        public const int L4_FPAGE_ADDR_BITS = L4_MWORD_BITS - L4_FPAGE_ADDR_SHIFT;  ///< Page address size

        public const uint L4_FPAGE_RIGHTS_MASK = ((1U << L4_FPAGE_RIGHTS_BITS) - 1) << L4_FPAGE_RIGHTS_SHIFT;
        public const uint L4_FPAGE_TYPE_MASK = ((1U << L4_FPAGE_TYPE_BITS) - 1) << L4_FPAGE_TYPE_SHIFT;
        public const uint L4_FPAGE_SIZE_MASK = ((1U << L4_FPAGE_SIZE_BITS) - 1) << L4_FPAGE_SIZE_SHIFT;
        public const uint L4_FPAGE_ADDR_MASK = ((1U << L4_FPAGE_ADDR_BITS) - 1) << L4_FPAGE_ADDR_SHIFT;

        enum Type
        {
            L4_FPAGE_SPECIAL = 0,
            L4_FPAGE_MEMORY = 1,
            L4_FPAGE_IO = 2,
            L4_FPAGE_OBJ = 3,
        };

        public uint raw;
        public L4FPage(uint addr, int order, int rights)
        {
            this.raw = l4_fpage_generic(addr, Type.L4_FPAGE_MEMORY, order, rights);
        }

        private static uint l4_fpage_generic(uint address, Type type, int size, int rights)
        {
            var raw = ((rights << L4_FPAGE_RIGHTS_SHIFT) & L4_FPAGE_RIGHTS_MASK)
                    | (((uint)type << L4_FPAGE_TYPE_SHIFT) & L4_FPAGE_TYPE_MASK)
                    | ((size << L4_FPAGE_SIZE_SHIFT) & L4_FPAGE_SIZE_MASK)
                    | ((address) & L4_FPAGE_ADDR_MASK);
            return (uint)raw;
        }

    }

    // Basically a floating point number with 10 bits mantissa and
    // 5 bits exponent (t = m*2^e).
    //
    // The timeout can also specify an absolute point in time (bit 16 == 1).
    public struct Timeout
    {
        public const int MANTISSA_BITS = 10;
        public const int EXPONENTS_BITS = 5;
        public const int EXPONENTS_BITMASK = 0x7c00;
        public uint raw;

        public Timeout(uint snd_time, uint rcv_time)
        {
            var rcv = UsecToTimeOut(rcv_time);
            var snd = UsecToTimeOut(snd_time);
            this.raw = (uint)((snd << 16) | rcv);
        }

        Timeout(ushort snd, ushort rcv)
        {
            this.raw = (uint)((snd << 16) | rcv);
        }


        private static ushort UsecToTimeOut(uint time)
        {
            var r = Util.msb(time);
            if (r < MANTISSA_BITS)
            {
                return (ushort)time;
            }
            else
            {
                var exp = r - MANTISSA_BITS;
                var man = time >> exp;
                return (ushort)(man | (ushort)(((exp << MANTISSA_BITS) & EXPONENTS_BITMASK)));
            }
        }

        public static Timeout Never
        {
            get
            {
                return new Timeout(0, 0);
            }
        }

        public static Timeout SendZero
        {
            get
            {
                return new Timeout(0x400, 0);
            }
        }

        public static Timeout RecvZero
        {
            get
            {
                return new Timeout(0, 0x400);
            }
        }

        public static bool operator ==(Timeout lhs, Timeout rhs)
        {
            return lhs.raw == rhs.raw;
        }

        public static bool operator !=(Timeout lhs, Timeout rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return (int)raw;
        }

        public override bool Equals(object obj)
        {
            return false;
        }
    }

    public struct ThreadInfo
    {
        public L4Handle thread;
        public L4Handle gate;
    }
}
