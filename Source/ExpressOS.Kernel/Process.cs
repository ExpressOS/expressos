using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public class Process
    {
        internal uint EntryPoint;
        public readonly AddressSpace Space;
        internal readonly Credential Credential;
        internal readonly AndroidApplicationInfo AppInfo;
        internal readonly byte[] SFSFilePrefix;

        internal int helperPid;
        internal uint ShadowBinderVMStart;
        internal UserPtr binderVMStart;
        internal int binderVMSize;
        internal bool ScreenEnabled;

        public const int STDOUT_FD = 1;
        public const int STDERR_FD = 2;

        internal readonly FileDescriptorTable Files;

        const uint INITIAL_STACK_LOCATION = 0xb2000000;

        [ContractInvariantMethod]
        private void ObjectInvariantMethod()
        {
            Contract.Invariant(Credential.GhostOwner == this);
            Contract.Invariant(Space.GhostOwner == this);
            Contract.Invariant(Files.GhostOwner == this);
        }

        internal Process(ASCIIString name, AndroidApplicationInfo appInfo)
        {
            Contract.Ensures(Credential.GhostOwner == this);
            Contract.Ensures(Space.GhostOwner == this);
            Contract.Ensures(Files.GhostOwner == this);

            this.AppInfo = appInfo;
            SFSFilePrefix = Util.StringToByteArray(appInfo.DataDir, false);

            this.Files = new FileDescriptorTable(this);

            const uint UTCB_BOTTOM = 0xc0000000;
            var utcb = new Pointer(UTCB_BOTTOM);

            // TODO: expose interface to change the name of the process
            var archAddressSpace = Arch.ArchAddressSpace.Create(name, utcb, 
                Arch.ArchDefinition.UTCBSizeShift + Arch.ArchDefinition.MaxThreadPerTaskLog2);
            this.Space = new AddressSpace(this, archAddressSpace);
            this.Credential = SecurityManager.GetCredential(name, this);
        }

        [Pure]
        internal bool IsValidFd(int fd)
        {
            Contract.Ensures(Contract.Result<bool>() == Files.IsValidFd(fd));
            return Files.IsValidFd(fd);
        }

        public int GetUnusedFd()
        {
            Contract.Ensures(Files.IsAvailableFd(Contract.Result<int>()));
            return Files.GetUnusedFd();
        }

        [Pure]
        public File LookupFile(int fd)
        {
            Contract.Ensures(Contract.Result<File>() == null || IsValidFd(fd));
            Contract.Ensures(Contract.Result<File>() == null || Contract.Result<File>().GhostOwner == this);

            var file = Files.Lookup(fd);
            if (file == null)
            {
                return null;
            }

            Contract.Assume(Files.IsValidFd(fd));
            Contract.Assert(file.GhostOwner == this);
            return file;
        }

        internal void InstallFd(int fd, File file)
        {
            Contract.Requires(file != null);
            Contract.Requires(file.GhostOwner == this);
            Contract.Requires(Files.IsAvailableFd(fd));

            Files.Add(fd, file);
        }

        internal void UninstallFd(int fd)
        {
            Contract.Requires(Files.IsValidFd(fd));
            Contract.Ensures(Files.IsAvailableFd(fd));

            Files.Remove(fd);
        }
    }
}
