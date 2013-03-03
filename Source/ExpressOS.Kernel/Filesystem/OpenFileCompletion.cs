namespace ExpressOS.Kernel
{
    public sealed class OpenFileCompletion : ThreadCompletionEntryWithBuffer
    {
        public readonly GenericINode.INodeKind fileKind;
        public readonly int flags;
        public readonly int mode;

        public OpenFileCompletion(Thread current, GenericINode.INodeKind fileKind, ByteBufferRef buf, int flags, int mode)
            : base(current, Kind.OpenFileCompletionKind, buf)
        {
            this.fileKind = fileKind;
            this.flags = flags;
            this.mode = mode;
        }
    }
}
