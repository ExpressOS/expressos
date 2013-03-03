using System;

namespace ExpressOS.Kernel.Arch
{
    public class ArchINode : GenericINode
    {
        private readonly int fd;
        public int Fd
        {
            get
            {
                return fd;
            }
        }

        public uint ArchInodeSize;
        public readonly int helperPid;

        internal ArchINode(int fd, uint size, int helperPid, INodeKind kind)
            : base(kind)
        {
            this.fd = fd;
            this.ArchInodeSize = size;
            this.helperPid = helperPid;
        }

        public ArchINode(int fd, uint size, int helperPid)
            : this(fd, size, helperPid, INodeKind.ArchINodeKind)
        { }

        public void Close()
        {
            IPCStubs.Close(helperPid, fd);
        }

        public int ReadImpl(ByteBufferRef buffer, int offset, int count, ref uint pos)
        {
            var max_length = buffer.Length - offset > count ? count : buffer.Length - offset;
            if (max_length < 0)
            {
                pos = 0;
                return -ErrorCode.EINVAL;
            }

            int ret = IPCStubs.Read(helperPid, fd, new Pointer(buffer.Location + offset), count, ref pos);
            return ret;
        }

        internal int ArchRead(Thread current, ref ExceptionRegisters regs, UserPtr userBuf, int len, uint pos, File file)
        {
            var buf = Globals.AllocateAlignedCompletionBuffer(len);

            if (!buf.isValid)
                return -ErrorCode.ENOMEM;

            var iocp = IOCompletion.CreateReadIOCP(current, userBuf, len, file, buf);
       
            var r = IPCStubs.ReadAsync(current.Parent.helperPid, current.impl._value.thread._value, new Pointer(buf.Location), fd, len, pos);

            if (r < 0)
            {
                iocp.Dispose();
                return r;
            }

            Globals.CompletionQueue.Enqueue(iocp);
            current.SaveState(ref regs);
            current.AsyncReturn = true;
            return 0;
        }

        internal int ArchWrite(Thread current, ref ExceptionRegisters regs, ref ByteBufferRef buf, int len, uint pos, File file)
        {
            if (!Globals.CompletionQueueAllocator.Contains(buf))
            {
                Arch.Console.WriteLine("inode-write: unimplemented");
                Arch.ArchDefinition.Panic();
            }

            var iocp = IOCompletion.CreateWriteIOCP(current, file, buf);
            var r = IPCStubs.WriteAsync(current.Parent.helperPid, current.impl._value.thread._value, new Pointer(buf.Location), fd, len, pos);

            if (r < 0)
            {
                iocp.Dispose();
                return r;
            }

            Globals.CompletionQueue.Enqueue(iocp);
            current.SaveState(ref regs);
            current.AsyncReturn = true;
            // Take the buffer
            buf = ByteBufferRef.Empty;
            return 0;
        }

        public static void HandleIOCP(IOCompletion iocp, int retval, int new_pos)
        {
            var current = iocp.thr;

            switch (iocp.type)
            {
                case IOCompletion.Type.Read:
                    HandleReadAsync(current, iocp, retval, new_pos);
                    return;
                case IOCompletion.Type.Write:
                    HandleWriteAsync(current, iocp, retval, new_pos);
                    return;
            }
        }

        private static void HandleReadAsync(Thread current, IOCompletion iocp, int retval, int new_pos)
        {
            if (retval < 0)
            {
                iocp.Dispose();
                current.ReturnFromCompletion(retval);
                return;
            }

            var notWritten = iocp.userBuf.Write(current, new Pointer(iocp.buf.Location), retval);
            if (iocp.posToUpdate != null)
            {
                iocp.posToUpdate.position = (uint)(new_pos - notWritten);
            }

            iocp.Dispose();
            current.ReturnFromCompletion(retval);
        }

        private static void HandleWriteAsync(Thread current, IOCompletion iocp, int retval, int new_pos)
        {
            if (retval < 0)
            {
                iocp.Dispose();
                current.ReturnFromCompletion(retval);
                return;
            }

            if (iocp.posToUpdate != null)
            {
                var inode = iocp.posToUpdate.inode;
                iocp.posToUpdate.position = (uint)new_pos;
                // If position went beyond size, update size.
                if (new_pos > inode.Size)
                    inode.Size = (uint)new_pos;
            }

            iocp.Dispose();
            current.ReturnFromCompletion(retval);
        }

        public int ArchFStat64(Thread current, UserPtr buf)
        {
            var ret = IPCStubs.linux_sys_fstat64(current.Parent.helperPid, Fd);
            if (ret < 0)
                return ret;

            if (buf.Write(current, new Pointer(Globals.LinuxIPCBuffer.Location), GenericINode.SIZE_OF_STAT64) != 0)
                return -ErrorCode.EFAULT;

            return 0;
        }

        internal int ftruncate(Thread current, int length)
        {
            if (length < 0)
                return -ErrorCode.EINVAL;

            var ret = IPCStubs.Ftruncate(current.Parent.helperPid, Fd, length);

            if (ret < 0)
                return ret;

            ArchInodeSize = (uint)length;

            return 0;
        }
    }
}
