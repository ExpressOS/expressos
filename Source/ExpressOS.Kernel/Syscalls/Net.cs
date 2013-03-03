using System;
using System.Diagnostics.Contracts;
using ExpressOS.Kernel.Arch;

namespace ExpressOS.Kernel
{
    public static class Net
    {
        public const int POLLIN = 0x1;
        public const int POLLOUT = 0x4;
        public const int POLLERR = 0x8;
        public const int POLLHUP = 0x10;

        public const int SYS_SOCKET = 1;      /* sys_socket(2)                */
        public const int SYS_BIND = 2;        /* sys_bind(2)                  */
        public const int SYS_CONNECT = 3;     /* sys_connect(2)               */
        public const int SYS_LISTEN = 4;      /* sys_listen(2)                */
        public const int SYS_ACCEPT = 5;      /* sys_accept(2)                */
        public const int SYS_GETSOCKNAME = 6; /* sys_getsockname(2)           */
        public const int SYS_GETPEERNAME = 7; /* sys_getpeername(2)           */
        public const int SYS_SOCKETPAIR = 8;  /* sys_socketpair(2)            */
        public const int SYS_SEND = 9;        /* sys_send(2)                  */
        public const int SYS_RECV = 10;       /* sys_recv(2)                  */
        public const int SYS_SENDTO = 11;     /* sys_sendto(2)                */
        public const int SYS_RECVFROM = 12;   /* sys_recvfrom(2)              */
        public const int SYS_SHUTDOWN = 13;   /* sys_shutdown(2)              */
        public const int SYS_SETSOCKOPT = 14; /* sys_setsockopt(2)            */
        public const int SYS_GETSOCKOPT = 15; /* sys_getsockopt(2)            */
        public const int SYS_SENDMSG = 16;    /* sys_sendmsg(2)               */
        public const int SYS_RECVMSG = 17;    /* sys_recvmsg(2)               */
        public const int SYS_ACCEPT4 = 18;    /* sys_accept4(2)               */
        public const int SYS_RECVMMSG = 19;   /* sys_recvmmsg(2)              */
        public const int SYS_SENDMMSG = 20;   /* sys_sendmmsg(2)              */

        public static int Poll(Thread current, ref Arch.ExceptionRegisters regs, UserPtr fds, int nfds, int timeout)
        {
            if (nfds < 0)
                return -ErrorCode.EINVAL;

            var pollfd_size = pollfd.Size * nfds;

            var buf = Globals.AllocateAlignedCompletionBuffer(pollfd_size);
            if (!buf.isValid)
                return -ErrorCode.ENOMEM;

            var poll_entry = new PollCompletion(current, fds, nfds, buf);
        
            if (fds.Read(current, buf, pollfd_size) != 0)
            {
                poll_entry.Dispose();
                return -ErrorCode.EFAULT;
            }

            Contract.Assert(buf.Length >= pollfd_size);
            if (!TranslateToLinuxFd(current, poll_entry, buf))
            {
                poll_entry.Dispose();
                return -ErrorCode.EBADF;
            }

            var ret = Arch.IPCStubs.PollAsync(current.Parent.helperPid, current.impl._value.thread._value, new Pointer(buf.Location), nfds, timeout);

            if (ret < 0)
            {
                poll_entry.Dispose();
                return ret;
            }

            Globals.CompletionQueue.Enqueue(poll_entry);
            current.SaveState(ref regs);
            current.AsyncReturn = true;
            return 0;
        }

