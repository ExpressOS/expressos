using System;

namespace ExpressOS.Kernel.Arch
{
    public static class ArchFS
    {
        internal static int OpenAndReturnLinuxFd(int helperPid, ASCIIString fileName, int flag, int mode)
        {
            Globals.LinuxIPCBuffer.CopyFrom(0, fileName.GetByteString());
            var fd = IPCStubs.Open(helperPid, flag, mode);
            return fd;
        }

        public static ArchINode Open(int helperPid, ASCIIString fileName, int flag, int mode, out ErrorCode ec)
        {
            var fd = OpenAndReturnLinuxFd(helperPid, fileName, flag, mode);
            if (fd < 0) {
                ec.Code = -fd;
                return null;
            }

            var ret = IPCStubs.linux_sys_fstat64(helperPid, fd);

            uint size = 0;
            if (ret >= 0)
                size = (uint)FileSystem.GetSizeFromStat64(Globals.LinuxIPCBuffer);

            ec.Code = ErrorCode.NoError;
            return new ArchINode(fd, size, helperPid);
        }

        internal static OpenFileCompletion OpenAndGetSizeAsync(Thread current, byte[] filename, int flags, int mode)
        {
            ArchDefinition.Assert(filename[filename.Length - 1] == 0);
            var buf = Globals.AllocateAlignedCompletionBuffer(filename.Length);
            if (!buf.isValid)
                return null;

            buf.CopyFrom(0, filename);

            var completion = new OpenFileCompletion(current, GenericINode.INodeKind.ArchINodeKind, buf, flags, mode);
            IPCStubs.OpenAndGetSizeAsync(current.Parent.helperPid, current.impl._value.thread._value, new Pointer(completion.buf.Location), flags, mode);
            return completion;
        }
    }
}
