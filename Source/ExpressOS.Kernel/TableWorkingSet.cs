namespace ExpressOS.Kernel
{
    /*
     * Tabular working set to solve UserToVirt() query in O(1) time.
     * It mimics two-level paging on x86 hardware.
     */
    public class TableWorkingSet
    {
        public const int PDT_SHIFT = 10;
        public const int PGT_SHIFT = 10;
        public const int PGT_IDX_MASK = ((1 << PGT_SHIFT) - 1) << Arch.ArchDefinition.PageShift;

        private class PageTable
        {
            int DirectoryIndex;
            Pointer[] Table;
            public Pointer this[int key]
            {
                get
                {
                    return Table[key];
                }
                set
                {
                    Table[key] = value;
                }
            }

            public PageTable(int DirectoryIndex) {
                this.DirectoryIndex = DirectoryIndex;
                Table = new Pointer[1 << PGT_SHIFT];
            }
        }

        private PageTable[] Directory;

        public TableWorkingSet()
        {
            Directory = new PageTable[1 << PDT_SHIFT];
        }

        public Pointer UserToVirt(UserPtr addr)
        {
            var directory_index = DirectoryIndex(addr);

            if (Directory[directory_index] == null)
                return Pointer.Zero;
    
            var table_index = TableIndex(addr);
            var virtualAddr = Directory[directory_index][table_index];

            if (virtualAddr == Pointer.Zero)
                return Pointer.Zero;
    
            virtualAddr += Arch.ArchDefinition.PageOffset(addr.Value.ToInt32());

            return virtualAddr;
        }

        public void Add(UserPtr userAddress, Pointer virtualAddr)
        {
            Utils.Assert(virtualAddr != Pointer.Zero);
            var directory_index = DirectoryIndex(userAddress);
            var table_index = TableIndex(userAddress);

            var table = GetOrCreateTable(directory_index);

            Utils.Assert(table[table_index] == Pointer.Zero);
            table[table_index] = virtualAddr;
        }

        public void Remove(AddressSpace parent, UserPtr startPage, UserPtr endPage)
        {
            Utils.Assert(Arch.ArchDefinition.PageOffset(startPage.Value.ToUInt32()) == 0);
            Utils.Assert(Arch.ArchDefinition.PageOffset(endPage.Value.ToUInt32()) == 0);

            for (var page = startPage; page < endPage; page += Arch.ArchDefinition.PageSize)
            {
                var directory_index = DirectoryIndex(page);
                var table_index = TableIndex(page);
                var table = Directory[directory_index];
                if (table == null)
                    continue;

                if (table[table_index] == Pointer.Zero)
                    continue;

                FreePhysicalPage(table[table_index]);
                table[table_index] = Pointer.Zero;
            }

            Arch.NativeMethods.l4api_flush_regions(parent.impl._value, startPage.Value, endPage.Value, (int)MemoryRegion.FAULT_MASK);
        }


        public void Dump()
        {
#if false
            var r = this.next;
            while (r != null)
            {
                Arch.Console.Write(r.userAddress.Value.ToUInt32());
                Arch.Console.Write("(");
                Arch.Console.Write(r.virtualAddress.ToUInt32());
                Arch.Console.Write(")");

                if (r.next != null)
                    Arch.Console.Write(" -> ");

                r = r.next;
            }
            Arch.Console.WriteLine();
#endif
        }

        public bool SanityCheck()
        {
            return true;
        }

        private static int TableIndex(UserPtr addr)
        {
            return (int)((addr.Value.ToUInt32() & PGT_IDX_MASK) >> Arch.ArchDefinition.PageShift);
        }

        private static int DirectoryIndex(UserPtr addr)
        {
            return (int)(addr.Value.ToUInt32() >> (Arch.ArchDefinition.PageShift + PGT_SHIFT));
        }

        private PageTable GetOrCreateTable(int directory_index)
        {
            if (Directory[directory_index] == null)
                Directory[directory_index] = new PageTable(directory_index);

            return Directory[directory_index];
        }

        private static void FreePhysicalPage(Pointer page)
        {
            if (Globals.PageAllocator.Contains(page))
                Globals.PageAllocator.FreePage(page);
            else
                Globals.LinuxMemoryAllocator.Free(page);
        }
    }
}