        private static bool TranslateToLinuxFd(Thread current, PollCompletion poll_entry, ByteBufferRef b)
        {
            Contract.Requires(b.Length >= poll_entry.fdMaps.Length * pollfd.Size);
            var proc = current.Parent;
            var nfds = poll_entry.fdMaps.Length;

            for (var i = 0; i < nfds; ++i)
            {
                Contract.Assert(i < nfds && nfds * pollfd.Size <= b.Length);
                Contract.Assert(i + 1 <= nfds && (i + 1) * pollfd.Size <= nfds * pollfd.Size);
                Contract.Assert((i + 1) * pollfd.Size <= nfds * pollfd.Size && nfds * pollfd.Size <= b.Length);
                var poll_struct = pollfd.Deserialize(b, i * pollfd.Size);
                var file = proc.LookupFile(poll_struct.fd);
                if (file == null)
                    return false;

                // DEBUG
                if (file.inode.LinuxFd < 0)
                {
                    Arch.Console.Write("Poll: transalte fd linux fd < 0 for fd=");
                    Arch.Console.Write(poll_struct.fd);
                    Arch.Console.Write(" kind=");
                    Arch.Console.Write((int)file.inode.kind);
                    Arch.Console.WriteLine();
                }

                poll_entry.fdMaps[i] = poll_struct.fd;
                poll_struct.fd = file.inode.LinuxFd;
                poll_struct.Write(b, i * pollfd.Size);
            }
            return true;
        }

        public static int Select(Thread current, ref Arch.ExceptionRegisters regs, int maxfds, UserPtr inp, UserPtr outp, UserPtr exp, UserPtr tvp)
        {
            var helper = new SelectHelper();
            int ret = 0;

            ret = helper.AddUserFdList(current, inp, maxfds, POLLIN);
            if (ret < 0)
                return ret;

            ret = helper.AddUserFdList(current, outp, maxfds, POLLOUT);
            if (ret < 0)
                return ret;

            ret = helper.AddUserFdList(current, exp, maxfds, POLLERR);
            if (ret < 0)
                return ret;

            int timeout = 0;
            timeval tv;
            if (tvp == UserPtr.Zero)
            {
                timeout = -1;
            }
            else if (tvp.Read(current, out tv) != 0)
            {
                return -ErrorCode.EFAULT;
            }
            else
            {
                timeout = (int)(tv.tv_sec * 1000 + tv.tv_usec / 1000);
            }

            var nfds = helper.TotalFds;
            var pollfd_size = pollfd.Size * nfds;
            var buf = Globals.AllocateAlignedCompletionBuffer(pollfd_size);
            if (!buf.isValid)
                return -ErrorCode.ENOMEM;

            helper.WritePollFds(buf);

            var select_entry = new SelectCompletion(current, maxfds, inp, outp, exp, helper, buf);
            ret = Arch.IPCStubs.PollAsync(current.Parent.helperPid, current.impl._value.thread._value, new Pointer(buf.Location), nfds, timeout);

            if (ret < 0)
            {
                select_entry.Dispose();
                return ret;
            }

            Globals.CompletionQueue.Enqueue(select_entry);
            current.SaveState(ref regs);
            current.AsyncReturn = true;
            return 0;
        }

