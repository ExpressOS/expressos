using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using ExpressOS.Kernel;
using Arch = ExpressOS.Kernel.Arch;

namespace ExpressOS.Kernel
{
    public class SelectHelper
    {
        class FdMapNode
        {
            internal readonly int linux_fd;
            internal readonly int expressos_fd;
            internal short event_type;
            internal FdMapNode next;

            internal FdMapNode(int linux_fd, int expressos_fd, short event_type)
            {
                this.linux_fd = linux_fd;
                this.expressos_fd = expressos_fd;
                this.event_type = event_type;
            }
        }

        FdMapNode fdlist;
        public int TotalFds { get; private set; }

        internal SelectHelper()
        {
            fdlist = new FdMapNode(0, 0, 0);
        }

        private int AddFdList(Thread current, FixedSizeBitVector fdlist, short event_type)
        {
            var proc = current.Parent;
            for (int fd = fdlist.FindNextOne(-1); fd > 0; fd = fdlist.FindNextOne(fd)) {
                //Arch.Console.Write("AddFdList:");
                //Arch.Console.Write(fd);
                //Arch.Console.Write(" ev-");
                //Arch.Console.Write(event_type);
                //Arch.Console.WriteLine();

                var file = proc.LookupFile(fd);
                if (file == null)
                    return -ErrorCode.EBADF;

                if (file.inode.LinuxFd < 0)
                    return -ErrorCode.EINVAL;

                Add(file.inode.LinuxFd, fd, event_type);
            }
            return 0;
        }

        private void Add(int linux_fd, int expressos_fd, short event_type)
        {
            if (linux_fd < 0)
                return;

            var r = fdlist;
            var prev = r;
            while (r != null && r.linux_fd < linux_fd)
            {
                prev = r;
                r = r.next;
            }

            if (r != null && r.linux_fd == linux_fd)
            {
                r.event_type |= event_type;
            }
            else
            {
                var n = new FdMapNode(linux_fd, expressos_fd, event_type);
                ++TotalFds;
                n.next = prev.next;
                prev.next = n;
            }
        }

        internal void WritePollFds(ByteBufferRef buf)
        {
            Contract.Requires(TotalFds * pollfd.Size <= buf.Length);

            var i = 0;
            var r = fdlist.next;
            while (r != null && i < TotalFds)
            {
                pollfd poll_struct;
                poll_struct.fd = r.linux_fd;
                poll_struct.events = r.event_type;
                poll_struct.revents = 0;
                Contract.Assert(i * pollfd.Size + pollfd.Size <= buf.Length);
                poll_struct.Write(buf, i * pollfd.Size);
                ++i;
            }
        }

        internal int AddUserFdList(Thread current, UserPtr fdlist, int maxfds, short event_type)
        {
            if (fdlist == UserPtr.Zero)
                return 0;

            var buf = new byte[(maxfds + 7) / 8];
            if (fdlist.Read(current, buf) != 0)
                return -ErrorCode.EFAULT;

            var vec = new FixedSizeBitVector(maxfds, buf);

            var ret = AddFdList(current, vec, event_type);
            return ret;
        }

        internal int TranslateToUserFdlist(Thread current, ByteBufferRef buf, int poll_ret, int maxfds, UserPtr userPtr, short event_type)
        {
            Contract.Requires(poll_ret * pollfd.Size < buf.Length);

            if (userPtr == UserPtr.Zero)
                return 0;

            var res = 0;
            var len = (maxfds + 7) / 8;
            var vec = new FixedSizeBitVector(maxfds);

            for (int i = 0; i < poll_ret; i++)
            {
                var poll_struct = pollfd.Deserialize(buf, i * pollfd.Size);
                var linux_fd = poll_struct.fd;
                var node = Lookup(linux_fd);
                if (node == null)
                    return -ErrorCode.EBADF;

                if ((poll_struct.revents & event_type & node.event_type) != 0)
                {
                    vec.Set(node.expressos_fd);
                    ++res;
                }
            }

            if (userPtr.Write(current, vec.Buffer) != 0)
                return -ErrorCode.EFAULT;

            return res;
        }

        private FdMapNode Lookup(int linux_fd)
        {
            var r = fdlist.next;
            while (r != null && r.linux_fd < linux_fd)
                r = r.next;

            return r != null && r.linux_fd == linux_fd ? r : null;
        }
    }
}
