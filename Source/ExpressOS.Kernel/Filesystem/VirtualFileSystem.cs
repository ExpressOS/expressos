using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public static class FileSystem
    {
        public const int PATH_MAX = 1024;
        private static ASCIIString IPCFilename;
        private static ASCIIString AshmemFileName;
        private static ASCIIString CWDStr;

        #region ioctl constants
        public const int _IOC_NRBITS = 8;
        public const int _IOC_TYPEBITS = 8;
        public const int _IOC_SIZEBITS = 14;
        public const int _IOC_DIRBITS = 2;

        public const int _IOC_NRMASK = ((1 << _IOC_NRBITS) - 1);
        public const int _IOC_TYPEMASK = ((1 << _IOC_TYPEBITS) - 1);
        public const int _IOC_SIZEMASK = ((1 << _IOC_SIZEBITS) - 1);
        public const int _IOC_DIRMASK = ((1 << _IOC_DIRBITS) - 1);

        public const int _IOC_NRSHIFT = 0;
        public const int _IOC_TYPESHIFT = (_IOC_NRSHIFT + _IOC_NRBITS);
        public const int _IOC_SIZESHIFT = (_IOC_TYPESHIFT + _IOC_TYPEBITS);
        public const int _IOC_DIRSHIFT = (_IOC_SIZESHIFT + _IOC_SIZEBITS);

        public const int FIONREAD = 0x541B;

        #endregion

        public const int SIZEOF_OLD_STAT = 32;
        private const int OFFSET_OF_SIZE_IN_STAT64 = 44;
        private const int OFFSET_OF_MODE_IN_STAT64 = 16;
        public const int SIZE_OF_STAT64 = 96;
        public const int S_IFMT = 0xf000;
        public const int S_IFDIR = 0x4000;


        public const int SEEK_SET = 0;
        public const int SEEK_CUR = 1;
        public const int SEEK_END = 2;



        public const int O_RDWR = 2;

        private const int F_GETFD = 1;
        private const int F_SETFD = 2;
        private const int F_GETFL = 3;
        private const int F_SETFL = 4;
        private const int F_GETLK = 5;
        private const int F_SETLK = 6;
        private const int F_SETLKW = 7;

        public static int IoctlSize(uint ioctlCommand)
        {
            return (int)((ioctlCommand >> _IOC_SIZESHIFT) & _IOC_SIZEMASK);
        }

        public static void Initialize()
        {
            IPCFilename = new ASCIIString("/dev/binder");
            AshmemFileName = new ASCIIString("/dev/ashmem");
            CWDStr = new ASCIIString("/");
        }

        public static int Writev(Thread current, ref Arch.ExceptionRegisters regs, int fd, UserPtr iovPtr, int iovcnt)
        {
            if (iovcnt < 0)
                return -ErrorCode.EINVAL;

            if (iovcnt == 0)
                return 0;

            var proc = current.Parent;
            var file = proc.LookupFile(fd);

            if (file == null)
                return -ErrorCode.EBADF;

            var mode = file.flags & FileFlags.ReadWriteMask;

            if (mode == FileFlags.ReadOnly)
                return -ErrorCode.EPERM;

            var iovec_buf = new byte[IOVector.Size * iovcnt];

            if (iovPtr.Read(current, iovec_buf, iovec_buf.Length) != 0)
            {
                Arch.Console.WriteLine("Cannot read iovec");
                return -ErrorCode.EFAULT;
            }

            int totalLength = 0;
            for (int i = 0; i < iovcnt; ++i)
            {
                Contract.Assert((i + 1) * IOVector.Size <= iovec_buf.Length);
                var iovec = IOVector.Deserialize(iovec_buf, i * IOVector.Size);
                totalLength += iovec.iov_len;
            }

            var buf = Globals.AllocateAlignedCompletionBuffer(totalLength);
            if (!buf.isValid)
                return -ErrorCode.ENOMEM;

            int cursor = 0;
            for (int i = 0; i < iovcnt; ++i)
            {
                Contract.Assert((i + 1) * IOVector.Size <= iovec_buf.Length);
                var iovec = IOVector.Deserialize(iovec_buf, i * IOVector.Size);

                var chunk = buf.Slice(cursor, iovec.iov_len);

                // Post condition of Slice
                Contract.Assume(chunk.Length >= iovec.iov_len);
                if (iovec.iov_base.Read(current, chunk, iovec.iov_len) != 0)
                {
                    Globals.CompletionQueueAllocator.FreePages(new Pointer(buf.Location), buf.Length >> Arch.ArchDefinition.PageShift);
                    return -ErrorCode.EFAULT;
                }

                cursor += iovec.iov_len;
            }

            int ret = file.Write(current, ref regs, ref buf, totalLength);

            if (buf.isValid)
                Globals.CompletionQueueAllocator.FreePages(new Pointer(buf.Location), buf.Length >> Arch.ArchDefinition.PageShift);
            return ret;
        }

        public static int Open(Thread current, ref Arch.ExceptionRegisters regs, UserPtr filenamePtr, int flags, int mode)
        {
            // TODO: Deal with current path
            var filenameBuf = new byte[PATH_MAX];
            var ret = filenamePtr.ReadString(current, filenameBuf);

            var proc = current.Parent;
            int fd = 0;
            GenericINode inode = null;

            var startTime = Arch.NativeMethods.l4api_get_system_clock();

            if (Util.ByteStringCompare(filenameBuf, IPCFilename.GetByteString()) == 0)
            {
                fd = proc.GetUnusedFd();
                inode = BinderINode.Instance;
            }
            else if (Util.ByteStringCompare(filenameBuf, AshmemFileName.GetByteString()) == 0)
            {
                var linux_fd = Arch.ArchFS.OpenAndReturnLinuxFd(current.Parent.helperPid, new ASCIIString(filenameBuf), flags, mode);
                if (linux_fd < 0)
                    return linux_fd;

                inode = new AshmemINode(linux_fd, current.Parent.helperPid);
                fd = proc.GetUnusedFd();
            }
            else if (SecureFS.IsSecureFS(current, filenameBuf))
            {
                var completion = SecureFS.OpenAndReadPagesAsync(current, filenameBuf, flags, mode);
                if (completion == null)
                    return -ErrorCode.ENOMEM;

                Globals.CompletionQueue.Enqueue(completion);
                current.SaveState(ref regs);
                current.AsyncReturn = true;
                return 0;
            }
            else
            {
                var filename_len = ret;

                var completion = Arch.ArchFS.OpenAndGetSizeAsync(current, filenameBuf, flags, mode);
                if (completion == null)
                    return -ErrorCode.ENOMEM;

                Globals.CompletionQueue.Enqueue(completion);
                current.SaveState(ref regs);
                current.AsyncReturn = true;
                return 0;
            }

            if (fd > 0)
            {
                var file = new File(proc, inode, flags, mode);
                proc.InstallFd(fd, file);
            }

            if (SyscallProfiler.Enable)
            {
                var endTime = Arch.NativeMethods.l4api_get_system_clock();
                SyscallProfiler.AccountOpen((int)inode.kind, (long)(endTime - startTime));
            }

            return fd;
        }



        public static int Read(Thread current, ref Arch.ExceptionRegisters regs, int fd, UserPtr userBuf, int len)
        {
            if (len == 0)
                return 0;

            if (len < 0 || fd <= 0)
                return -ErrorCode.EINVAL;

            var proc = current.Parent;
            var file = proc.LookupFile(fd);
            if (file == null)
                return -ErrorCode.EBADF;

            var mode = file.flags & FileFlags.ReadWriteMask;
            if (mode == FileFlags.WriteOnly)
                return -ErrorCode.EPERM;

            int ret = file.Read(current, ref regs, userBuf, len);
            return ret;
        }

        public static int Pread64(Thread current, ref Arch.ExceptionRegisters regs, int fd, UserPtr userBuf, int len, uint offset)
        {
            if (len == 0)
                return 0;

            if (len < 0 || fd <= 0)
                return -ErrorCode.EINVAL;

            var proc = current.Parent;
            var file = proc.LookupFile(fd);
            if (file == null)
                return -ErrorCode.EINVAL;

            var mode = file.flags & FileFlags.ReadWriteMask;
            if (mode == FileFlags.WriteOnly)
                return -ErrorCode.EPERM;

            int ret = file.Read(current, ref regs, userBuf, len, offset);
            return ret;
        }

        public static int Write(Thread current, ref Arch.ExceptionRegisters regs, int fd, UserPtr userBuf, int len)
        {
            if (len == 0)
                return 0;

            if (len < 0)
                return -ErrorCode.EINVAL;

            var proc = current.Parent;
            var file = proc.LookupFile(fd);

            if (file == null)
                return -ErrorCode.EBADF;

            var mode = file.flags & FileFlags.ReadWriteMask;

            if (mode == FileFlags.ReadOnly)
                return -ErrorCode.EPERM;

            int ret = file.Write(current, ref regs, userBuf, len);
            return ret;
        }

        public static int Ioctl(Thread current, ref Arch.ExceptionRegisters regs, int fd, int arg0, int arg1)
        {
            Contract.Requires(current.Parent != null);

            var proc = current.Parent;
            var file = proc.LookupFile(fd);

            if (file == null)
            {
                Arch.Console.Write("Ioctl: invalid fd=");
                Arch.Console.Write(fd);
                Arch.Console.WriteLine();
                return -ErrorCode.EBADF;
            }

            return file.Ioctl(current, ref regs, arg0, arg1);
        }

        public static int Dup(Thread current, int old_fd)
        {
            var proc = current.Parent;
            var file = proc.LookupFile(old_fd);
            if (file == null)
                return -ErrorCode.EBADF;

            int new_fd = proc.GetUnusedFd();
            proc.InstallFd(new_fd, file);
            file.inode.IncreaseRefCount();

            return new_fd;
        }

        public static int Dup2(Thread current, int old_fd, int new_fd)
        {
            if (old_fd == new_fd)
                return new_fd;

            var proc = current.Parent;
            var file = proc.LookupFile(old_fd);
            if (file == null)
                return -ErrorCode.EBADF;

            var file2 = proc.LookupFile(new_fd);
            if (file2 == null)
                return -ErrorCode.EBADF;

            file2.Close();
            // post-condition of LookupFile
            Contract.Assume(proc.IsValidFd(new_fd));

            proc.UninstallFd(new_fd);

            proc.InstallFd(new_fd, file);

            return new_fd;
        }


        public static int FStat64(Thread current, int fd, UserPtr buf)
        {
            var proc = current.Parent;
            var file = proc.LookupFile(fd);
            if (file == null)
                return -ErrorCode.EINVAL;

            int ret = file.FStat64(current, buf);
            if (ret < 0)
                return ret;

            return 0;
        }

        public static int Close(Thread current, int fd)
        {
            var proc = current.Parent;
            var file = proc.LookupFile(fd);

            if (file == null)
                return -ErrorCode.EBADF;

            // post-condition of LookupFile
            Contract.Assume(proc.IsValidFd(fd));
            proc.UninstallFd(fd);

            int ret = file.Close();
            return ret;
        }


        public static int Lseek(Thread current, int fd, int offset, int origin)
        {
            var proc = current.Parent;
            var file = proc.LookupFile(fd);
            if (file == null)
                return -ErrorCode.EINVAL;

            int ret = file.Lseek(current, offset, origin);
            return ret;
        }

        public static int Access(Thread current, ref Arch.ExceptionRegisters regs, UserPtr filenamePtr, int mode)
        {
            // XXX: This is vfs related, now let's assume that we're dealing with pass through fs.

            var buf = Globals.AllocateAlignedCompletionBuffer(PATH_MAX);

            if (!buf.isValid)
                return -ErrorCode.ENOMEM;

            var ret = filenamePtr.ReadString(current, buf);

            var accessCompletion = new BridgeCompletion(current, buf);

            ret = Arch.IPCStubs.AccessAsync(current.Parent.helperPid, current.impl._value.thread._value, new Pointer(buf.Location), mode);

            if (ret < 0)
            {
                accessCompletion.Dispose();
                return ret;
            }

            Globals.CompletionQueue.Enqueue(accessCompletion);
            current.SaveState(ref regs);
            current.AsyncReturn = true;
            return 0;
        }

        private static int StatAt64(Thread current, UserPtr filenamePtr, UserPtr buf, bool followSymlink)
        {
            var proc = current.Parent;
            int err;
            filenamePtr.ReadString(current, Globals.LinuxIPCBuffer);
            if (followSymlink)
            {
                err = Arch.IPCStubs.linux_sys_stat64(proc.helperPid);
            }
            else
            {
                err = Arch.IPCStubs.linux_sys_lstat64(proc.helperPid);
            }
            if (err != 0)
                return err;

            if (buf.Write(current, new Pointer(Globals.LinuxIPCBuffer.Location), SIZE_OF_STAT64) != 0)
                return -ErrorCode.EFAULT;

            return 0;
        }

        public static int Stat64(Thread current, UserPtr filenamePtr, UserPtr buf)
        {
            return StatAt64(current, filenamePtr, buf, true);
        }

        public static int LStat64(Thread current, UserPtr filenamePtr, UserPtr buf)
        {
            return StatAt64(current, filenamePtr, buf, false);
        }

        public static int Pipe(Thread current, UserPtr pipeFd)
        {
            var proc = current.Parent;
            var helperPid = proc.helperPid;
            int fd0, fd1;

            var ret = Arch.IPCStubs.Pipe(helperPid, out fd0, out fd1);

            if (ret < 0)
                return ret;

            var inode1 = new Arch.ArchINode(fd0, 0, helperPid);
            var inode2 = new Arch.ArchINode(fd1, 0, helperPid);

            // XXX: are the permission settings correct?
            var file1 = new File(proc, inode1, FileFlags.ReadWrite, 0);

            var rfd0 = proc.GetUnusedFd();
            proc.InstallFd(rfd0, file1);

            var file2 = new File(proc, inode2, FileFlags.ReadWrite, 0);

            var rfd1 = proc.GetUnusedFd();
            proc.InstallFd(rfd1, file2);

            //Arch.Console.Write("pipe: linux_fd [");
            //Arch.Console.Write(fd0);
            //Arch.Console.Write(",");
            //Arch.Console.Write(fd1);
            //Arch.Console.Write("] => [");
            //Arch.Console.Write(rfd0);
            //Arch.Console.Write(",");
            //Arch.Console.Write(rfd1);
            //Arch.Console.Write("], ret=");
            //Arch.Console.Write(ret);
            //Arch.Console.WriteLine();

            if (pipeFd.Write(current, rfd0) != 0 || (pipeFd + sizeof(int)).Write(current, rfd1) != 0)
            {
                Arch.IPCStubs.Close(helperPid, fd0);
                Arch.IPCStubs.Close(helperPid, fd1);

                return -ErrorCode.EFAULT;
            }

            return ret;
        }


        public static int Getcwd(Thread current, UserPtr buf, int length)
        {
            buf.Write(current, CWDStr.GetByteString());
            return 0;
        }

        public static int Mkdir(Thread current, UserPtr pathname, int mode)
        {
            pathname.ReadString(current, Globals.LinuxIPCBuffer);
            return Arch.IPCStubs.Mkdir(current.Parent.helperPid, mode);
        }

        public static int Unlink(Thread current, UserPtr pathname)
        {
            pathname.ReadString(current, Globals.LinuxIPCBuffer);
            return Arch.IPCStubs.Unlink(current.Parent.helperPid);
        }

        public static int Fcntl64(Thread current, int fd, int cmd, int arg0)
        {
            if (fd <= 0)
                return -ErrorCode.EBADF;

            var proc = current.Parent;
            var file = proc.LookupFile(fd);
            if (file == null)
                return -ErrorCode.EBADF;

            if (file.inode.LinuxFd < 0)
                return -ErrorCode.EBADF;

            // Ignore locks
            if (cmd == F_GETLK || cmd == F_SETLK || cmd == F_SETLKW
                || cmd == F_GETFD || cmd == F_SETFD)
                return 0;

            var ret = Arch.IPCStubs.Fcntl64(current.Parent.helperPid, file.inode.LinuxFd, cmd, arg0);
            return ret;

        }

        public static int ftruncate(Thread current, int fd, int length)
        {
            if (fd <= 0)
                return -ErrorCode.EBADF;

            var proc = current.Parent;
            var file = proc.LookupFile(fd);
            if (file == null)
                return -ErrorCode.EBADF;

            if (file.inode.LinuxFd < 0)
                return -ErrorCode.EINVAL;

            var ret = file.Truncate(current, length);
            return ret;
        }

        public static bool StatIsDir(ByteBufferRef buf)
        {
            var mode = Deserializer.ReadInt(buf, OFFSET_OF_MODE_IN_STAT64);
            return ((mode & S_IFMT) & S_IFDIR) != 0;
        }

        public static long GetSizeFromStat64(ByteBufferRef buf)
        {
            return Deserializer.ReadLong(buf, OFFSET_OF_SIZE_IN_STAT64);
        }

        public static void SetSizeFromStat64(ByteBufferRef buf, ulong size)
        {
            Deserializer.WriteULong(size, buf, OFFSET_OF_SIZE_IN_STAT64);
        }

        internal static void HandleOpenFileCompletion(OpenFileCompletion c, int linux_fd, int size)
        {
            var current = c.thr;
            var proc = current.Parent;

            GenericINode inode = null;
            int ret = linux_fd;

            if (ret < 0)
            {
                c.Dispose();
                current.ReturnFromCompletion(ret);
                return;
            }

            switch (c.fileKind)
            {
                case GenericINode.INodeKind.ArchINodeKind:
                    inode = new Arch.ArchINode(linux_fd, (uint)size, proc.helperPid);
                    break;
                case GenericINode.INodeKind.SecureFSINodeKind:
                    inode = SecureFS.HandleOpenFileCompletion(c, linux_fd, size, ref ret);
                    break;
                default:
                    break;

            }

            if (inode != null)
            {
                var file = new File(proc, inode, c.flags, c.mode);
                ret = proc.GetUnusedFd();
                proc.InstallFd(ret, file);
            }

            c.Dispose();
            current.ReturnFromCompletion(ret);
            return;
        }
    }
}
