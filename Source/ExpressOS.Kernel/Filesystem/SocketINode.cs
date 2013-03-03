namespace ExpressOS.Kernel
{
    internal sealed class SocketINode : Arch.ArchINode
    {
        internal SocketINode(int fd, int helperPid)
            : base(fd, 0, helperPid, INodeKind.SocketINodeKind)
        { }
    }
}
