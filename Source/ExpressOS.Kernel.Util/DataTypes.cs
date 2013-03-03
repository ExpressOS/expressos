using System;
using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    /*
     * TLS Struct, 16 bytes, see set_thread_area(2) for details
     */
    public struct userdesc
    {
        public ulong d0;
        public ulong d1;
    }

    public struct timespec
    {
        public uint tv_sec;
        public uint tv_nsec;
        public const int Size = 8;

        public ulong ToMilliseconds()
        {
            return tv_sec * 1000000 + tv_nsec / 1000;
        }
    }

    public struct timeval
    {
        public uint tv_sec;
        public uint tv_usec;
    }

    public struct sys_binder_write_desc
    {
        public Pointer write_buffer;

        /* r/w fields */
        public int buffer_size;
        public int patch_table_entries; /* data_entries */
        public int patch_table_offset;

        /* read only field */
        public int bwr_write_size;

        /* write only field */
        public int write_consumed;
        public int read_consumed;
    };

    public struct pollfd
    {
        public int fd;         /* file descriptor */
        public short events;     /* requested events */
        public short revents;    /* returned events */

        public const int Size = 8;
        public const int OFFSET_OF_REVENTS = 6;

        public static pollfd Deserialize(ByteBufferRef buf, int offset)
        {
            Contract.Assert(offset >= 0 && offset + Size <= buf.Length);
            pollfd res;
            res.fd = Deserializer.ReadInt(buf, offset);
            res.events = Deserializer.ReadShort(buf, offset + sizeof(int));
            res.revents = Deserializer.ReadShort(buf, offset + sizeof(int) + sizeof(short));
            return res;
        }

        public void Write(ByteBufferRef buf, int offset)
        {
            Contract.Assert(offset >= 0 && offset + Size <= buf.Length);
            Deserializer.WriteInt(fd, buf, offset);
            Deserializer.WriteShort(events, buf, offset + sizeof(int));
            Deserializer.WriteShort(revents, buf, offset + sizeof(int) + sizeof(short));
        }
    };

    public struct FileFlags
    {
        public const int ReadOnly = 0;
        public const int WriteOnly = 1;
        public const int ReadWrite = 2;
        public const int ReadWriteMask = 3;
        public const int Create = 0x200;
        public const int Append = 0x8;
    }
}
