using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public class FileDescriptorTable
    {
        public File[] descriptors;
        public int finger;
        public readonly Process GhostOwner;
        private const int DEFAULT_SIZE = 128;
        private const int FD_START = 3;

        [ContractInvariantMethod]
        private void ObjectInvariantMethod()
        {
            Contract.Invariant(descriptors != null);
            Contract.Invariant(finger < descriptors.Length);
        }

        public FileDescriptorTable(Process owner)
        {
            Contract.Ensures(GhostOwner == owner);

            descriptors = new File[DEFAULT_SIZE];
            for (var i = 0; i < descriptors.Length; ++i)
            {
                descriptors[i] = null;
            }

            finger = FD_START;
            this.GhostOwner = owner;
        }

        [Pure]
        public File Lookup(int fd)
        {
            Contract.Ensures(Contract.Result<File>() == null || (IsValidFd(fd) && Contract.Result<File>().GhostOwner == GhostOwner));
           
            if (!IsValidFd(fd))
            {
                return null;
            }

            var ret = descriptors[fd];

            Contract.Assert(ret == null || IsValidFd(fd));

            // Proven by Dafny
            Contract.Assume(ret == null || ret.GhostOwner == GhostOwner);
            return ret;
        }

        internal void Add(int fd, File file)
        {
            Contract.Requires(IsAvailableFd(fd));
            Contract.Requires(file != null && file.GhostOwner == GhostOwner);
            Contract.Ensures(IsValidFd(fd));

            descriptors[fd] = file;
        }

        internal int GetUnusedFd()
        {
            Contract.Ensures(IsAvailableFd(Contract.Result<int>()));

            var _this = this;
            var size = descriptors.Length;
            var i = finger;
            while (i < size)
            {
                if (IsAvailableFd(i))
                {
                    UpdateFinger(i);
                    return i;
                }
                ++i;
            }

            var new_descriptors = new File[2 * size];
            for (var j = 0; j < size; ++j)
            {
                new_descriptors[i] = descriptors[i];
            }

            for (var j = size; j < 2 * size; ++j)
            {
                new_descriptors[i] = null;
            }

            descriptors = new_descriptors;

            // Proven by Dafny
            Contract.Assume(IsAvailableFd(size));
            UpdateFinger(size);
            return size;
        }

        internal void Remove(int fd)
        {
            Contract.Requires(IsValidFd(fd));
            Contract.Ensures(IsAvailableFd(fd));

            descriptors[fd] = null;
            UpdateFinger(fd);
        }

        private void UpdateFinger(int fd)
        {
            Contract.Requires(IsAvailableFd(fd));
            Contract.Ensures(IsAvailableFd(fd));

            if (fd >= 3)
            {
                finger = fd;
            }

            // Proven by Dafny
            Contract.Assume(IsAvailableFd(fd));
        }

        [Pure]
        internal bool IsValidFd(int fd)
        {
            Contract.Ensures(Contract.Result<bool>() == (fd > 0 && fd < descriptors.Length));
            return fd > 0 && fd < descriptors.Length;
        }

        [Pure]
        internal bool IsAvailableFd(int fd)
        {
            Contract.Ensures(Contract.Result<bool>() == (IsValidFd(fd) && descriptors[fd] == null));
            return IsValidFd(fd) && descriptors[fd] == null;
        }


    }
}
