using ExpressOS.Kernel.Arch;
using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public static class Globals
    {
        public static FreeListPageAllocator PageAllocator;
        public static ThreadList Threads;

        public static ByteBufferRef LinuxIPCBuffer
        {
            get
            {
                Contract.Ensures(Contract.Result<ByteBufferRef>().Length >= ArchGlobals.MinimumIPCBufferSize);
                Contract.Assume(ArchGlobals.LinuxIPCBuffer.Length >= ArchGlobals.MinimumIPCBufferSize);
                return ArchGlobals.LinuxIPCBuffer;
            }
        }

        public static Arch.BootParam BootParam;
        public static FreeListPageAllocator CompletionQueueAllocator;
        public static FutexCompletionEntry FutexLists;
        public static TimerQueue TimeoutQueue;
        public static SecurityManager SecurityManager;
        public static LinuxMemoryAllocator LinuxMemoryAllocator;
        public static CapabilityManager CapabilityManager;
        public static CompletionQueue CompletionQueue;


        public static void Initialize(ref Arch.BootParam param)
        {
            BootParam = param;

            PageAllocator = new FreeListPageAllocator();
            PageAllocator.Initialize(param.MainMemoryStart, param.MainMemorySize >> Arch.ArchDefinition.PageShift);

            CompletionQueueAllocator = new FreeListPageAllocator();
            CompletionQueueAllocator.Initialize(param.CompletionQueueBase, param.CompletionQueueSize >> Arch.ArchDefinition.PageShift);

            Threads = ThreadList.CreateSentinal();
            FutexLists = FutexCompletionEntry.CreateSentinal();
            TimeoutQueue = new TimerQueue();
            SecurityManager = new SecurityManager();
            LinuxMemoryAllocator = new LinuxMemoryAllocator();
            CapabilityManager = new CapabilityManager();
            CompletionQueue = new CompletionQueue();
            
            SecureFS.Initialize(Util.StringToByteArray("ExpressOS-security", false));
            ReadBufferUnmarshaler.Initialize();
        }

        public static ByteBufferRef AllocateAlignedCompletionBuffer(int len)
        {
            Contract.Ensures(!Contract.Result<ByteBufferRef>().isValid || Contract.Result<ByteBufferRef>().Length >= len);
            
            var aligned_size = (int)Arch.ArchDefinition.PageAlign((uint)len);           
            var requiredPages = aligned_size / Arch.ArchDefinition.PageSize;

            // Aligned size is greater than the length
            Contract.Assume(requiredPages * Arch.ArchDefinition.PageSize >= len);

            var blob = Globals.CompletionQueueAllocator.AllocPages(requiredPages);
            if (!blob.isValid)
            {
                Arch.Console.Write("AllocateAlignedCompletionBuffer: out of memory, len=");
                Arch.Console.Write(len);
                Arch.Console.WriteLine();
                Utils.Panic();
            }

            return blob;
        }
    }
}
