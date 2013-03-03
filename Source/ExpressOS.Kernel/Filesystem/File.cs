using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public class File
    {
        public readonly Process GhostOwner;

        public readonly GenericINode inode;
        public readonly int flags;
        public readonly int mode;
        public uint position;

        public File(Process owner, GenericINode inode, int flags, int mode)
        {
            Contract.Ensures(GhostOwner == owner);

            this.inode = inode;
            this.flags = flags;
            this.mode = mode;
            this.position = 0;
            this.GhostOwner = owner;
            inode.IncreaseRefCount();
        }

        public int Read(Thread current, ref Arch.ExceptionRegisters regs, UserPtr userBuf, int len)
        {
            Contract.Requires(GhostOwner == current.Parent);
            var ret = inode.Read(current, ref regs, userBuf, len, this.position, this);
            return ret;
        }

        public int Read(Thread current, ref Arch.ExceptionRegisters regs, UserPtr userBuf, int len, uint pos)
        {
            Contract.Requires(GhostOwner == current.Parent);
            var ret = inode.Read(current, ref regs, userBuf, len, pos, null);
            return ret;
        }

        internal int Write(Thread current, ref Arch.ExceptionRegisters regs, UserPtr userBuf, int len)
        {
            Contract.Requires(GhostOwner == current.Parent);
            var ret = inode.Write(current, ref regs, userBuf, len, position, this);
            return ret;
        }

        internal int Write(Thread current, ref Arch.ExceptionRegisters regs, ref ByteBufferRef buf, int len)
        {
            Contract.Requires(GhostOwner == current.Parent);
            var ret = inode.Write(current, ref regs, ref buf, len, position, this);
            return ret;
        }

        public int Ioctl(Thread current, ref Arch.ExceptionRegisters regs, int arg0, int arg1)
        {
            return inode.Ioctl(current, ref regs, arg0, arg1);
        }

        internal static File CreateStdout(Process proc)
        {
            Contract.Ensures(Contract.Result<File>().GhostOwner == proc);
            return new File(proc, ConsoleINode.Instance, FileFlags.WriteOnly, 0);
        }

        public int Close()
        {
            return inode.DecreaseRefCount();
        }

        internal int FStat64(Thread current, UserPtr buf)
        {
            Contract.Requires(GhostOwner == current.Parent);
            return inode.FStat64(current, buf);
        }

        internal int Lseek(Thread current, int offset, int origin)
        {
            Contract.Requires(GhostOwner == current.Parent);
            long new_pos = 0;
            switch (origin)
            {
                case FileSystem.SEEK_CUR:
                    new_pos = position + offset;
                    break;

                case FileSystem.SEEK_SET:
                    new_pos = offset;
                    break;

                case FileSystem.SEEK_END:
                    new_pos = inode.Size + offset;
                    break;

                default:
                    return -ErrorCode.EINVAL;
            }

            if (new_pos > inode.Size)
            {
                if (origin == FileSystem.SEEK_SET)
                {
                    inode.Size = (uint)new_pos;
                }
                new_pos = inode.Size;
            }
            else
            if (new_pos < 0)
            {
                new_pos = 0;
            }
            position = (uint)new_pos;
            return (int)new_pos;
        }

        public int Truncate(Thread current, int length)
        {
            Contract.Requires(GhostOwner == current.Parent);
            return inode.Truncate(current, length);
        }

        internal int Read(ByteBufferRef buffer, int offset, int size, ref uint pos)
        {
            return inode.Read(buffer, offset, size, ref pos);
        }

        internal int Read(byte[] buf, ref uint pos)
        {
            var buf_ref = new ByteBufferRef(buf);
            return inode.Read(buf_ref, 0, buf.Length, ref pos);
        }
    }
}

