namespace ExpressOS.Kernel
{
    public sealed class VBinderCompletion : ThreadCompletionEntry
    {
        public readonly UserPtr ptr_label;
        public readonly UserPtr userBuf;
        public readonly uint size;

        public VBinderCompletion(Thread current, UserPtr ptr_label, UserPtr userBuf, uint size)
            : base(current, Kind.VBinderCompletionKind)
        {
            this.ptr_label = ptr_label;
            this.userBuf = userBuf;
            this.size = size;
        }
    }
}
