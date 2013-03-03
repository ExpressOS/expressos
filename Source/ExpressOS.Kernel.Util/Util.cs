using ExpressOS.Kernel;
using System;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;

namespace ExpressOS.Kernel
{
    public static class Util
    {
        public static uint ToUInt(int v)
        {
            if (v >= 0)
                return (uint)v;

            return (uint)(-v) | 0x80000000;
        }

        public static int FindMostSignificantBit(int v)
        {
            int res = 0;
            while (v != 0)
            {
                ++res;
                v >>= 1;
            }
            return res;
        }

        // like strncpy, but return the number of bytes copied.
        // It never add 0 to terminate the string
        public static int CopyString(ref ByteBufferRef dst, int cursor, ByteBufferRef src)
        {
            Contract.Requires(cursor >= 0 && cursor < dst.Length);

            var i = 0;
            while (i + cursor < dst.Length && i < src.Length && src.Get(i) != 0)
            {
                Contract.Assert(i < src.Length);
                dst.Set(i + cursor, src.Get(i));
                ++i;
            }
            return i;
        }

        public static int Strnlen(ByteBufferRef buf, int max_len)
        {
            var i = 0;
            while (i < max_len && i < buf.Length)
            {
                if (buf.Get(i) == 0)
                    return i;

                ++i;
            }
            return i;
        }

        public static int ffs(int i)
        {
            var res = 1;
            while (i != 0)
            {
                if ((i & 0x1) != 0)
                    return res;

                i >>= 1;
                res++;
            }
            return 0;
        }

        public static int msb(uint v)
        {
            int r = 0;
            while (v != 0)
            {
                v >>= 1;
                r++;
            }

            return r;
        }

        public static byte[] StringToByteArray(string s, bool with_null)
        {
            var b = new byte[with_null ? s.Length + 1 : s.Length];
            for (var i = 0; i < s.Length; ++i)
                b[i] = (byte)s[i];

            if (with_null)
                b[b.Length - 1] = 0;

            return b;
        }

        //
        // String comparison. It can mimic both strcmp() / strncmp(), depending whether lhs/rhs contains the trailing zero.
        //

        public static int ByteStringCompare(byte[] lhs, byte[] rhs)
        {
            for (var i = 0; i < lhs.Length && i < rhs.Length; ++i)
            {
                if (lhs[i] == rhs[i])
                {
                    if (lhs[i] == 0)
                        return 0;
                    else
                        continue;
                }

                return lhs[i] > rhs[i] ? 1 : -1;
            }
            return 0;
        }

        public static bool ByteArrayEqual(byte[] lhs, byte[] rhs)
        {
            if (lhs.Length != rhs.Length)
                return false;

            for (var i = 0; i < lhs.Length; ++i)
            {
                if (lhs[i] != rhs[i])
                    return false;
            }
            return true;
        }
    }
}

