using System;

namespace ExpressOS.Kernel
{
    public struct Pointer
    {
        uint _value;
        public const int Size = 4;

        public Pointer(IntPtr v)
        {
            _value = (uint)(v.ToInt32());
        }

        public Pointer(uint value)
        {
            this._value = value;
        }

        public unsafe Pointer(void * p)
        {
            this._value = (uint)p;
        }

        public Pointer Add(int rhs)
        {
            return this + rhs;
        }

        public int Diff(Pointer rhs)
        {
            return (int)(_value - rhs._value);
        }

        public IntPtr ToIntPtr()
        {
            return new IntPtr(_value);
        }

        public static Pointer Zero
        {
            get
            {
                return new Pointer(IntPtr.Zero);
            }
        }

        public override string ToString()
        {
            string res = "0x" + ToHex((int)(_value >> 24))
                    + ToHex((int)((_value & 0xff0000) >> 16))
                    + ToHex((int)((_value & 0xff00) >> 8))
                    + ToHex((int)(_value & 0xff));
            return res;
        }

        public override bool Equals(object obj)
        {
            //if (obj is Pointer)
            //{
            //    return this == (Pointer)obj;
            //}
            //else
            //{
            //    return false;
            //}
            return false;
        }

        public static bool operator ==(Pointer lhs, Pointer rhs)
        {
            return lhs._value == rhs._value;
        }

        public static bool operator !=(Pointer lhs, Pointer rhs)
        {
            return !(lhs == rhs);
        }

        public static Pointer operator +(Pointer lhs, int rhs)
        {
            return new Pointer((uint)(lhs._value + rhs));
        }

        public static Pointer operator +(Pointer lhs, uint rhs)
        {
            return new Pointer(lhs._value + rhs);
        }

        public static Pointer operator -(Pointer lhs, int rhs)
        {
            return new Pointer((uint)(lhs._value - rhs));
        }

        public static Pointer operator &(Pointer lhs, int mask)
        {
            return new Pointer(lhs._value & (uint)mask);
        }

        public static int operator -(Pointer lhs, Pointer rhs)
        {
            return lhs.Diff(rhs);
        }

        public static bool operator <=(Pointer lhs, Pointer rhs)
        {
            return lhs._value <= rhs._value;
        }

        public static bool operator <(Pointer lhs, Pointer rhs)
        {
            return lhs._value < rhs._value;
        }

        public static bool operator >=(Pointer lhs, Pointer rhs)
        {
            return lhs._value >= rhs._value;
        }

        public static bool operator >(Pointer lhs, Pointer rhs)
        {
            return lhs._value > rhs._value;
        }

        public override int GetHashCode()
        {
            return (int)_value;
        }

        private static string ToHex(int v)
        {
            string res = new string(ToHexDigit((v & 0xf0) >> 4), 1);
            res += ToHexDigit(v & 0xf);
            return res;
        }

        private static char ToHexDigit(int v)
        {
            if (0 <= v && v < 10)
            {
                return (char)('0' + v);
            }
            else if (10 <= v && v < 16)
            {
                return (char)('a' + v - 10);
            }
            else
            {
                return '?';
            }
        }

        public int ToInt32()
        {
            return (int)_value;
        }

        public uint ToUInt32()
        {
            return _value;
        }

        public unsafe void* ToPointer()
        {
            return (void*)_value;
        }
    }
}

