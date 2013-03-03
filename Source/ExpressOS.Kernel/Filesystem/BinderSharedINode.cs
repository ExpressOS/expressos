
namespace ExpressOS.Kernel
{
    /*
     * Base class for inode that shares memory (i.e., alien memory) with Linux.
     */
    internal class AlienSharedMemoryINode : Arch.ArchINode
    {
        internal Pointer vaddrInShadowProcess;
        internal AlienSharedMemoryINode(int linux_fd, int helperPid, INodeKind kind)
            : base(linux_fd, 0, helperPid, kind)
        { }
    }

    /*
     * File / Memory region opened by other processes, and mapped into the process
     * through binder IPC.
     */
    internal sealed class BinderSharedINode : AlienSharedMemoryINode
    {
        internal BinderSharedINode(int linux_fd, int helperPid)
            : base(linux_fd, helperPid, INodeKind.BinderSharedINodeKind)
        { }
    }
}