        public static void HandlePollAsync(PollCompletion entry, int ret)
        {
            var current = entry.thr;
            var proc = current.Parent;
            if (ret <= 0)
            {
                entry.Dispose();
                current.ReturnFromCompletion(ret);
                return;
            }

            var b = entry.buf;

            if (b.Length < ret * pollfd.Size)
            {
                ret = -ErrorCode.ENOMEM;
                entry.Dispose();
                current.ReturnFromCompletion(ret);
                Utils.Panic();
                return;
            }

            // Translate fds back into fd in ExpressOS
            for (var i = 0; i < ret; ++i)
            {
                Contract.Assert(i < ret && ret * pollfd.Size <= b.Length);
                Contract.Assert(i + 1 <= ret && (i + 1) * pollfd.Size <= ret * pollfd.Size);
                Contract.Assert((i + 1) * pollfd.Size <= ret * pollfd.Size && ret * pollfd.Size <= b.Length);
                var poll_struct = pollfd.Deserialize(b, i * pollfd.Size);
                var expressos_fd = -1;
                for (var j = 0; j < entry.fdMaps.Length; ++j)
                {
                    var file = proc.LookupFile(entry.fdMaps[j]);
                    if (file == null)
                        continue;

                    if (file.inode.LinuxFd == poll_struct.fd)
                    {
                        expressos_fd = entry.fdMaps[j];
                        break;
                    }
                }

                if (expressos_fd == -1)
                {
                    // DEBUG
                    Arch.Console.Write("poll_async: cannot find ExpressOS fd for fd ");
                    Arch.Console.Write(poll_struct.fd);
                    Arch.Console.Write(" i=");
                    Arch.Console.Write(i);
                    Arch.Console.Write(" poll_ret=");
                    Arch.Console.Write(ret);
                    Arch.Console.Write(" nfds=");
                    Arch.Console.Write(entry.fdMaps.Length);
                    Arch.Console.WriteLine();
                    ret = -ErrorCode.EBADF;
                    break;
                }

                // Write result back
                var p_user_entry = entry.userFdBuf + i * pollfd.Size;
                p_user_entry.Write(current, expressos_fd);
                (p_user_entry + pollfd.OFFSET_OF_REVENTS).Write(current, poll_struct.revents);
            }

            entry.Dispose();
            current.ReturnFromCompletion(ret);
        }

        public static int socketcall(Thread current, ref Arch.ExceptionRegisters regs, int call, UserPtr argPtr)
        {
            int err = 0;
            int a0, a1, a2, a3, a4, a5;

            SyscallProfiler.EnterSocketcall(call);
            switch (call)
            {
                case SYS_SOCKET:
                    if (argPtr.Read(current, out a0) != 0 || (argPtr + sizeof(int)).Read(current, out a1) != 0
                        || (argPtr + sizeof(int) * 2).Read(current, out a2) != 0)
                        return -ErrorCode.EFAULT;

                    err = Socket(current, ref regs, a0, a1, a2);
                    break;

                case SYS_BIND:
                    if (argPtr.Read(current, out a0) != 0 || (argPtr + sizeof(int)).Read(current, out a1) != 0
                        || (argPtr + sizeof(int) * 2).Read(current, out a2) != 0)
                        return -ErrorCode.EFAULT;

                    err = Bind(current, ref regs, a0, new UserPtr(a1), a2);
                    break;

                case SYS_CONNECT:
                    if (argPtr.Read(current, out a0) != 0 || (argPtr + sizeof(int)).Read(current, out a1) != 0
                        || (argPtr + sizeof(int) * 2).Read(current, out a2) != 0)
                        return -ErrorCode.EFAULT;

                    err = Connect(current, ref regs, a0, new UserPtr(a1), a2);
                    break;

                case SYS_GETSOCKNAME:
                    if (argPtr.Read(current, out a0) != 0 || (argPtr + sizeof(int)).Read(current, out a1) != 0
                        || (argPtr + sizeof(int) * 2).Read(current, out a2) != 0)
                        return -ErrorCode.EFAULT;

                    err = Getsockname(current, ref regs, a0, new UserPtr(a1), new UserPtr(a2));
                    break;

                case SYS_SENDTO:
                    if (argPtr.Read(current, out a0) != 0 || (argPtr + sizeof(int)).Read(current, out a1) != 0
                        || (argPtr + sizeof(int) * 2).Read(current, out a2) != 0 || (argPtr + sizeof(int) * 3).Read(current, out a3) != 0
                        || (argPtr + sizeof(int) * 4).Read(current, out a4) != 0 || (argPtr + sizeof(int) * 5).Read(current, out a5) != 0)
                        return -ErrorCode.EFAULT;

                    err = Sendto(current, a0, new UserPtr(a1), a2, a3, new UserPtr(a4), a5);
                    break;

                case SYS_RECVFROM:
                    if (argPtr.Read(current, out a0) != 0 || (argPtr + sizeof(int)).Read(current, out a1) != 0
                        || (argPtr + sizeof(int) * 2).Read(current, out a2) != 0 || (argPtr + sizeof(int) * 3).Read(current, out a3) != 0
                        || (argPtr + sizeof(int) * 4).Read(current, out a4) != 0 || (argPtr + sizeof(int) * 5).Read(current, out a5) != 0)
                        return -ErrorCode.EFAULT;

                    err = RecvFrom(current, a0, new UserPtr(a1), a2, a3, new UserPtr(a4), new UserPtr(a5));
                    break;

                case SYS_SHUTDOWN:
                    if (argPtr.Read(current, out a0) != 0 || (argPtr + sizeof(int)).Read(current, out a1) != 0)
                        return -ErrorCode.EFAULT;

                    err = Shutdown(current, a0, a1);
                    break;

                case SYS_SETSOCKOPT:
                    if (argPtr.Read(current, out a0) != 0 || (argPtr + sizeof(int)).Read(current, out a1) != 0
                        || (argPtr + sizeof(int) * 2).Read(current, out a2) != 0 || (argPtr + sizeof(int) * 3).Read(current, out a3) != 0
                        || (argPtr + sizeof(int) * 4).Read(current, out a4) != 0)
                        return -ErrorCode.EFAULT;

                    err = Setsockopt(current, ref regs, a0, a1, a2, new UserPtr(a3), a4);
                    break;

                case SYS_GETSOCKOPT:
                    if (argPtr.Read(current, out a0) != 0 || (argPtr + sizeof(int)).Read(current, out a1) != 0
                      || (argPtr + sizeof(int) * 2).Read(current, out a2) != 0 || (argPtr + sizeof(int) * 3).Read(current, out a3) != 0
                      || (argPtr + sizeof(int) * 4).Read(current, out a4) != 0)
                        return -ErrorCode.EFAULT;

                    err = Getsockopt(current, ref regs, a0, a1, a2, new UserPtr(a3), new UserPtr(a4));
                    break;

                default:
                    Arch.Console.Write("Unimplemented socketcall ");
                    Arch.Console.Write(call);
                    Arch.Console.WriteLine();
                    err = -1;
                    break;
            }
            SyscallProfiler.ExitSocketcall(call);

            return err;
        }

