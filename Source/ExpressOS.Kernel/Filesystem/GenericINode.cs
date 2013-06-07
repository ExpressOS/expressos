using System.Diagnostics.Contracts;
namespace ExpressOS.Kernel
{
    /*
     * The base class of all inodes.
     * To work around the partial support of inheritance, it dispatches virtual calls.
     */
    public class GenericINode
    {
        public const int SIZE_OF_STAT64 = 96;

        public enum INodeKind
        {
            InvalidINodeKind,
            ArchINodeKind,
            ConsoleINodeKind,
            BinderINodeKind,
            BinderSharedINodeKind,
            AshmemINodeKind,
            ScreenBufferINodeKind,
            SecureFSINodeKind,
            SocketINodeKind,
        }

        public readonly INodeKind kind;
        int refCount;

        internal Arch.ArchINode ArchINode
        {
            get
            {
                switch (kind)
                {
                    case INodeKind.ArchINodeKind:
                    case INodeKind.BinderSharedINodeKind:
                    case INodeKind.AshmemINodeKind:
                    case INodeKind.ScreenBufferINodeKind:
                    case INodeKind.SecureFSINodeKind:
                    case INodeKind.SocketINodeKind:
                        return (Arch.ArchINode)this;
                    default:
                        return null;
                }
            }
        }

        internal ConsoleINode ConsoleINode
        { get { return kind == INodeKind.ConsoleINodeKind ? (ConsoleINode)this : null; } }
        internal BinderINode BinderINode
        { get { return kind == INodeKind.BinderINodeKind ? (BinderINode)this : null; } }
        internal BinderSharedINode BinderSharedINode
        { get { return kind == INodeKind.BinderSharedINodeKind ? (BinderSharedINode)this : null; } }
        internal AshmemINode AshmemINode
        { get { return kind == INodeKind.AshmemINodeKind ? (AshmemINode)this : null; } }
        internal ScreenBufferINode ScreenINode
        { get { return kind == INodeKind.ScreenBufferINodeKind ? (ScreenBufferINode)this : null; } }
        internal SecureFSInode SFSINode
        { get { return kind == INodeKind.SecureFSINodeKind ? (SecureFSInode)this : null; } }
        internal SocketINode SocketINode
        { get { return kind == INodeKind.SocketINodeKind ? (SocketINode)this : null; } }
        internal AlienSharedMemoryINode AlienSharedMemoryINode
        {
            get
            {
                if (kind == INodeKind.AshmemINodeKind || kind == INodeKind.BinderSharedINodeKind || kind == INodeKind.ScreenBufferINodeKind)
                    return (AlienSharedMemoryINode)this;
                else
                    return null;
            }
        }

        
        internal GenericINode(INodeKind kind)
        {
            this.kind = kind;
        }

        private int Close()
        {
            switch (kind)
            {
                case INodeKind.ArchINodeKind:
                case INodeKind.AshmemINodeKind:
                case INodeKind.BinderSharedINodeKind:
                case INodeKind.ScreenBufferINodeKind:
                case INodeKind.SocketINodeKind:
                    ArchINode.Close();
                    return 0;
                case INodeKind.SecureFSINodeKind:
                    return SFSINode.SFSClose();
                default:
                    return 0;
            }
        }

        public int Read(ByteBufferRef buffer, int offset, int count, ref uint pos)
        {
            switch (kind)
            {
                case INodeKind.ArchINodeKind:
                    return ArchINode.ReadImpl(buffer, offset, count, ref pos);
            }
            return -ErrorCode.EINVAL;
        }

        public void IncreaseRefCount()
        {
            ++refCount;
        }

        internal int DecreaseRefCount()
        {
            --refCount;
            Utils.Assert(refCount >= 0);
            if (refCount > 0)
                return 0;

            return Close();
        }

        internal int Write(Thread current, ref Arch.ExceptionRegisters regs, UserPtr userBuf, int len, uint pos, File file)
        {
            var buf = Globals.AllocateAlignedCompletionBuffer(len);

            if (!buf.isValid)
                return -ErrorCode.ENOMEM;

            var l = userBuf.Read(current, buf, len);
            var bytesToBeWritten = len - l;

            var ret = Write(current, ref regs, ref buf, bytesToBeWritten, pos, file);

            // Buffer hasn't been taken by write(), free it here
            if (buf.isValid)
                Globals.CompletionQueueAllocator.FreePages(new Pointer(buf.Location), buf.Length >> Arch.ArchDefinition.PageShift);

            return ret;
        }

        /*
         * Write a segment buffer.
         * 
         * The buffer itself is passed as a reference, because some inode might take the ownership
         * of the buffer and put it as a part of its completion. In this case the buf is set to empty
         */
        internal int Write(Thread current, ref Arch.ExceptionRegisters regs, ref ByteBufferRef buf, int len, uint pos, File file)
        {
            switch (kind)
            {
                case INodeKind.ConsoleINodeKind:
                    {
                        uint dummy = 0;
                        return ConsoleINode.WriteImpl(current, buf, len, ref dummy);
                    }
                case INodeKind.ArchINodeKind:
                case INodeKind.SocketINodeKind:
                    return ArchINode.ArchWrite(current, ref regs, ref buf, len, pos, file);

                case INodeKind.SecureFSINodeKind:
                    return SFSINode.SFSWrite(current, ref regs, buf, len, pos, file);
            }
            return -ErrorCode.EINVAL;
        }

