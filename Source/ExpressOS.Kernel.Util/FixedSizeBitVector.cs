using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace ExpressOS.Kernel
{
    public struct FixedSizeBitVector
    {
        int n;
        public readonly byte[] Buffer;
        public const int BitsPerByte = 8;

        public FixedSizeBitVector(int bits)
        {
            this.n = bits;
            this.Buffer = new byte[Align(bits) / BitsPerByte];
        }

        public FixedSizeBitVector(int bits, byte[] b)
        {
            Contract.Requires(bits <= b.Length * BitsPerByte);
            this.n = bits;
            this.Buffer = b;
        }

        public void Set(int b)
        {
            if (b >= n)
                return;

            int byte_offset = b / BitsPerByte;
            Buffer[byte_offset] |= (byte)(1 << (b % BitsPerByte));
        }

        public int FindNextOne(int b)
        {
            int i = b + 1;
            if (i >= n)
                return -1;

            int byte_offset = i / BitsPerByte;

            var j = Util.ffs(Buffer[byte_offset] >> (i % BitsPerByte));
            if (j != 0)
                return i + j - 1;

            i = Align(i);
            byte_offset = i / BitsPerByte;
            
            while (i < n)
            {
                var k = Util.ffs(Buffer[byte_offset]);
                if (k == 0)
                {
                    i += BitsPerByte;
                    ++byte_offset;
                    continue;
                }
                else
                {
                    return byte_offset * BitsPerByte + k - 1;
                }

            }
            return -1;
        }

        private static int Align(int i)
        {
            return (i + 7) / BitsPerByte * BitsPerByte;
        }

        public int Length
        {
            get
            {
                return Buffer.Length;
            }
        }
    }
}