        private static int Shutdown(Thread current, int sockfd, int how)
        {
            var proc = current.Parent;
            var file = proc.LookupFile(sockfd);
            if (file == null)
                return -ErrorCode.EBADF;

            if (file.inode.kind != GenericINode.INodeKind.SocketINodeKind)
                return -ErrorCode.ENOTSOCK;

            var ret = Arch.IPCStubs.Shutdown(proc.helperPid, sockfd, how);
            return ret;
        }

        private static int Getsockname(Thread current, ref Arch.ExceptionRegisters regs, int sockfd, UserPtr sockaddr, UserPtr p_addrlen)
        {
            var proc = current.Parent;
            var file = proc.LookupFile(sockfd);
            if (file == null)
                return -ErrorCode.EBADF;

            if (file.inode.kind != GenericINode.INodeKind.SocketINodeKind)
                return -ErrorCode.ENOTSOCK;

            int addrlen;
            if (p_addrlen.Read(current, out addrlen) != 0)
                return -ErrorCode.EFAULT;

            if (addrlen > Globals.LinuxIPCBuffer.Length)
                return -ErrorCode.EINVAL;

            var buf = Globals.AllocateAlignedCompletionBuffer((int)addrlen);

            if (!buf.isValid)
                return -ErrorCode.ENOMEM;

            var completion = new GetSockParamCompletion(current, sockaddr, p_addrlen, buf);
          
            Arch.IPCStubs.GetsockNameAsync(proc.helperPid, current.impl._value.thread._value, new Pointer(buf.Location), file.inode.LinuxFd, addrlen);

            Globals.CompletionQueue.Enqueue(completion);
            current.SaveState(ref regs);
            current.AsyncReturn = true;
            return 0;
        }

