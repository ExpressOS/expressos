
namespace ExpressOS.Kernel.Arch
{
    public struct BootParam
    {
        public const int UTCBOffset = 4096;
        public readonly Pointer MainMemoryStart;
        public readonly int MainMemorySize;
        public readonly L4Handle LinuxServerTid;
        public readonly Pointer LinuxMainMemoryStart;
        public readonly int LinuxMainMemorySize;
        public readonly Pointer SyncIPCBufferBase;
        public readonly int SyncIPCBufferSize;
        public readonly Pointer CompletionQueueBase;
        public readonly int CompletionQueueSize;
    }
}
