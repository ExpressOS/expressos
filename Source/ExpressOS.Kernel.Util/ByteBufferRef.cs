using System;
using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    /*
     * Class to represent a trunk of memory in the kernel.
     */
    public struct ByteBufferRef
    {
        public readonly IntPtr Location;
        private readonly int length;
        public int Length
        {
            [Pure]
            get
            {
                Contract.Ensures(Contract.Result<int>() == length);
                return length;
            }
        }

        public unsafe ByteBufferRef(byte[] buf)
        {
            Contract.Ensures(Contract.ValueAtReturn(out length) == buf.Length);
            fixed (byte* p = &buf[0])
            {
                this.Location = new IntPtr(p);
            }
            this.length = buf.Length;
        }

        public ByteBufferRef(IntPtr location, int size)
        {
            Contract.Ensures(Contract.ValueAtReturn(out Location) == location);
            Contract.Ensures(Contract.ValueAtReturn(out length) == size);

            this.Location = location;
            this.length = size;
        }

        [Pure]
        public ByteBufferRef Slice(int offset, int size)
        {
            Contract.Requires(offset >= 0 && size > 0);
            Contract.Requires(offset + size <= Length);
            Contract.Ensures(Contract.Result<ByteBufferRef>().Length == size);
            Contract.Ensures(Contract.OldValue(length) == length);

            return new ByteBufferRef((new Pointer(Location) + offset).ToIntPtr(), size);
        }

        [Pure]
        public static ByteBufferRef Empty
        {
            get
            {
                Contract.Ensures(!Contract.Result<ByteBufferRef>().isValid);
                return new ByteBufferRef(IntPtr.Zero, 0);
            }
        }

        [Pure]
        public bool isValid
        {
            get
            {
                return Location != IntPtr.Zero;
            }
        }

        [Pure]
        public static bool operator ==(ByteBufferRef lhs, ByteBufferRef rhs)
        {
            Contract.Ensures(Contract.Result<bool>() == (lhs.Location == rhs.Location && lhs.length == rhs.length));
            return lhs.Location == rhs.Location && lhs.length == rhs.length;
        }

        [Pure]
        public static bool operator !=(ByteBufferRef lhs, ByteBufferRef rhs)
        {
            return !(lhs == rhs);
        }

        [Pure]
        public override bool Equals(object obj)
        {
            return false;
        }

        [Pure]
        public override int GetHashCode()
        {
            return Location.ToInt32() ^ length;
        }

        public void ClearAfter(int offset)
        {
            Contract.Requires(offset >= 0);
            if (offset >= length)
                return;

            for (var i = offset; i < length; ++i)
            {
                Set(i, 0);
            }
        }

        public void Clear()
        {
            ClearAfter(0);
        }

        [Pure]
        public unsafe byte Get(int index)
        {
            Contract.Requires(index >= 0 && index < Length);
            Contract.Ensures(Length == Contract.OldValue(Length));
            return ((byte*)Location)[index];
        }

        public unsafe void Set(int index, byte v)
        {
            Contract.Requires(index >= 0 && index < Length);
            Contract.Ensures(Length == Contract.OldValue(Length));
            ((byte*)Location)[index] = v;
        }

        public byte this[int i]
        {
            get { return Get(i); }
            set { Set(i, value); }
        }

        public void CopyFrom(int offset, byte[] src)
        {
            Contract.Requires(offset >= 0 && offset < Length);
            for (var i = 0; i < src.Length && i + offset < length; ++i)
            {
                Set(i + offset, src[i]);
            }
        }

        public void CopyFrom(int offset, ByteBufferRef src)
        {
            Contract.Requires(offset >= 0 && offset < Length);
            for (var i = 0; i < src.Length && i + offset < length; ++i)
            {
                Set(i + offset, src[i]);
            }
        }

        public void CopyTo(int src_offset, byte[] dst, int dst_offset, int size)
        {
            Contract.Requires(src_offset >= 0 && src_offset + size <= Length);
            Contract.Requires(dst_offset >= 0 && dst_offset + size <= dst.Length);
            Contract.Requires(size > 0);

            for (var i = 0; i < size; ++i)
            {
                Contract.Assert(i + src_offset >= 0 && i + src_offset < src_offset + size);
                Contract.Assert(src_offset + size <= length);
                Contract.Assert(i + src_offset < length);
                var b = Get(i + src_offset);
                dst[i + dst_offset] = b;
            }
        }
    }
}