        private static int RecvFrom(Thread current, int sockfd, UserPtr userBuf, int len, int flags, UserPtr sockaddr, UserPtr p_addrlen)
        {
            var proc = current.Parent;
            var file = proc.LookupFile(sockfd);
            if (file == null)
                return -ErrorCode.EBADF;

            int addrlen;
            if (p_addrlen.Read(current, out addrlen) != 0)
                return -ErrorCode.EFAULT;

            var buf = Globals.LinuxIPCBuffer;
            if (len + addrlen > buf.Length)
            {
                return -ErrorCode.ENOMEM;
            }

            var ret = Arch.IPCStubs.Recvfrom(proc.helperPid, file.inode.LinuxFd, len, flags, ref addrlen);
            if (ret < 0)
                return ret;

            var left = userBuf.Write(current, new Pointer(buf.Location), ret);

            if (sockaddr.Write(current, new Pointer(buf.Location + ret - left), addrlen) != 0)
                return -ErrorCode.EFAULT;

            return ret - left;
        }

        private static int Sendto(Thread current, int sockfd, UserPtr userBuf, int len, int flags, UserPtr sockaddr, int addrlen)
        {
            var proc = current.Parent;
            var file = proc.LookupFile(sockfd);
            if (file == null)
                return -ErrorCode.EBADF;

            var buf = Globals.LinuxIPCBuffer;

            if (len < 0 || addrlen < 0)
                return -ErrorCode.EINVAL;

            if (len + addrlen > buf.Length)
                return -ErrorCode.ENOMEM;

            if (sockaddr == UserPtr.Zero && addrlen != 0)
                return -ErrorCode.EINVAL;

            Contract.Assert(buf.Length >= len + addrlen);

            if (userBuf.Read(current, buf, len) != 0)
                return -ErrorCode.EFAULT;

            var old_buf_len = buf.Length;
            var sockaddr_buf = buf.Slice(len, buf.Length - len);

            // Post-condition of ByteBufferRef#ctor and slice
            Contract.Assume(buf.Length == old_buf_len);
            Contract.Assume(sockaddr_buf.Length == buf.Length - len);
       
            Contract.Assert(buf.Length - len >= addrlen);

            if (sockaddr != UserPtr.Zero && sockaddr.Read(current, sockaddr_buf, addrlen) != 0)
                return -ErrorCode.EFAULT;

            return Arch.IPCStubs.Sendto(proc.helperPid, file.inode.LinuxFd, len, flags, addrlen);
        }

        private static int Bind(Thread current, ref Arch.ExceptionRegisters regs, int sockfd, UserPtr sockaddr, int addrlen)
        {
            return BindOrConnect(SYS_BIND, current, ref regs, sockfd, sockaddr, addrlen);
        }

        private static int Connect(Thread current, ref Arch.ExceptionRegisters regs, int sockfd, UserPtr sockaddr, int addrlen)
        {
            return BindOrConnect(SYS_CONNECT, current, ref regs, sockfd, sockaddr, addrlen);
        }

        private static int BindOrConnect(int type, Thread current, ref Arch.ExceptionRegisters regs, int sockfd, UserPtr sockaddr, int addrlen)
        {
            Contract.Requires(type == SYS_BIND || type == SYS_CONNECT);

            var proc = current.Parent;
            var file = proc.LookupFile(sockfd);
            if (file == null)
                return -ErrorCode.EBADF;

            if (file.inode.kind != GenericINode.INodeKind.SocketINodeKind)
                return -ErrorCode.ENOTSOCK;

            if (addrlen > Globals.LinuxIPCBuffer.Length)
                return -ErrorCode.EINVAL;

            var buf = Globals.AllocateAlignedCompletionBuffer(addrlen);

            if (!buf.isValid)
                return -ErrorCode.ENOMEM;

            var completion = new BridgeCompletion(current, buf);
          
            if (sockaddr.Read(current, buf, addrlen) != 0)
            {
                completion.Dispose();
                return -ErrorCode.EFAULT;
            }

            if (type == SYS_BIND)
            {
                Arch.IPCStubs.BindAsync(proc.helperPid, current.impl._value.thread._value, new Pointer(buf.Location), file.inode.LinuxFd, addrlen);
            }
            else if (type == SYS_CONNECT)
            {
                Arch.IPCStubs.ConnectAsync(proc.helperPid, current.impl._value.thread._value, new Pointer(buf.Location), file.inode.LinuxFd, addrlen);
            }

            Globals.CompletionQueue.Enqueue(completion);
            current.SaveState(ref regs);
            current.AsyncReturn = true;
            return 0;
        }

