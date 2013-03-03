using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public sealed partial class MemoryRegion
    {
        public const uint FAULT_MASK = Arch.L4FPage.L4_FPAGE_RWX;
        public const uint FALUT_NONE = Arch.L4FPage.L4_FPAGE_FAULT_NONE;
        public const uint FAULT_READ = Arch.L4FPage.L4_FPAGE_FAULT_READ;
        public const uint FAULT_WRITE = Arch.L4FPage.L4_FPAGE_FAULT_WRITE;
        public const uint FAULT_EXEC = Arch.L4FPage.L4_FPAGE_FAULT_EXEC;

        // Create an empty user-space memory region
        // reserve the first page as well as the kernel space
        public static MemoryRegion CreateUserSpaceRegion(Process owner)
        {
            Contract.Ensures(Contract.Result<MemoryRegion>().GhostOwner == owner);

            var r = new MemoryRegion(owner, 0, 0, null, 0, 0, Pointer.Zero, Arch.ArchDefinition.PageSize, true);
            var r1 = new MemoryRegion(owner, 0, 0, null, 0, 0, new Pointer(AddressSpace.KERNEL_OFFSET), AddressSpace.KERNEL_SIZE, true);
            r.Next = r1;
            return r;
        }

        internal void Dump()
        {
            Arch.Console.Write(StartAddress.ToUInt32());
            Arch.Console.Write("~");
            Arch.Console.Write(End.ToUInt32());
            if (BackingFile != null)
            {
                Arch.Console.Write(" backed at ");
                Arch.Console.Write((long)FileOffset);
                Arch.Console.Write("+");
                Arch.Console.Write(FileSize);
            }
            Arch.Console.Write(" access=");
            Arch.Console.Write(Access);
            Arch.Console.WriteLine();
        }

        public bool SanityCheck()
        {
#if EXPRESSOS_DEBUG
            var r = this;
            var prev = this;
            while (r != null && r.Next != null && r.End <= r.Next.StartAddress)
            {
                if (r.StartAddress == r.End)
                    return false;

                if (Arch.ArchDefinition.PageOffset(r.Size) != 0
                    || Arch.ArchDefinition.PageOffset(r.StartAddress.ToInt32()) != 0
                    || FileSize > Size)
                {
                    return false;
                }

                if (CanMerge(r, r.Next))
                {
                    Arch.Console.Write("Memory Sanity Check -- Unoptimal:");
                    r.Dump();
                    return false;
                }

                prev = r;
                r = r.Next;
            }

            return r.Next == null;
#else
            return true;
#endif
        }

        internal void UpdateAccessRights(AddressSpace space, uint newAccess)
        {
            if (Access == newAccess)
                return;

            Arch.NativeMethods.l4api_flush_regions(space.impl._value, StartAddress, End, (int)(~newAccess & FAULT_MASK));
            Access = newAccess;
        }
    }
}
