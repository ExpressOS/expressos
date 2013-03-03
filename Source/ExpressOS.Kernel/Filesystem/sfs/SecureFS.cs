using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public class SecureFS
    {
        internal static byte[] HMACSecretKey;
 
        private const int READ_AHEAD_PAGES = 1;

        public static void Initialize(byte[] HMACKey)
        {
            HMACSecretKey = HMACKey;
        }

        public static bool IsSecureFS(Thread current, byte[] filename)
        {
            if (Util.ByteStringCompare(current.Parent.SFSFilePrefix, filename) != 0)
                return false;

            Globals.LinuxIPCBuffer.CopyFrom(0, filename);
            var ret = Arch.IPCStubs.linux_sys_stat64(current.Parent.helperPid);
            if (ret == 0 && FileSystem.StatIsDir(Globals.LinuxIPCBuffer))
                return false;

            return true;
        }

        internal static OpenFileCompletion OpenAndReadPagesAsync(Thread current, byte[] filename, int flags, int mode)
        {
            Contract.Requires(filename.Length < READ_AHEAD_PAGES * Arch.ArchDefinition.PageSize);
            Utils.Assert(filename[filename.Length - 1] == 0);

            var size = READ_AHEAD_PAGES * Arch.ArchDefinition.PageSize;

            var buf = Globals.AllocateAlignedCompletionBuffer(size);
            if (!buf.isValid)
                return null;

            buf.CopyFrom(0, filename);

            var completion = new OpenFileCompletion(current, GenericINode.INodeKind.SecureFSINodeKind, buf, flags, mode);
            Arch.IPCStubs.OpenAndReadPagesAsync(current.Parent.helperPid, current.impl._value.thread._value, new Pointer(completion.buf.Location), buf.Length / Arch.ArchDefinition.PageSize, flags, mode);
            return completion;
        }

        internal static GenericINode HandleOpenFileCompletion(OpenFileCompletion completion, int linux_fd, int size, ref int p_ret)
        {
            var current = completion.thr;
            return SecureFSInode.Create(current, completion.buf, linux_fd, size, ref p_ret);
        }
    }
}