        internal int Read(Thread current, ref Arch.ExceptionRegisters regs, UserPtr userBuf, int len, uint pos, File file)
        {
            switch (kind)
            {
                case INodeKind.ArchINodeKind:
                case INodeKind.SocketINodeKind:
                    return ArchINode.ArchRead(current, ref regs, userBuf, len, pos, file);

                case INodeKind.SecureFSINodeKind:
                    return SFSINode.SFSRead(current, ref regs, userBuf, len, pos, file);
            }
            return -ErrorCode.EINVAL;
        }

        internal int Ioctl(Thread current, ref Arch.ExceptionRegisters regs, int arg0, int arg1)
        {
            switch (kind)
            {
                case INodeKind.BinderINodeKind:
                    return BinderINode.Ioctl(current, ref regs, (uint)arg0, new UserPtr(arg1));

                // There's no good way to figure out the type of a bindersharednode is.
                // So we have to guess a little bit..
                case INodeKind.AshmemINodeKind:
                case INodeKind.BinderSharedINodeKind:
                case INodeKind.ArchINodeKind:
                case INodeKind.SocketINodeKind:
                    return Ioctl(current, (uint)arg0, new UserPtr(arg1));
            }
            return -ErrorCode.EINVAL;
        }

        internal int FStat64(Thread current, UserPtr buf)
        {
            switch (kind)
            {
                case INodeKind.ArchINodeKind:
                    return ArchINode.ArchFStat64(current, buf);
                case INodeKind.SecureFSINodeKind:
                    return SFSINode.SFSFStat64(current, buf);
            }
            return -ErrorCode.EINVAL;
        }

        public uint Size
        {
            get
            {
                switch (kind)
                {
                    case INodeKind.ArchINodeKind:
                        return ArchINode.ArchInodeSize;
                    case INodeKind.SecureFSINodeKind:
                        return SFSINode.FileSize;
                    default:
                        return 0;
                }
            }
            set
            {
                switch (kind)
                {
                    case INodeKind.ArchINodeKind:
                        ArchINode.ArchInodeSize = value;
                        break;
                    case INodeKind.SecureFSINodeKind:
                        SFSINode.ftruncate((int)value);
                        break;
                    default:
                        return;
                }
            }
        }

        public int LinuxFd
        {
            get
            {
                var inode = BackArchINode;
                return inode == null ? -1 : BackArchINode.Fd;
            }
        }

        private Arch.ArchINode BackArchINode
        {
            get
            {
                switch (kind)
                {
                    case INodeKind.AshmemINodeKind:
                    case INodeKind.BinderSharedINodeKind:
                    case INodeKind.ArchINodeKind:
                    case INodeKind.ScreenBufferINodeKind:
                    case INodeKind.SocketINodeKind:
                    case INodeKind.SecureFSINodeKind:
                        return (Arch.ArchINode)this;
                    default:
                        return null;
                }
            }
        }

        private int Ioctl(Thread current, uint cmd, UserPtr arg1)
        {
            //Arch.Console.Write("ioctl:, cmd=");
            //Arch.Console.Write(cmd);
            //Arch.Console.WriteLine();
            // marshal arguments
            switch (cmd)
            {
                case AshmemINode.ASHMEM_SET_NAME:
                case AshmemINode.ASHMEM_PIN:
                case AshmemINode.ASHMEM_UNPIN:
                case AshmemINode.ASHMEM_GET_NAME:
                case AshmemINode.ASHMEM_SET_SIZE:
                case AshmemINode.ASHMEM_GET_SIZE:
                case AshmemINode.ASHMEM_SET_PROT_MASK:
                case AshmemINode.ASHMEM_GET_PROT_MASK:
                case AshmemINode.ASHMEM_GET_PIN_STATUS:
                case AshmemINode.ASHMEM_PURGE_ALL_CACHES:
                    return AshmemINode.AshmemIoctl(this, current, cmd, arg1);

                case FileSystem.FIONREAD:
                    return LinuxIoctl(current, cmd, arg1);
                default:
                    return -1;
            }
        }

        private int LinuxIoctl(Thread current, uint cmd, UserPtr arg1)
        {
            var msg_buf = Globals.LinuxIPCBuffer;
            int ret = 0;

            // marshal arguments
            switch (cmd)
            {
                case FileSystem.FIONREAD:
                    break;

                default:
                    return -ErrorCode.ENOTTY;
            }

            ret = Arch.IPCStubs.linux_sys_vfs_linux_ioctl(current.Parent.helperPid, LinuxFd, cmd, arg1.Value.ToInt32());

            if (ret < 0)
                return ret;

            // unmarshal if necessary
            if (cmd == FileSystem.FIONREAD)
            {
                if (arg1.Write(current, new Pointer(msg_buf.Location), sizeof(int)) != 0)
                    return -ErrorCode.EFAULT;
            }

            return ret;
        }

        public int Truncate(Thread current, int length)
        {
            switch (kind)
            {
                case INodeKind.ArchINodeKind:
                    return ArchINode.ftruncate(current, length);

                case INodeKind.SecureFSINodeKind:
                    return SFSINode.ftruncate(length);
            }

            return -ErrorCode.EINVAL;
        }
    }
}
