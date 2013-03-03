
namespace ExpressOS.Kernel.Arch
{
    public static class ArchGlobals
    {
        public static ByteBufferRef LinuxIPCBuffer { get; private set; }
        public static L4Handle LinuxServerTid { get; private set; }

        public static Pointer LinuxMainMemoryStart { get; private set; }
        public static int LinuxMainMemorySize { get; private set; }

        /*
         * The minimum size of the synchronous IPC buffer is 65k
         */
        public const uint MinimumIPCBufferSize = 65 * 1024;

        public static void Initialize(ref BootParam param)
        {
            LinuxMainMemoryStart = param.LinuxMainMemoryStart;
            LinuxMainMemorySize = param.LinuxMainMemorySize;
            LinuxIPCBuffer = new ByteBufferRef(param.SyncIPCBufferBase.ToIntPtr(), param.SyncIPCBufferSize);

            if (param.SyncIPCBufferSize < MinimumIPCBufferSize)
                ArchDefinition.Panic();

            LinuxServerTid = param.LinuxServerTid;
        }
    }
}
