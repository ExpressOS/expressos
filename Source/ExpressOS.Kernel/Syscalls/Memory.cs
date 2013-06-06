using System;

using Arch = ExpressOS.Kernel.Arch;
using System.Runtime.InteropServices;
using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public static class Memory
    {
        #region Constants
        public const int PROT_READ = 0x1;            /* Page can be read.  */
        public const int PROT_WRITE = 0x2;           /* Page can be written.  */
        public const int PROT_EXEC = 0x4;            /* Page can be executed.  */
        public const int PROT_NONE = 0x0;            /* Page can not be accessed.  */
        public const int PROT_GROWSDOWN = 0x01000000;     /* Extend change to start of growsdown vma (mprotect only).  */
        public const int PROT_GROWSUP = 0x02000000;  /* Extend change to start of growsup vma (mprotect only).  */

        /* Sharing types (must choose one and only one of these).  */
        public const int MAP_SHARED = 0x01;          /* Share changes.  */
        public const int MAP_PRIVATE = 0x02;         /* Changes are private.  */
        public const int MAP_TYPE = 0x0f;            /* Mask for type of mapping.  */

        /* Other flags.  */
        public const int MAP_FIXED = 0x10;           /* Interpret addr exactly.  */

        public const int MAP_FILE = 0;
        public const int MAP_ANONYMOUS = 0x20;       /* Don't use a file.  */
        public const int MAP_ANON = MAP_ANONYMOUS;
        public const int MAP_32BIT = 0x40;           /* Only give out 32-bit addresses.  */

        /* These are Linux-specific.  */
        public const int MAP_GROWSDOWN = 0x00100;    /* Stack-like segment.  */
        public const int MAP_DENYWRITE = 0x00800;    /* ETXTBSY */
        public const int MAP_EXECUTABLE = 0x01000;   /* Mark it as an executable.  */
        public const int MAP_LOCKED = 0x02000;       /* Lock the mapping.  */
        public const int MAP_NORESERVE = 0x04000;    /* Don't check for reservations.  */
        public const int MAP_POPULATE = 0x08000;     /* Populate (prefault) pagetables.  */
        public const int MAP_NONBLOCK = 0x10000;     /* Do not block on IO.  */
        public const int MAP_STACK = 0x20000;        /* Allocation is for a stack.  */
        public const int MAP_HUGETLB = 0x40000;      /* Create huge page mapping.  */

        public const int MADV_NORMAL = 0;            /* no further special treatment */
        public const int MADV_RANDOM = 1;            /* expect random page references */
        public const int MADV_SEQUENTIAL = 2;        /* expect sequential page references */
        public const int MADV_WILLNEED = 3;          /* will need these pages */
        public const int MADV_DONTNEED = 4;          /* don't need these pages */

        /* common parameters: try to keep these consistent across architectures */
        public const int MADV_REMOVE = 9;            /* remove these pages & resources */
        public const int MADV_DONTFORK = 10;         /* don't inherit across fork */
        public const int MADV_DOFORK = 11;           /* do inherit across fork */
        public const int MADV_HWPOISON = 100;        /* poison a page for testing */
        public const int MADV_SOFT_OFFLINE = 101;    /* soft offline page for testing */

        public const int MADV_MERGEABLE = 12;        /* KSM may merge identical pages */
        public const int MADV_UNMERGEABLE = 13;      /* KSM may not merge identical pages */

        public const int MADV_HUGEPAGE = 14;         /* Worth backing with hugepages */
        public const int MADV_NOHUGEPAGE = 15;       /* Not worth backing with hugepages */

        #endregion

        public static uint Brk(Thread current, uint brk)
        {
            Contract.Requires(current.Parent.Space.Brk >= current.Parent.Space.StartBrk);
            Contract.Ensures(Contract.Result<uint>() >= current.Parent.Space.StartBrk);

            var space = current.Parent.Space;
            uint oldBrk = space.Brk;

            brk = Arch.ArchDefinition.PageAlign(brk);

            if (brk < space.StartBrk || brk > AddressSpace.KERNEL_OFFSET
                || brk <= oldBrk // Not handling shrink right now
                )
            {
                return oldBrk;
            }
            else if (space.AddHeapMapping(brk))
            {
                return brk;
            }
            else
            {
                return oldBrk;
            }
        }

        public static int mmap2(Thread current, UserPtr addr, int length, int prot, int flags, int fd, int pgoffset)
        {
            Contract.Requires(current.Parent == current.Parent.Space.GhostOwner);

            Pointer targetAddr = new Pointer(Arch.ArchDefinition.PageAlign(addr.Value.ToUInt32()));

            //Arch.Console.Write("mmap2:");
            //Arch.Console.Write(addr.Value.ToInt32());
            //Arch.Console.Write(" sz=");
            //Arch.Console.Write(length);
            //Arch.Console.Write(" prot=");
            //Arch.Console.Write(prot);
            //Arch.Console.WriteLine();

            var proc = current.Parent;
            var space = proc.Space;
            Contract.Assert(proc == space.GhostOwner);

            if ((flags & MAP_FIXED) == 0)
            {
                if (targetAddr == Pointer.Zero || space.ContainRegion(targetAddr, length))
                    targetAddr = space.FindFreeRegion(length);
            }
            else if (Arch.ArchDefinition.PageOffset(addr.Value.ToInt32()) != 0)
            {
                return -ErrorCode.EINVAL;
            }

            targetAddr = new Pointer(Arch.ArchDefinition.PageIndex(targetAddr.ToUInt32()));

            if (targetAddr == Pointer.Zero || length == 0)
                return -ErrorCode.EINVAL;

            File file = null;
            GenericINode inode = null;

            if ((flags & MAP_ANONYMOUS) == 0)
            {
                file = proc.LookupFile(fd);
                if (file == null)
                    return -ErrorCode.EBADF;

                inode = file.inode;
            }
   
            int memorySize = Arch.ArchDefinition.PageAlign(length);
            
            // fix for code contract
            if (length > memorySize)
                length = memorySize;

            if ((file != null && length == 0) || length < 0)
                return -ErrorCode.EINVAL;

            if (file == null)
            {
                pgoffset = 0;
                length = 0;
            }

            //
            // Be careful for shared mapping -- which could be a shared memory region coming from Linux.
            // In this case we'll need to (1) call mmap() in the shadow process to obtain a valid mapping
            // (2) when a page fault happens, grabs the physical page from linux.
            //
            if ((flags & MAP_SHARED) != 0 && SharedWithLinux(inode))
            {
                // Don't know how to deal with it...
                if (addr != UserPtr.Zero)
                   return -ErrorCode.EINVAL;
               
                var vaddr = Arch.IPCStubs.linux_sys_alien_mmap2(current.Parent.helperPid, addr.Value, length,
                    prot, flags, inode.LinuxFd, pgoffset);

                if (vaddr > AddressSpace.KERNEL_OFFSET)
                    return -ErrorCode.EINVAL;

                switch (inode.kind)
                {
                    case GenericINode.INodeKind.BinderSharedINodeKind:
                    case GenericINode.INodeKind.AshmemINodeKind:
                    case GenericINode.INodeKind.ScreenBufferINodeKind:
                        inode.AlienSharedMemoryINode.vaddrInShadowProcess = new Pointer(vaddr);
                        break;

                    default:
                        // UNIMPLEMENTED... let's return EINVAL to make sure we can catch it.
                        return -ErrorCode.EINVAL;
                }
            }

            var r = space.AddMapping(ProtToAccessFlag(prot), flags, file, 
                (uint)pgoffset * Arch.ArchDefinition.PageSize, length, targetAddr, memorySize);

            if (r < 0)
                return r;

            //
            // HACK for binder IPC
            //
            if (inode != null && inode.kind == GenericINode.INodeKind.BinderINodeKind)
            {
                proc.binderVMStart = new UserPtr(targetAddr);
                proc.binderVMSize = length;
            }

            return targetAddr.ToInt32();
        }

        private static bool SharedWithLinux(GenericINode inode)
        {
            return inode.kind == GenericINode.INodeKind.BinderSharedINodeKind
                || inode.kind == GenericINode.INodeKind.AshmemINodeKind
                || inode.kind == GenericINode.INodeKind.ScreenBufferINodeKind;
        }

        private static uint ProtToAccessFlag(int flags)
        {
            uint target_flag = 0;
            if ((flags & PROT_READ) != 0)
                target_flag |= MemoryRegion.FAULT_READ;

            if ((flags & PROT_WRITE) != 0)
                target_flag |= MemoryRegion.FAULT_WRITE;

            if ((flags & PROT_EXEC) != 0)
                target_flag |= MemoryRegion.FAULT_EXEC;

            return target_flag;
        }

        public static int mprotect(Thread current, UserPtr addr, int len, int prot)
        {
            if (len < 0)
                return -ErrorCode.EINVAL;

            if (Arch.ArchDefinition.PageOffset(addr.Value.ToUInt32()) != 0)
                return -ErrorCode.EINVAL;

            var space = current.Parent.Space;

            var nextAddr = addr.Value;

            var aligned_len = (int)Arch.ArchDefinition.PageAlign((uint)len);

            var r = space.UpdateAccessRightRange(addr.Value, aligned_len, ProtToAccessFlag(prot));
            if (!r)
            {
                return -ErrorCode.EINVAL;
            }

            if (!space.SanityCheck())
            {
                Arch.Console.Write("Sanity Check failed, after mprotect vaddr=");
                Arch.Console.Write(addr.Value.ToUInt32());
                Arch.Console.Write(", len=");
                Arch.Console.Write(len);
                space.DumpAll();
                Utils.Panic();
            }

            return 0;
        }

        public static int munmap(Thread current, UserPtr addr, int length)
        {
            if (length <= 0)
                return -ErrorCode.EINVAL;

            var space = current.Parent.Space;
            var alignedLength = Arch.ArchDefinition.PageAlign(length);

            //Arch.Console.Write("munmap:");
            //Arch.Console.Write(addr.Value.ToInt32());
            //Arch.Console.Write(" sz=");
            //Arch.Console.Write(length);
            //Arch.Console.WriteLine();

            if (alignedLength <= 0)
                return -ErrorCode.EINVAL;

            return space.RemoveMapping(addr.Value, alignedLength);
        }

        public static int madvise(Thread current, uint start, int len, int behavior)
        {
            switch (behavior)
            {
                case MADV_NORMAL:
                case MADV_RANDOM:
                case MADV_SEQUENTIAL:
                case MADV_WILLNEED:
                case MADV_REMOVE:
                case MADV_DONTFORK:
                case MADV_DOFORK:
                case MADV_HWPOISON:
                case MADV_SOFT_OFFLINE:
                case MADV_MERGEABLE:
                case MADV_UNMERGEABLE:
                case MADV_HUGEPAGE:
                case MADV_NOHUGEPAGE:
                    return 0;

                case MADV_DONTNEED:
                    return madviseDontNeed(current, start, len);

                default:
                    return -ErrorCode.EINVAL;
            }
        }

        //* return values:
        //*  zero    - success
        //*  -EINVAL - start + len < 0, start is not page-aligned,
        //*              "behavior" is not a valid value, or application
        //*              is attempting to release locked or shared pages.
        //*  -ENOMEM - addresses in the specified range are not currently
        //*              mapped, or are outside the AS of the process.
        //*  -EIO    - an I/O error occurred while paging in data.
        //*  -EBADF  - map exists, but area maps something that isn't a file.
        //*  -EAGAIN - a kernel resource was temporarily unavailable.
        private static int madviseDontNeed(Thread current, uint start, int len)
        {
            if (Arch.ArchDefinition.PageOffset(start) != 0 || len < 0)
                return -ErrorCode.EINVAL;

            var alignedLength = Arch.ArchDefinition.PageAlign((uint)len);
            var endPage = new UserPtr(start + alignedLength);
            current.Parent.Space.workingSet.Remove(current.Parent.Space, new UserPtr(start), endPage);
            return 0;
        }
    }
}
