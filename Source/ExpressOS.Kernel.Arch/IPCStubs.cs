using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;

namespace ExpressOS.Kernel.Arch
{
    public static class IPCStubs
    {
        public enum IPCTag
        {
            EXPRESSOS_IPC = 2,
            EXPRESSOS_IPC_FLUSH_RET_QUEUE,
            EXPRESSOS_IPC_CMD,
        };

        private static Msgtag l4_ipc_send(L4Handle dest, Msgtag tag, Timeout timeout)
        {
            return NativeMethods.l4api_ipc_send(dest, NativeMethods.l4api_utcb(), tag, timeout);
        }

        private static Msgtag l4_stub_ipc_call(L4Handle dest, Msgtag tag, Timeout timeout)
        {
            return NativeMethods.l4api_ipc_call(dest, NativeMethods.l4api_utcb(), tag, timeout);
        }

        private static int l4_stub_ipc_error(Msgtag tag)
        {
            return NativeMethods.l4api_ipc_error(tag, NativeMethods.l4api_utcb());
        }

        public const int MAX_MR = 62;
        private const int SIZEOF_STAT64 = 96;

        public enum Type
        {
            EXPRESSOS_OP_TAKE_HELPER = 2,
            EXPRESSOS_OP_CLOCK_GETTIME,
            EXPRESSOS_OP_OPEN,
            EXPRESSOS_OP_CLOSE,
            EXPRESSOS_OP_VFS_READ,
            EXPRESSOS_OP_VFS_READ_ASYNC,
            EXPRESSOS_OP_VFS_WRITE_ASYNC,
            EXPRESSOS_OP_FSTAT_COMBINED,
            EXPRESSOS_OP_STAT_COMBINED,
            EXPRESSOS_OP_OPEN_AND_GET_SIZE_ASYNC,
            EXPRESSOS_OP_OPEN_AND_READ_PAGES_ASYNC,
            EXPRESSOS_OP_ACCESS_ASYNC,
            EXPRESSOS_OP_VFS_FTRUNCATE,
            EXPRESSOS_OP_PIPE,
            EXPRESSOS_OP_MKDIR,
            EXPRESSOS_OP_UNLINK,
            EXPRESSOS_OP_ASHMEM_IOCTL,
            EXPRESSOS_OP_VFS_LINUX_IOCTL,
            EXPRESSOS_OP_FCNTL64,
            EXPRESSOS_OP_SFS_FLUSH_PAGES_ASYNC,
            EXPRESSOS_OP_SOCKET_ASYNC,
            EXPRESSOS_OP_SETSOCKOPT_ASYNC,
            EXPRESSOS_OP_GETSOCKOPT_ASYNC,
            EXPRESSOS_OP_BIND_ASYNC,
            EXPRESSOS_OP_CONNECT_ASYNC,
            EXPRESSOS_OP_GETSOCKNAME_ASYNC,
            EXPRESSOS_OP_POLL,
            EXPRESSOS_OP_SENDTO,
            EXPRESSOS_OP_RECVFROM,
            EXPRESSOS_OP_SHUTDOWN,
            EXPRESSOS_OP_FUTEX_WAIT,
            EXPRESSOS_OP_FUTEX_WAKE,
            EXPRESSOS_OP_GET_USER_PAGE,
            EXPRESSOS_OP_ALIEN_MMAP2,
            EXPRESSOS_OP_FREE_LINUX_PAGE,
            EXPRESSOS_OP_BINDER_WRITE_READ,
            EXPRESSOS_OP_WRITE_APP_INFO,
            EXPRESSOS_OP_CONSOLE_WRITE,
            EXPRESSOS_OP_DOWNCALL_COUNT,
        };

        enum StatType
        {
            EXPRESSOS_OP_STAT_TYPE_STAT,
            EXPRESSOS_OP_STAT_TYPE_NEWSTAT,
            EXPRESSOS_OP_STAT_TYPE_STAT64,
            EXPRESSOS_OP_STAT_TYPE_LSTAT64,
        };

        private static unsafe void SetMR(int idx, int val)
        {
            Contract.Requires(idx >= 0 && idx <= MAX_MR);
            *(&(NativeMethods.l4api_utcb_mr()->mr0) + idx) = val;
        }

