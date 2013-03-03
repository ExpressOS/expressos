using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public static class Pager
    {
        public static void HandlePageFault(Process process, uint faultType, Pointer faultAddress, Pointer faultIP, out Pointer physicalPage, out uint permission)
        {
            // Object invariants of Process
            Contract.Assume(process.Space.GhostOwner == process);
            Contract.Assume(process.Space.Head.GhostOwner == process);

            // Never map page 0
            if (faultAddress.ToUInt32() < Arch.ArchDefinition.PageSize)
            {
                physicalPage = Pointer.Zero;
                permission = MemoryRegion.FALUT_NONE;
                return;
            }

            SyscallProfiler.EnterPageFault();
            var space = process.Space;
            var region = space.Find(faultAddress);

            if (region != null && (faultType & region.Access) != 0)
            {
                /*
                 * Check whether the kernel has allocated a page for this address, 
                 * which might be the case due to UserPtr.Read() / UserPtr.Write().
                 */
                var mapped_in_page = space.UserToVirt(new UserPtr(faultAddress));

                if (mapped_in_page != Pointer.Zero)
                {
                    physicalPage = PageIndex(mapped_in_page);
                    permission = region.Access & MemoryRegion.FAULT_MASK;
                    return;
                }

                // If the page is a shared page from Linux, we'll need to ask linux to grab the page
                // Otherwise let's just allocate a fresh one.

                var shared_memory_region = IsAlienSharedRegion(region);
                var ghost_page_from_fresh_memory = false;

                ByteBufferRef buf;
                if (shared_memory_region)
                {
                    buf = Globals.LinuxMemoryAllocator.GetUserPage(process, faultType, ToShadowProcessAddress(faultAddress, region));
                    if (!buf.isValid)
                    {
                        Arch.Console.WriteLine("pager: cannot map in alien page.");
                        space.DumpAll();
                        physicalPage = Pointer.Zero;
                        permission = MemoryRegion.FALUT_NONE;
                        return;
                    }

                }
                else
                {
                    buf = Globals.PageAllocator.AllocPage();

                    ghost_page_from_fresh_memory = true;

                    if (!buf.isValid)
                    {
                        Arch.Console.WriteLine("Cannot allocate new pages");
                        Utils.Panic();
                    }

                    if (region.BackingFile != null)
                    {
                        var rel_pos = (PageIndex(faultAddress) - region.StartAddress);
                        uint pos = (uint)((ulong)rel_pos + region.FileOffset);

                        var readSizeLong = region.FileSize - rel_pos;
                        if (readSizeLong < 0)
                            readSizeLong = 0;
                        else if (readSizeLong > Arch.ArchDefinition.PageSize)
                            readSizeLong = Arch.ArchDefinition.PageSize;

                        int readSize = (int)readSizeLong;

                        Contract.Assert(region.BackingFile.GhostOwner == process);

                        var r = region.BackingFile.Read(buf, 0, readSize, ref pos);
                        if (r < 0)
                            r = 0;

                        Utils.Assert(r <= Arch.ArchDefinition.PageSize);

                        if (r < Arch.ArchDefinition.PageSize)
                            buf.ClearAfter(r);
                    }
                    else
                    {
                        buf.Clear();
                    }
                }

                Contract.Assert(shared_memory_region ^ ghost_page_from_fresh_memory);

                var page = new Pointer(buf.Location);
                space.AddIntoWorkingSet(new UserPtr(PageIndex(faultAddress)), page);

                SyscallProfiler.ExitPageFault();
                physicalPage = page;
                permission = region.Access & MemoryRegion.FAULT_MASK;
                return;
            }
            else
            {
                /*
                 * TODO: mmap2 enables the application requests an automatically expandable
                 * region (e.g., a stack)
                 * 
                 * The feature doesn't seem to be actively used by the applications, since
                 * both the C runtime and the pthread library initializes stack explicitly.
                 * 
                 * The feature is currently unimplemented.
                 */
            }

            physicalPage = Pointer.Zero;
            permission = MemoryRegion.FALUT_NONE;
            return;
        }

        private static bool IsAlienSharedRegion(MemoryRegion region)
        {
            if ((region.Flags & Memory.MAP_SHARED) == 0)
                return false;

            if (region.BackingFile == null)
                return false;

            return region.BackingFile.inode.kind == GenericINode.INodeKind.BinderSharedINodeKind
                || region.BackingFile.inode.kind == GenericINode.INodeKind.AshmemINodeKind
                || region.BackingFile.inode.kind == GenericINode.INodeKind.ScreenBufferINodeKind;
        }

        private static Pointer ToShadowProcessAddress(Pointer faultAddress, MemoryRegion region)
        {
            var f = region.BackingFile;
            Pointer vaddr = Pointer.Zero;
            switch (f.inode.kind)
            {
                case GenericINode.INodeKind.BinderSharedINodeKind:
                case GenericINode.INodeKind.ScreenBufferINodeKind:
                case GenericINode.INodeKind.AshmemINodeKind:
                    vaddr = f.inode.AlienSharedMemoryINode.vaddrInShadowProcess;
                    break;

                default:
                    Arch.ArchDefinition.Panic();
                    break;
            }
            return vaddr + (faultAddress - region.StartAddress);
        }

        public static Pointer PageIndex(Pointer addr)
        {
            return addr & Arch.ArchDefinition.PageIndexMask;
        }

        public static UserPtr PageIndex(UserPtr addr)
        {
            return addr & Arch.ArchDefinition.PageIndexMask;
        }
    }
}