        private static int Getsockopt(Thread current, ref Arch.ExceptionRegisters regs, int sockfd, int level, int optname, UserPtr optval, UserPtr p_optlen)
        {
            var proc = current.Parent;
            var file = proc.LookupFile(sockfd);
            if (file == null)
                return -ErrorCode.EBADF;

            if (file.inode.kind != GenericINode.INodeKind.SocketINodeKind)
                return -ErrorCode.ENOTSOCK;

            uint optlen = 0;
            if (p_optlen.Read(current, out optlen) != 0)
                return -ErrorCode.EFAULT;

            if (optlen > Globals.LinuxIPCBuffer.Length)
                return -ErrorCode.EINVAL;

            var buf = Globals.AllocateAlignedCompletionBuffer((int)optlen);

            if (!buf.isValid)
                return -ErrorCode.ENOMEM;

            var completion = new GetSockParamCompletion(current, optval, p_optlen, buf);
            
            Arch.IPCStubs.GetSockoptAsync(proc.helperPid, current.impl._value.thread._value, new Pointer(buf.Location), file.inode.LinuxFd, level, optname, (int)optlen);

            Globals.CompletionQueue.Enqueue(completion);
            current.SaveState(ref regs);
            current.AsyncReturn = true;

            return 0;
        }

        public static void HandleGetSockParamCompletion(GetSockParamCompletion c, int retval, int optlen)
        {
            var current = c.thr;

            if (retval >= 0 && (c.p_addrlen.Write(current, optlen) != 0
                || c.payload.Write(current, new Pointer(c.buf.Location), optlen) != 0))
                retval = -ErrorCode.EFAULT;
        
            c.Dispose();
            current.ReturnFromCompletion(retval);
        }

        private static int Setsockopt(Thread current, ref Arch.ExceptionRegisters regs, int sockfd, int level, int optname, UserPtr optval, int optlen)
        {
            var proc = current.Parent;
            var file = proc.LookupFile(sockfd);
            if (file == null)
                return -ErrorCode.EBADF;

            if (file.inode.kind != GenericINode.INodeKind.SocketINodeKind)
                return -ErrorCode.ENOTSOCK;

            if (optlen > Globals.LinuxIPCBuffer.Length)
                return -ErrorCode.EINVAL;

            var buf = Globals.AllocateAlignedCompletionBuffer(optlen);

            if (!buf.isValid)
                return -ErrorCode.ENOMEM;

            var completion = new BridgeCompletion(current, buf);
          
            if (optval.Read(current, buf, optlen) != 0)
            {
                completion.Dispose();
                return -ErrorCode.EFAULT;
            }

            Arch.IPCStubs.SetSockoptAsync(proc.helperPid, current.impl._value.thread._value, new Pointer(buf.Location), file.inode.LinuxFd, level, optname, optlen);

            Globals.CompletionQueue.Enqueue(completion);
            current.SaveState(ref regs);
            current.AsyncReturn = true;
            return 0;
        }

