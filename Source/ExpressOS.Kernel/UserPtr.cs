using System;

using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public struct UserPtr
    {
        private Pointer _value;

        public static explicit operator UserPtr(Pointer v)
        {
            return new UserPtr(v);
        }

        public UserPtr(uint v)
        {
            _value = new Pointer(v);
        }

        public UserPtr(Pointer v)
        {
            _value = v;
        }

        public UserPtr(int v)
        {
            _value = new Pointer((uint)v);
        }

        public Pointer Value
        {
            get
            {
                return _value;
            }
        }

        public static bool operator ==(UserPtr lhs, UserPtr rhs)
        {
            return lhs._value == rhs._value;
        }

        public static bool operator !=(UserPtr lhs, UserPtr rhs)
        {
            return !(lhs == rhs);
        }

        public static bool operator <(UserPtr lhs, UserPtr rhs)
        {
            return lhs._value < rhs._value;
        }

        public static bool operator <=(UserPtr lhs, UserPtr rhs)
        {
            return lhs._value <= rhs._value;
        }

        public static bool operator >(UserPtr lhs, UserPtr rhs)
        {
            return lhs._value > rhs._value;
        }

        public static bool operator >=(UserPtr lhs, UserPtr rhs)
        {
            return lhs._value >= rhs._value;
        }

        public static UserPtr operator &(UserPtr lhs, int mask)
        {
            return new UserPtr(lhs.Value & mask);
        }

        public static UserPtr operator -(UserPtr lhs, int rhs)
        {
            return new UserPtr(lhs.Value - rhs);
        }

        public static UserPtr operator +(UserPtr lhs, int rhs)
        {
            return new UserPtr(lhs.Value + rhs);
        }

        public static UserPtr operator +(UserPtr lhs, uint rhs)
        {
            return new UserPtr(lhs.Value + rhs);
        }

        public override int GetHashCode()
        {
            return _value.ToInt32();
        }

        public override bool Equals(object obj)
        {
            return false;
        }

        public static UserPtr Zero
        {
            get
            {
                return new UserPtr(0);
            }
        }

        private unsafe int Read(Thread current, void* dst, int length)
        {
            var process = current.Parent;
            var buf = new ByteBufferRef(new IntPtr(dst), length);
            var bytesRead = Read(process, buf, false);
            return length - bytesRead;
        }

        private int Write(Process process, Pointer dst, int length)
        {
            var bytesLeft = length;

            var src = _value;
            var dst_buf = new ByteBufferRef(dst.ToIntPtr(), length);
            var cursor = 0;
            while (bytesLeft > 0)
            {
                var region = process.Space.Find(src);

                // Invalid mapping
                if (region == null || region.IsFixed)
                {
                    return bytesLeft;
                }

                var off = Arch.ArchDefinition.PageOffset(src.ToUInt32());
                var virtualAddr = process.Space.UserToVirt(new UserPtr(src));

                if (virtualAddr == Pointer.Zero)
                {
                    // Page isn't present, try to bring it in.
                    uint permission;

                    Pager.HandlePageFault(process, MemoryRegion.FAULT_MASK, src, Pointer.Zero, out virtualAddr, out permission);

                    if (virtualAddr == Pointer.Zero)
                        break;
                }

                var virtual_page = Arch.ArchDefinition.PageIndex(virtualAddr.ToUInt32());
                var page_buf = new ByteBufferRef(new IntPtr(virtual_page), Arch.ArchDefinition.PageSize);

                var b = Arch.ArchDefinition.PageSize - off;
                var bytesTobeCopied = b > bytesLeft ? bytesLeft : b;

                var dst_buf_page = page_buf.Slice(off, bytesTobeCopied);
                for (var i = 0; i < bytesTobeCopied; ++i)
                {
                    dst_buf_page.Set(i, dst_buf.Get(cursor + i));
                }

                bytesLeft -= bytesTobeCopied;
                src += bytesTobeCopied;
                cursor += bytesTobeCopied;
            }

            return bytesLeft;
        }

        private int Read(Process process, ByteBufferRef buffer, bool is_string)
        {
            var bytesLeft = is_string ? buffer.Length - 1 : buffer.Length;
            var bytesRead = 0;

            var src = _value;

            while (bytesLeft > 0)
            {
                var region = process.Space.Find(src);

                // Invalid mapping
                if (region == null || region.IsFixed)
                    break;

                var off = Arch.ArchDefinition.PageOffset(src.ToUInt32());
                var virtualAddr = process.Space.UserToVirt(new UserPtr(src));

                if (virtualAddr == Pointer.Zero)
                {
                    uint permission;

                    Pager.HandlePageFault(process, MemoryRegion.FAULT_MASK, _value, Pointer.Zero, out virtualAddr, out permission);

                    if (virtualAddr == Pointer.Zero)
                        break;
                }

                var virtual_page = Arch.ArchDefinition.PageIndex(virtualAddr.ToUInt32());
                var page_buf = new ByteBufferRef(new IntPtr(virtual_page), Arch.ArchDefinition.PageSize);
                
                var b = Arch.ArchDefinition.PageSize - off;
                var bytesTobeCopied = b > bytesLeft ? bytesLeft : b;

                var src_buf = page_buf.Slice(off, bytesTobeCopied);

                if (is_string)
                {
                    var res = Util.CopyString(ref buffer, bytesRead, src_buf);
                    bytesRead += res;

                    if (res < bytesTobeCopied)
                    {
                        // We're done.
                        break;
                    }
                }
                else
                {
                    for (var i = 0; i < bytesTobeCopied; ++i)
                    {
                        buffer.Set(i + bytesRead, src_buf.Get(i));
                    }
                    bytesRead += bytesTobeCopied;
                }

                bytesLeft -= bytesTobeCopied;
                src += bytesTobeCopied;
            }

            if (is_string)
            {
                buffer.Set(bytesRead, 0);
            }
            return bytesRead;
        }

        public int ReadString(Thread current, ByteBufferRef buf)
        {
            return Read(current.Parent, buf, true);
        }

        public int ReadString(Thread current, byte[] buf)
        {
            var b = new ByteBufferRef(buf);
            return ReadString(current, b);
        }

        public int Write(Thread current, Pointer kernelPointer, int length)
        {
            return Write(current.Parent, kernelPointer, length);
        }

        public static UserPtr RoundDown(UserPtr stack_top)
        {
            return new UserPtr(stack_top.Value.ToUInt32() & ~3U);
        }

        #region Read Helper

        internal unsafe int Read(Thread current, ByteBufferRef buf, int desired_length)
        {
            Contract.Requires(buf.Length >= desired_length);
            return Read(current, buf.Location.ToPointer(), desired_length);
        }

        internal unsafe int Read(Thread current, out uint val)
        {
            uint v = 0;
            var r = Read(current, &v, sizeof(uint));
            val = v;
            return r;
        }

        internal unsafe int Read(Thread current, out int val)
        {
            int v = 0;
            var r = Read(current, &v, sizeof(int));
            val = v;
            return r;
        }

        internal unsafe int Read(Thread current, out timespec val)
        {
            timespec v;
            var r = Read(current, &v, sizeof(timespec));
            val = v;
            return r;
        }

        internal unsafe int Read(Thread current, out timeval val)
        {
            timeval v;
            var r = Read(current, &v, sizeof(timeval));
            val = v;
            return r;
        }

        internal unsafe int Read(Thread current, out binder_write_read val)
        {
            binder_write_read v;
            var r = Read(current, &v, sizeof(binder_write_read));
            val = v;
            return r;
        }

        internal unsafe int Read(Thread current, out flat_binder_object val)
        {
            flat_binder_object v;
            var r = Read(current, &v, sizeof(flat_binder_object));
            val = v;
            return r;
        }

        internal unsafe int Read(Thread current, out userdesc val)
        {
            userdesc v;
            var r = Read(current, &v, sizeof(userdesc));
            val = v;
            return r;
        }

        #endregion

        internal unsafe int Read(Thread current, byte[] WriteBuffer, int chunk_len)
        {
            Contract.Requires(WriteBuffer.Length >= chunk_len);
            fixed (byte *p = &WriteBuffer[0]) {
                return Read(current, p, chunk_len);
            }
        }

        #region Write Helpers

        public int Write(Thread current, int v)
        {
            return Write(current.Parent, v);
        }

        public unsafe int Write(Thread current, short v)
        {
            return Write(current.Parent, new Pointer(&v), sizeof(short));
        }

        internal unsafe int Write(Process proc, int v)
        {
            var p = v;
            return Write(proc, new Pointer(&p), sizeof(int));
        }

        internal int Write(Process proc, byte[] val)
        {
            var b = new ByteBufferRef(val);
            return Write(proc, b);
        }

        internal int Write(Process proc, ByteBufferRef val)
        {
            return Write(proc, new Pointer(val.Location), val.Length);
        }

        internal int Write(Thread current, ByteBufferRef val)
        {
            return Write(current.Parent, val);
        }


        internal int Write(Thread current, byte[] val)
        {
            return Write(current.Parent, val);
        }

        internal unsafe int Write(Process proc, uint[] val)
        {
            fixed (uint *p = &val[0]) {
                return Write(proc, new Pointer(p), val.Length * sizeof(uint));
            }
        }

        internal unsafe int Write(Thread current, ref timespec res)
        {
            fixed (timespec* p = &res)
            {
                return Write(current, new Pointer(p), sizeof(timespec));
            }
        }

        internal unsafe int Write(Thread current, ref timeval r)
        {
            fixed (timeval* p = &r)
            {
                return Write(current, new Pointer(p), sizeof(timeval));
            }
        }

        internal unsafe int Write(Thread current, ref binder_write_read r)
        {
            fixed (binder_write_read* p = &r)
            {
                return Write(current, new Pointer(p), sizeof(binder_write_read));
            }
        }

        internal unsafe int Write(Thread current, userdesc r)
        {
            return Write(current, new Pointer(&r), sizeof(userdesc));
        }

        #endregion


    }
}
