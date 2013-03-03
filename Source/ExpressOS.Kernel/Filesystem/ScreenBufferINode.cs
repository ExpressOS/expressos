namespace ExpressOS.Kernel
{
    internal sealed class ScreenBufferINode : AlienSharedMemoryINode
    {
        internal ScreenBufferINode(int linux_fd, int helperPid)
            : base(linux_fd, helperPid, INodeKind.ScreenBufferINodeKind)
        { }
    }
}
