
using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    /*
     * Class to represent an address space in the kernel.
     *
     * An address space contains a series of memory region, each of which
     * represents a continous segment of the virtual memory in the address
     * space.
     * 
     * The implementation reserves two special memory regions that are
     * inaccessible from the application, which are the first page and the
     * high 1GB of the address space.
     */
    public partial class AddressSpace
    {
        internal readonly TableWorkingSet workingSet;
        internal readonly Arch.ArchAddressSpace impl;
        public uint StartBrk;
        public uint Brk;
        public const uint KERNEL_OFFSET = 0xc0000000;
        public const int KERNEL_SIZE = 0x10000000 - 1;
   
        [ContractInvariantMethod]
        private void ObjectInvariantMethod()
        {
            Contract.Invariant(Head.GhostOwner == GhostOwner);
            Contract.Invariant(Brk >= StartBrk);
        }

        public AddressSpace(Process owner, Arch.ArchAddressSpace impl)
        {
            Contract.Ensures(GhostOwner == owner);
            Contract.Ensures(Head.GhostOwner == GhostOwner);

            this.impl = impl;
            this.workingSet = new TableWorkingSet();
            this.Head = MemoryRegion.CreateUserSpaceRegion(owner);
            this.GhostOwner = owner;
            this.Brk = this.StartBrk = 0;
        }

        private void RemoveWorkingSet(Pointer vaddr, int size)
        {
            Contract.Ensures(Brk == Contract.OldValue(Brk));
            workingSet.Remove(this, new UserPtr(vaddr), new UserPtr(vaddr) + size);
        }

        internal int AddStackMapping(UserPtr location, int memorySize)
        {
            Contract.Requires(memorySize > 0);
            return AddMapping(MemoryRegion.FAULT_READ | MemoryRegion.FAULT_WRITE, 0, null, 0, 0, location.Value, memorySize);
        }

        internal bool AddHeapMapping(uint newBrk)
        {
            Contract.Requires(newBrk > Brk);

            var r = AddMapping(MemoryRegion.FAULT_READ | MemoryRegion.FAULT_WRITE, 0, null, 0, 0, new Pointer(Brk), (int)(newBrk - Brk));
            Contract.Assert(newBrk > Brk);
            if (r == 0)
            {
                Brk = newBrk;
                return true;
            }
            return false;
        }

        public Pointer UserToVirt(UserPtr addr)
        {
            return workingSet.UserToVirt(addr);
        }

        public void AddIntoWorkingSet(UserPtr userPtr, Pointer virtualAddr)
        {
            workingSet.Add(userPtr, virtualAddr);
        }

        internal Pointer FindFreeRegion(int length)
        {
            var r = Head;

            while (r.Next != null && r.End + length > r.Next.StartAddress)
                r = r.Next;

            return r.Next == null ? Pointer.Zero : r.End;
        }

        internal bool ContainRegion(Pointer targetAddr, int length)
        {
            var r = Head;

            while (r != null && !r.OverlappedInt(targetAddr, length))
                r = r.Next;

            return r != null;
        }

        public bool SanityCheck()
        {
            return Head.SanityCheck();
        }

        public void DumpAll()
        {
            Arch.Console.WriteLine("Region dump:");
            var r = Head;
            while (r != null)
            {
                r.Dump();
                r = r.Next;
            }
        }

        private bool Verify(UserPtr start, uint size, uint access)
        {
            var start_ptr = start.Value;
            var end = start_ptr + size;
            while (size > 0)
            {
                // TODO: Use hint to speed up the lookup
                var region = Find(start_ptr);
                if (region == null || (access & region.Access) == 0 || region.IsFixed)
                    return false;

                if (region.End >= end)
                    return true;

                start_ptr = region.End;
            }
            return true;
        }

        // Check whether we can access the user-level vma
        internal bool VerifyRead(UserPtr start, uint size)
        {
            return Verify(start, size, MemoryRegion.FAULT_READ);
        }

        internal bool VerifyWrite(UserPtr start, uint size)
        {
            return Verify(start, size, MemoryRegion.FAULT_WRITE);
        }

        internal void InitializeBrk(uint newBrk)
        {
            Contract.Ensures(Brk == newBrk);
            Contract.Ensures(StartBrk == newBrk);

            Brk = StartBrk = newBrk;
        }
    }
}
