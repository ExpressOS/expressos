namespace ExpressOS.Kernel
{
    public sealed class BridgeCompletion : ThreadCompletionEntryWithBuffer
    {
        public BridgeCompletion(Thread current, ByteBufferRef buf)
            : base(current, Kind.BridgeCompletionKind, buf)
        { }
    }

}