        private static void SetMR(int idx, uint val)
        {
            Contract.Requires(idx >= 0 && idx <= MAX_MR);
            SetMR(idx, (int)val);
        }

        private static unsafe int GetMR(int idx)
        {
            Contract.Requires(idx >= 0 && idx <= MAX_MR);
            return *(&(NativeMethods.l4api_utcb_mr()->mr0) + idx);
        }

        private static int RelativeBufferPos(Pointer buf)
        {
            return buf - new Pointer(ArchGlobals.LinuxIPCBuffer.Location);
        }

        #region Filesystem IPC stubs
        public static int Open(int helper_pid, int flags, int mode)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_OPEN);
            SetMR(1, helper_pid);
            SetMR(2, flags);
            SetMR(3, mode);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 4, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : GetMR(1);
        }

        public static int Close(int helper_pid, int fd)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_CLOSE);
            SetMR(1, helper_pid);
            SetMR(2, fd);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 3, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : GetMR(1);
        }

        public static int OpenAndGetSizeAsync(int helper_pid, uint handle, Pointer buf, int flags, int mode)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_OPEN_AND_GET_SIZE_ASYNC);
            SetMR(1, helper_pid);
            SetMR(2, handle);
            SetMR(3, RelativeBufferPos(buf));
            SetMR(4, flags);
            SetMR(5, mode);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 6, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }

        public static int OpenAndReadPagesAsync(int helper_pid, uint handle, Pointer buf, int npages, int flags, int mode)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_OPEN_AND_READ_PAGES_ASYNC);
            SetMR(1, helper_pid);
            SetMR(2, handle);
            SetMR(3, RelativeBufferPos(buf));
            SetMR(4, npages);
            SetMR(5, flags);
            SetMR(6, mode);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 7, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }

        public static int Read(int helper_pid, int fd, Pointer buf, int count, ref uint pos)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_VFS_READ);
            SetMR(1, helper_pid);
            SetMR(2, fd);
            SetMR(3, count);
            SetMR(4, pos);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 5, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);

            if (l4_stub_ipc_error(res) != 0)
                return -1;

            var readBytes = GetMR(1);
            var new_pos = GetMR(2);

            if (readBytes > ArchGlobals.LinuxIPCBuffer.Length)
                return -1;

            var src = ArchGlobals.LinuxIPCBuffer.Slice(0, readBytes);
            var b = new ByteBufferRef(buf.ToIntPtr(), count);
            b.CopyFrom(0, src);
            pos = (uint)new_pos;
            return readBytes;
        }

        public static int ReadAsync(int helper_pid, uint handle, Pointer buf, int fd, int count, uint pos)
        {
            return ReadWriteAsync((int)Type.EXPRESSOS_OP_VFS_READ_ASYNC, helper_pid, handle, buf, fd, count, pos);
        }

        public static int WriteAsync(int helper_pid, uint handle, Pointer buf, int fd, int count, uint pos)
        {
            return ReadWriteAsync((int)Type.EXPRESSOS_OP_VFS_WRITE_ASYNC, helper_pid, handle, buf, fd, count, pos);
        }

        public static int Ftruncate(int helper_pid, int fd, int length)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_VFS_FTRUNCATE);
            SetMR(1, helper_pid);
            SetMR(2, fd);
            SetMR(3, length);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 4, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : GetMR(1);
        }

        public static int Fcntl64(int helper_pid, int fd, int cmd, int arg0)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_FCNTL64);
            SetMR(1, helper_pid);
            SetMR(2, fd);
            SetMR(3, cmd);
            SetMR(4, arg0);


            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 5, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : GetMR(1);
        }

        public static int AccessAsync(int helper_pid, uint handle, Pointer filename, int mode)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_ACCESS_ASYNC);
            SetMR(1, helper_pid);
            SetMR(2, handle);
            SetMR(3, RelativeBufferPos(filename));
            SetMR(4, mode);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 5, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }

        public static int Pipe(int helper_pid, out int read_pipe, out int write_pipe)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_PIPE);
            SetMR(1, helper_pid);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 2, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            var ret = GetMR(1);

            if (l4_stub_ipc_error(res) != 0 || ret < 0)
            {
                read_pipe = 0;
                write_pipe = 0;
                return -1;
            }
            else
            {
                read_pipe = GetMR(2);
                write_pipe = GetMR(3);
            }
            return ret;
        }

        public static int Mkdir(int helper_pid, int mode)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_MKDIR);
            SetMR(1, helper_pid);
            SetMR(2, mode);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 3, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : GetMR(1);
        }

        public static int Unlink(int helper_pid)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_UNLINK);
            SetMR(1, helper_pid);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 2, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : GetMR(1);
        }

        public static int linux_sys_fstat64(int helper_pid, int fd)
        {
            return FStatCombined((int)StatType.EXPRESSOS_OP_STAT_TYPE_STAT64, helper_pid, fd, SIZEOF_STAT64);
        }

        public static int linux_sys_lstat64(int helper_pid)
        {
            return StatCombined((int)StatType.EXPRESSOS_OP_STAT_TYPE_LSTAT64, helper_pid, SIZEOF_STAT64);
        }

        public static int linux_sys_stat64(int helper_pid)
        {
            return StatCombined((int)StatType.EXPRESSOS_OP_STAT_TYPE_STAT64, helper_pid, SIZEOF_STAT64);
        }

        public static int linux_sys_vfs_ashmem_ioctl(int helper_pid, int fd, uint cmd, int arg0)
        {
            return IoctlImpl((int)Type.EXPRESSOS_OP_ASHMEM_IOCTL, helper_pid, fd, cmd, arg0);
        }

        public static int linux_sys_vfs_linux_ioctl(int helper_pid, int fd, uint cmd, int arg0)
        {
            return IoctlImpl((int)Type.EXPRESSOS_OP_VFS_LINUX_IOCTL, helper_pid, fd, cmd, arg0);
        }

        private static int IoctlImpl(int type, int helper_pid, int fd, uint cmd, int arg0)
        {
            SetMR(0, type);
            SetMR(1, helper_pid);
            SetMR(2, fd);
            SetMR(3, cmd);
            SetMR(4, arg0);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 5, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : GetMR(1);
        }
        
        private static int FStatCombined(int type, int helper_pid, int fd, uint length)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_FSTAT_COMBINED);
            SetMR(1, helper_pid);
            SetMR(2, type);
            SetMR(3, fd);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 4, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);

            if (l4_stub_ipc_error(res) != 0)
                return -1;

            var stat_size = GetMR(2);

            /* API mismatch here */
            if (stat_size != length)
                return -1;

            var ret = GetMR(1);
            return ret;
        }

        private static int StatCombined(int type, int helper_pid, uint length)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_STAT_COMBINED);
            SetMR(1, helper_pid);
            SetMR(2, type);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 3, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);

            if (l4_stub_ipc_error(res) != 0)
                return -1;

            var stat_size = GetMR(2);

            /* API mismatch here */
            if (stat_size != length)
                return -1;

            var ret = GetMR(1);
            return ret;
        }

        private static int ReadWriteAsync(int op, int helper_pid, uint handle, Pointer buf, int fd, int count, uint pos)
        {
            SetMR(0, op);
            SetMR(1, helper_pid);
            SetMR(2, handle);
            SetMR(3, RelativeBufferPos(buf));
            SetMR(4, fd);
            SetMR(5, count);
            SetMR(6, pos);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 7, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }

        #endregion

        #region Network IPC stubs
        public static int SocketAsync(int helper_pid, uint handle, int domain, int type, int protocol)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_SOCKET_ASYNC);
            SetMR(1, helper_pid);
            SetMR(2, handle);
            SetMR(3, domain);
            SetMR(4, type);
            SetMR(5, protocol);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 6, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }

        public static int SetSockoptAsync(int helper_pid, uint handle, Pointer buf, int sockfd, int level, int optname, int optlen)
        {
            return SockoptAsync((int)Type.EXPRESSOS_OP_SETSOCKOPT_ASYNC, helper_pid, handle, buf, sockfd, level, optname, optlen);
        }

        public static int GetSockoptAsync(int helper_pid, uint handle, Pointer buf, int sockfd, int level, int optname, int optlen)
        {
            return SockoptAsync((int)Type.EXPRESSOS_OP_GETSOCKOPT_ASYNC, helper_pid, handle, buf, sockfd, level, optname, optlen);
        }

        public static int BindAsync(int helper_pid, uint handle, Pointer buf, int sockfd, int addrlen)
        {
            return BindOrConnectAsync((int)Type.EXPRESSOS_OP_BIND_ASYNC, helper_pid, handle, buf, sockfd, addrlen);
        }

        public static int ConnectAsync(int helper_pid, uint handle, Pointer buf, int sockfd, int addrlen)
        {
            return BindOrConnectAsync((int)Type.EXPRESSOS_OP_CONNECT_ASYNC, helper_pid, handle, buf, sockfd, addrlen);
        }

        public static int GetsockNameAsync(int helper_pid, uint handle, Pointer buf, int sockfd, int addrlen)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_GETSOCKNAME_ASYNC);
            SetMR(1, helper_pid);
            SetMR(2, handle);
            SetMR(3, RelativeBufferPos(buf));
            SetMR(4, sockfd);
            SetMR(5, addrlen);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 6, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }

        public static int PollAsync(int helper_pid, uint handle, Pointer fds, int nfds, int timeout)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_POLL);
            SetMR(1, helper_pid);
            SetMR(2, handle);
            SetMR(3, RelativeBufferPos(fds));
            SetMR(4, nfds);
            SetMR(5, timeout);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 6, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }

        public static int Sendto(int helper_pid, int sockfd, int len, int flags, int addrlen)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_SENDTO);
            SetMR(1, helper_pid);
            SetMR(2, sockfd);
            SetMR(3, len);
            SetMR(4, flags);
            SetMR(5, addrlen);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 6, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);

            if (l4_stub_ipc_error(res) != 0)
                return -1;

            return GetMR(1);
        }

        public static int Recvfrom(int helper_pid, int sockfd, int len, int flags, ref int addrlen)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_RECVFROM);
            SetMR(1, helper_pid);
            SetMR(2, sockfd);
            SetMR(3, len);
            SetMR(4, flags);
            SetMR(5, addrlen);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 6, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);

            if (l4_stub_ipc_error(res) != 0)
                return -1;

            addrlen = GetMR(2);
            return GetMR(1);
        }

        public static int Shutdown(int helper_pid, int sockfd, int how)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_SHUTDOWN);
            SetMR(1, helper_pid);
            SetMR(2, sockfd);
            SetMR(3, how);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 4, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }

        private static int SockoptAsync(int type, int helper_pid, uint handle, Pointer buf, int sockfd, int level, int optname, int optlen)
        {
            SetMR(0, type);
            SetMR(1, helper_pid);
            SetMR(2, handle);
            SetMR(3, RelativeBufferPos(buf));
            SetMR(4, sockfd);
            SetMR(5, level);
            SetMR(6, optname);
            SetMR(7, optlen);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 8, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }

        private static int BindOrConnectAsync(int type, int helper_pid, uint handle, Pointer buf, int sockfd, int addrlen)
        {
            SetMR(0, type);
            SetMR(1, helper_pid);
            SetMR(2, handle);
            SetMR(3, RelativeBufferPos(buf));
            SetMR(4, sockfd);
            SetMR(5, addrlen);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 6, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }

        #endregion

        #region MM IPC stubs

        public static int linux_sys_free_linux_pages(Pointer[] addresses)
        {
            Contract.Requires(addresses.Length + 2 <= MAX_MR);
            
            var len = addresses.Length;

            SetMR(0, (int)Type.EXPRESSOS_OP_FREE_LINUX_PAGE);
            SetMR(1, len);

            for (var i = 0; i < len; ++i)
                Deserializer.WriteInt(addresses[i] - ArchGlobals.LinuxMainMemoryStart, ArchGlobals.LinuxIPCBuffer, i * sizeof(int));

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 2, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }

        public static Pointer linux_sys_get_user_page(int helper_pid, uint faultType, Pointer shadowAddress)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_GET_USER_PAGE);
            SetMR(1, helper_pid);
            SetMR(2, faultType & L4FPage.L4_FPAGE_FAULT_WRITE);
            SetMR(3, shadowAddress.ToUInt32());

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 4, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            
            if (l4_stub_ipc_error(res) != 0)
                return Pointer.Zero;

            var relative_pos = GetMR(1);
            ArchDefinition.Assert(relative_pos < ArchGlobals.LinuxMainMemorySize);           
            return ArchGlobals.LinuxMainMemoryStart + relative_pos;
        }

        public static uint linux_sys_alien_mmap2(int helper_pid, Pointer addr, int length, int prot, int flags, int fd, int pgoffset)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_ALIEN_MMAP2);
            SetMR(1, helper_pid);
            SetMR(2, addr.ToUInt32());
            SetMR(3, length);
            SetMR(4, prot);
            SetMR(5, flags);
            SetMR(6, fd);
            SetMR(7, pgoffset);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 8, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? 0xffffffffU : (uint)GetMR(1);
        }

        #endregion

        #region Futex IPC stubs
        public static int linux_sys_futex_wait(int helper_pid, uint handle, int op, Pointer shadowAddr, int val, timespec ts, uint bitset)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_FUTEX_WAIT);
            SetMR(1, helper_pid);
            SetMR(2, handle);
            SetMR(3, op);
            SetMR(4, shadowAddr.ToUInt32());
            SetMR(5, val);
            SetMR(6, ts.tv_sec);
            SetMR(7, ts.tv_nsec);
            SetMR(8, bitset);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 9, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }

        public static int linux_sys_futex_wake(int helper_pid, uint handle, int op, Pointer shadowAddr, uint bitset)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_FUTEX_WAKE);
            SetMR(1, helper_pid);
            SetMR(2, handle);
            SetMR(3, op);
            SetMR(4, shadowAddr.ToUInt32());
            SetMR(5, bitset);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 6, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }

        #endregion

        #region Binder IPC stubs
        public static int linux_sys_binder_write_read_async(int helper_pid, uint handle, sys_binder_write_desc desc)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_BINDER_WRITE_READ);
            SetMR(1, helper_pid);
            SetMR(2, handle);
            SetMR(3, RelativeBufferPos(desc.write_buffer));
            SetMR(4, desc.buffer_size);
            SetMR(5, desc.bwr_write_size);
            SetMR(6, desc.patch_table_entries);
            SetMR(7, desc.patch_table_offset);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 8, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }

        public static int linux_sys_take_helper(out uint shadowBinderVMStart, out int workspace_fd, out uint workspace_size)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_TAKE_HELPER);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 1, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);

            if (l4_stub_ipc_error(res) != 0)
            {
                shadowBinderVMStart = 0;
                workspace_fd = 0;
                workspace_size = 0;
                return -1;
            }
            else
            {
                var pid = GetMR(1);
                shadowBinderVMStart = (uint)GetMR(2);
                workspace_fd = GetMR(3);
                workspace_size = (uint)GetMR(4);
                return pid;
            }
        }
        #endregion

        #region Misc IPC stub

        public static int linux_sys_clock_gettime(int clk_id)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_CLOCK_GETTIME);
            SetMR(1, clk_id);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 2, 0, 0);
            var res = l4_stub_ipc_call(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : GetMR(1);
        }

        public static int ScatterWritePageAsync(int helper_pid, uint handle, Pointer buf, int fd, int page_count)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_SFS_FLUSH_PAGES_ASYNC);
            SetMR(1, helper_pid);
            SetMR(2, handle);
            SetMR(3, fd);
            SetMR(4, page_count);
            SetMR(5, RelativeBufferPos(buf));

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 6, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0;
        }
        #endregion

        public static int WriteAppInfo(int helper_pid, int length)
        {
            SetMR(0, (int)Type.EXPRESSOS_OP_WRITE_APP_INFO);
            SetMR(1, helper_pid);
            SetMR(2, length);

            var tag = new Msgtag((int)IPCTag.EXPRESSOS_IPC, 3, 0, 0);
            var res = l4_ipc_send(ArchGlobals.LinuxServerTid, tag, Timeout.Never);
            return l4_stub_ipc_error(res) != 0 ? -1 : 0; 
        }
    }
}
