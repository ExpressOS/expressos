using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public class VBinderMessage
    {
        public readonly Thread from;
        public readonly int label;
        public readonly ByteBufferRef payload;
        public readonly int Length;

        public readonly Thread GhostTarget;

        public VBinderMessage(Thread from, Thread target, int label, ByteBufferRef payload, int length)
        {
            Contract.Ensures(GhostTarget == target);
            this.from = from;
            this.label = label;
            this.payload = payload;
            this.GhostTarget = target;
            this.Length = length;
        }

        internal void Recycle()
        {
            var size = (uint)payload.Length;
            var aligned_size = Arch.ArchDefinition.PageAlign(size);
            var pages = (int)(aligned_size / Arch.ArchDefinition.PageSize);

            Globals.CompletionQueueAllocator.FreePages(new Pointer(payload.Location), pages);
        }
    }
}
