using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace ExpressOS.Kernel
{
    public static class Deserializer
    {
        public static int ReadInt(byte[] buf, int offset)
        {
            Contract.Requires(buf.Length >= offset + sizeof(int));
            uint res = 0;
            for (var i = 0; i < sizeof(uint); ++i)
                res += (uint)buf[offset + i] << (8 * i);
            return (int)res;
        }

        public static uint ReadUInt(byte[] buf, int offset)
        {
            Contract.Requires(buf.Length >= offset + sizeof(uint));
            uint res = 0;
            for (var i = 0; i < sizeof(uint); ++i)
                res += (uint)buf[offset + i] << (8 * i);
            return res;
        }

        public static uint ReadUInt(ByteBufferRef buf, int offset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(buf.Length >= offset + sizeof(uint));
            uint res = 0;
            for (var i = 0; i < sizeof(uint); ++i)
            {
                Contract.Assert(offset + i < buf.Length);
                res += (uint)buf.Get(offset + i) << (8 * i);
            }
            return res;
        }

        public static ushort ReadUShort(byte[] buf, int offset)
        {
            Contract.Requires(buf.Length >= offset + sizeof(ushort));
            uint res = 0;
            for (var i = 0; i < sizeof(ushort); ++i)
                res += (uint)buf[offset + i] << (8 * i);
            return (ushort)res;
        }

        public static long ReadLong(byte[] buf, int offset)
        {
            Contract.Requires(buf.Length >= offset + sizeof(long));
            ulong res = 0;
            for (var i = 0; i < sizeof(long); ++i)
                res += (ulong)buf[offset + i] << (8 * i);
            return (long)res;
        }

        public static long ReadLong(ByteBufferRef buf, int offset)
        {
            return (long)ReadUlong(buf, offset);
        }

        public static ulong ReadUlong(ByteBufferRef buf, int offset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(buf.Length >= offset + sizeof(long));
            ulong res = 0;
            for (var i = 0; i < sizeof(long); ++i)
            {
                Contract.Assert(offset + i < buf.Length);
                res += (ulong)buf.Get(offset + i) << (8 * i);
            }
            return res;
        }

        public static void WriteInt(int val, ByteBufferRef buf, int offset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(buf.Length >= offset + sizeof(int));
            var v = (uint)val;
            WriteUInt(v, buf, offset);
        }

        public static void WriteUInt(uint val, ByteBufferRef buf, int offset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(buf.Length >= offset + sizeof(uint));
            for (var i = 0; i < sizeof(uint); ++i)
            {
                Contract.Assert(offset + i < buf.Length);
                buf.Set(offset + i, (byte)((val >> (8 * i)) & 0xff));
            }
        }

        public static void WriteShort(short val, ByteBufferRef buf, int offset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(buf.Length >= offset + sizeof(short));
            for (var i = 0; i < sizeof(short); ++i)
            {
                Contract.Assert(offset + i < buf.Length);
                buf.Set(offset + i, (byte)((val >> (8 * i)) & 0xff));
            }
        }

        public static void WriteULong(ulong val, ByteBufferRef buf, int offset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(buf.Length >= offset + sizeof(long));
            for (var i = 0; i < sizeof(long); ++i)
            {
                Contract.Assert(offset + i < buf.Length);
                buf.Set(offset + i, (byte)((val >> (8 * i)) & 0xff));
            }
        }

        public static int ReadInt(ByteBufferRef buf, int offset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(buf.Length >= offset + sizeof(int));
            uint res = 0;
            for (var i = 0; i < sizeof(uint); ++i)
            {
                Contract.Assert(offset + i < buf.Length);
                res += (uint)buf.Get(offset + i) << (8 * i);
            }
            return (int)res;
        }

        public static short ReadShort(ByteBufferRef buf, int offset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(buf.Length >= offset + sizeof(ushort));
            uint res = 0;
            for (var i = 0; i < sizeof(short); ++i)
            {
                Contract.Assert(offset + i < buf.Length);
                res += (uint)buf.Get(offset + i) << (8 * i);
            }
            return (short)res;
        }


        public static short ReadShort(byte[] buf, int offset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(buf.Length >= offset + sizeof(ushort));
            uint res = 0;
            for (var i = 0; i < sizeof(short); ++i)
            {
                Contract.Assert(offset + i < buf.Length);
                res += (uint)buf[offset + i] << (8 * i);
            }
            return (short)res;
        }
    }
}
