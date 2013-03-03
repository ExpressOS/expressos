
using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    internal sealed class AshmemINode : AlienSharedMemoryINode
    {
        internal AshmemINode(int linux_fd, int helperPid)
            : base(linux_fd, helperPid, INodeKind.AshmemINodeKind)
        { }     

        public const int ASHMEM_NAME_LEN = 256;
        public const uint ASHMEM_SET_NAME = 0x41007701;
        public const uint ASHMEM_GET_NAME = 0x81007702;
        public const uint ASHMEM_SET_SIZE = 0x40047703;
        public const uint ASHMEM_GET_SIZE = 0x00007704;
        public const uint ASHMEM_SET_PROT_MASK = 0x40047705;
        public const uint ASHMEM_GET_PROT_MASK = 0x00007706;
        public const uint ASHMEM_PIN = 0x40087707;
        public const uint ASHMEM_UNPIN = 0x40087708;
        public const uint ASHMEM_GET_PIN_STATUS = 0x00007709;
        public const uint ASHMEM_PURGE_ALL_CACHES = 0x0000770a;
        public const int ASHMEM_PIN_SIZE = 8;

        internal static int AshmemIoctl(GenericINode generic_inode, Thread current, uint cmd, UserPtr arg1)
        {
            Contract.Requires(Globals.LinuxIPCBuffer.Length >= AshmemINode.ASHMEM_PIN_SIZE);

            int ret = 0;

            switch (cmd)
            {
                case AshmemINode.ASHMEM_SET_NAME:
                    arg1.ReadString(current, Globals.LinuxIPCBuffer);
                    break;

                case AshmemINode.ASHMEM_PIN:
                case AshmemINode.ASHMEM_UNPIN:
                    if (arg1.Read(current, Globals.LinuxIPCBuffer, AshmemINode.ASHMEM_PIN_SIZE) != 0)
                        ret = -ErrorCode.EFAULT;
                    break;

                case AshmemINode.ASHMEM_GET_NAME:
                case AshmemINode.ASHMEM_SET_SIZE:
                case AshmemINode.ASHMEM_GET_SIZE:
                case AshmemINode.ASHMEM_SET_PROT_MASK:
                case AshmemINode.ASHMEM_GET_PROT_MASK:
                case AshmemINode.ASHMEM_GET_PIN_STATUS:
                case AshmemINode.ASHMEM_PURGE_ALL_CACHES:
                    break;

                default:
                    ret = -ErrorCode.ENOSYS;
                    break;
            }

            if (ret < 0)
                return ret;

            var linux_fd = generic_inode.LinuxFd;

            ret = Arch.IPCStubs.linux_sys_vfs_ashmem_ioctl(current.Parent.helperPid, linux_fd, cmd, arg1.Value.ToInt32());

            if (ret < 0)
                  return ret;
  
            // unmarshal if necessary
            if (cmd == AshmemINode.ASHMEM_GET_NAME)
            {
                var length = Util.Strnlen(Globals.LinuxIPCBuffer, AshmemINode.ASHMEM_NAME_LEN);
                // include terminator
                if (length < AshmemINode.ASHMEM_NAME_LEN)
                    length++;

                var buf = Globals.LinuxIPCBuffer.Slice(0, length);
                if (arg1.Write(current, Globals.LinuxIPCBuffer) != 0)
                    return -ErrorCode.EFAULT;
            }

            return ret;
        }

    }
}
