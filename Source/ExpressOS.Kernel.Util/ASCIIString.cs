using System;
using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    /*
     * Class to represent a null terminated string.
     */
    public struct ASCIIString
    {
        private byte[] raw;

        public int Length
        {
            get
            {
                return raw.Length - 1;
            }
        }

        public ASCIIString(string s)
        {
            Contract.Requires(s != null);
            raw = new byte[s.Length + 1];
            for (var i = 0; i < s.Length; ++i)
                raw[i] = (byte)s[i];
            raw[s.Length] = 0;
        }

        public ASCIIString(byte[] b)
        {
            this.raw = b;
        }

        public byte[] GetByteString()
        {
            return raw;
        }
    }
}