        private static int Socket(Thread current, ref Arch.ExceptionRegisters regs, int domain, int type, int protocol)
        {
            var proc = current.Parent;
            Arch.IPCStubs.SocketAsync(proc.helperPid, current.impl._value.thread._value, domain, type, protocol);

            var completion = new SocketCompletion(current);

            Globals.CompletionQueue.Enqueue(completion);
            current.SaveState(ref regs);
            current.AsyncReturn = true;
            return 0;
        }

        public static void HandleSocketCompletion(SocketCompletion c, int ret)
        {
            var current = c.thr;
            if (ret < 0)
            {
                current.ReturnFromCompletion(ret);
                return;
            }

            var proc = current.Parent;
            
            var inode = new SocketINode(ret, proc.helperPid);
            var file = new File(proc, inode, FileFlags.ReadWriteMask, 0);

            var fd = proc.GetUnusedFd();
            proc.InstallFd(fd, file);
            current.ReturnFromCompletion(fd);
        }

        public static void HandleSelectAsync(SelectCompletion entry, int retval)
        {
            var current = entry.thr;
            var b = entry.buf;

            if (retval <= 0 || retval * pollfd.Size >= b.Length)
            {
                entry.Dispose();
                current.ReturnFromCompletion(retval);
                return;
            }

            int ret = retval;

            var len = (entry.fds + 7) / 8;

            var bit_set = 0;
            var r = entry.helper.TranslateToUserFdlist(current, b, retval, entry.fds, entry.inp, POLLIN);
            if (r < 0)
                ret = r;

            if (ret >= 0)
            {
                bit_set += r;
                r = entry.helper.TranslateToUserFdlist(current, b, retval, entry.fds, entry.outp, POLLOUT);
                if (r < 0)
                    ret = r;
            }

            if (ret >= 0)
            {
                bit_set += r;
                r = entry.helper.TranslateToUserFdlist(current, b, retval, entry.fds, entry.exp, POLLERR | POLLHUP);
                if (r < 0)
                    ret = r;
            }

            if (ret >= 0)
                ret = bit_set;

            entry.Dispose();
            current.ReturnFromCompletion(ret);
        }
    }

    public sealed class PollCompletion : ThreadCompletionEntryWithBuffer
    {
        public readonly UserPtr userFdBuf;
        public readonly int[] fdMaps;

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(buf.Length >= fdMaps.Length * pollfd.Size);
        }

        public PollCompletion(Thread current, UserPtr fds, int nfds, ByteBufferRef buf)
            : base(current, Kind.PollCompletionKind, buf)
        {
            Contract.Requires(nfds >= 0);
            Contract.Requires(buf.Length >= nfds * pollfd.Size);
            Contract.Ensures(fdMaps.Length == nfds);
            Contract.Ensures(this.buf.Length >= fdMaps.Length * pollfd.Size);

            this.userFdBuf = fds;
            this.fdMaps = new int[nfds];
        }
    }

    public sealed class SelectCompletion : ThreadCompletionEntryWithBuffer
    {
        public readonly UserPtr inp;
        public readonly UserPtr outp;
        public readonly UserPtr exp;
        public readonly SelectHelper helper;
        public readonly int fds;
    
        public SelectCompletion(Thread current, int maxfds, UserPtr inp, UserPtr outp, UserPtr exp, SelectHelper helper, ByteBufferRef buf)
            : base(current, Kind.SelectCompletionKind, buf)
        {
            this.fds = maxfds;
            this.inp = inp;
            this.outp = outp;
            this.exp = exp;
            this.helper = helper;
        }
    }

    public sealed class GetSockParamCompletion : ThreadCompletionEntryWithBuffer
    {
        public readonly UserPtr payload;
        public readonly UserPtr p_addrlen;

        public GetSockParamCompletion(Thread current, UserPtr payload, UserPtr p_addrlen, ByteBufferRef buf)
            : base(current, Kind.GetSocketParamCompletionKind, buf)
        {
            this.payload = payload;
            this.p_addrlen = p_addrlen;
        }
    }
}
