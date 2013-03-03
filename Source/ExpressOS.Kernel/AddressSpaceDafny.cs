using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public partial class AddressSpace
    {
        public MemoryRegion Head;
        public readonly Process GhostOwner;

        void RemoveNode(MemoryRegion prev, MemoryRegion r)
        {
            prev.Next = r.Next;
        }

        void InsertNode(MemoryRegion prev, MemoryRegion r)
        {
            r.Next = prev.Next;
            prev.Next = r;
        }

        bool TryMergeWithNext(MemoryRegion r)
        {
            var next = r.Next;
            if (next != null && MemoryRegion.CanMerge(r, next))
            {
                RemoveNode(r, next);
                r.Expand(next);
                return true;
            }
            else
            {
                return false;
            }
        }

        void InsertOrMerge(MemoryRegion prev, MemoryRegion r, MemoryRegion next)
        {
            if (MemoryRegion.CanMerge(prev, r))
            {
                prev.Expand(r);
                return;
            }

            InsertNode(prev, r);
            TryMergeWithNext(r);
        }

        public MemoryRegion Find(Pointer address)
        {
            Contract.Ensures(Contract.Result<MemoryRegion>() == null
                || Contract.Result<MemoryRegion>().GhostOwner == GhostOwner);

            Contract.Ensures(Contract.Result<MemoryRegion>() == null
                || Contract.Result<MemoryRegion>().BackingFile == null
                || Contract.Result<MemoryRegion>().BackingFile.GhostOwner == GhostOwner);

            Contract.Ensures(Contract.Result<MemoryRegion>() == null || Contract.Result<MemoryRegion>().GhostOwner == GhostOwner);

            var h = Head;
            while (h != null)
            {
                // Object invariant of h
                // To be supported in next release of code contract
                // See http://social.msdn.microsoft.com/Forums/en-US/codecontracts/thread/17f9af7a-849f-4c91-93b4-95a98763d080
                //
                Contract.Assume(h.BackingFile == null || h.BackingFile.GhostOwner == h.GhostOwner);

                //
                // Property of the container
                //
                Contract.Assume(h.GhostOwner == GhostOwner);

                if (h.StartAddress <= address && address < h.StartAddress + h.Size)
                {
                    return h;
                }
                h = h.Next;
            }
            return null;
        }

        void Insert(MemoryRegion r)
        {
            Contract.Requires(r != null && r.GhostOwner == GhostOwner);

            var h = Head.Next;
            var prev = Head;

            while (h != null && h.End <= r.StartAddress)
            {
                prev = h;
                h = h.Next;
            }
            InsertOrMerge(prev, r, h);
            return;
        }

        //
        // Separate region into two. It returns the rightmost part of the region
        //
        MemoryRegion Split(MemoryRegion r, int offset)
        {
            Contract.Requires(r.BackingFile == null || r.BackingFile.GhostOwner == r.GhostOwner);
            //Contract.Ensures(Contract.Result<MemoryRegion>().BackingFile.GhostOwner == Contract.Result<MemoryRegion>().GhostOwner);

            MemoryRegion next;
            if (offset >= r.FileSize)
            {
                next = new MemoryRegion(r.GhostOwner, r.Access, r.Flags, null, 0, 0, r.StartAddress + offset, r.Size - offset, r.IsFixed);
            }
            else
            {
                next = new MemoryRegion(r.GhostOwner, r.Access, r.Flags, r.BackingFile, (uint)(r.FileOffset + (uint)offset),
                    (int)(r.FileSize - offset), r.StartAddress + offset, r.Size - offset, r.IsFixed);
            }

            r.CutRight(r.Size - offset);
            InsertNode(r, next);
            return next;

        }

        void UpdateAccessRights(MemoryRegion r, uint newaccess)
        {
            r.UpdateAccessRights(this, newaccess);
        }

        /*
         * Create a new memory region in the address space.
         * 
         * If there are any overlaps between the current address space and the requested one,
         * this function will unamp the overlapped portions of the address space before
         * mapping in the new memory region.
         * 
         * Several clients, including the dynamic linker relies on this feature. See mmap(2)
         * for details.
         * 
         * This function requires vaddr and memorySize are aligned to the page boundary.
         */
        internal int AddMapping(uint access, int flags, File file, uint fileOffset, int fileSize, Pointer vaddr, int memorySize)
        {
            Contract.Requires(file == null || file.GhostOwner == GhostOwner);
            Contract.Requires(0 <= fileSize && fileSize <= memorySize);
            Contract.Requires(file == null || fileSize > 0);
            Contract.Requires(file != null || (fileSize == 0 && fileOffset == 0));

            if (memorySize <= 0 || Arch.ArchDefinition.PageOffset(memorySize) != 0)
                return -ErrorCode.EINVAL;

            var diff = Arch.ArchDefinition.PageOffset(vaddr.ToUInt32());
            if (diff != 0)
                return -ErrorCode.EINVAL;

            var r = RemoveMapping(vaddr, memorySize);
            if (r != 0)
            {
                return r;
            }

            var newRegion = new MemoryRegion(GhostOwner, access, flags, file, fileOffset, fileSize, vaddr, memorySize, false);
            Insert(newRegion);
            return 0;
        }

        internal bool UpdateAccessRightRange(Pointer start, int size, uint access)
        {
            if (Arch.ArchDefinition.PageOffset(start.ToUInt32()) != 0)
                return false;

            var prev = Head;
            var r = prev.Next;
            var end = start + size;

            while (r != null && r.StartAddress < end)
            {
                if (!r.OverlappedInt(start, size) || access == r.Access)
                {
                    prev = r;
                    r = r.Next;
                }
                else
                {
                    if (r.IsFixed)
                    {
                        return false;
                    }

                    if (r.StartAddress < start)
                    {
                        var region_end = r.End;
                        var middleRegion = Split(r, start - r.StartAddress);

                        if (end < region_end)
                        {
                            // update middle region
                            prev = Split(middleRegion, size);
                            UpdateAccessRights(middleRegion, access);
                            return true;
                        }
                        else
                        {
                            UpdateAccessRights(middleRegion, access);
                            TryMergeWithNext(middleRegion);
                            prev = middleRegion;
                            r = prev.Next;
                        }
                    }
                    else
                    {
                        if (r.End <= end)
                        {
                            UpdateAccessRights(r, access);
                            TryMergeWithNext(r);
                            var merged = TryMergeWithNext(prev);
                            if (merged)
                            {
                                r = prev.Next;
                            }
                            else
                            {
                                prev = prev.Next;
                                r = prev.Next;
                            }
                        }
                        else
                        {
                            var right_region = Split(r, end - r.StartAddress);
                            UpdateAccessRights(r, access);
                            var merged = TryMergeWithNext(prev);
                            prev = right_region;
                            r = prev.Next;
                        }
                    }
                }
            }
            return true;
        }

        internal int RemoveMapping(Pointer vaddr, int size)
        {
            Contract.Requires(size > 0);

            if (Arch.ArchDefinition.PageOffset(size) != 0 || Arch.ArchDefinition.PageOffset(vaddr.ToUInt32()) != 0)
                return -ErrorCode.EINVAL;

            MemoryRegion prev;
            var end = vaddr + size;
            var c = false;
            var changed = false;
            int ret;

            RemoveMappingLeft(vaddr, size, out ret, out c, out prev);
            changed |= c;
            if (ret <= 0)
            {
                if (changed) { RemoveWorkingSet(vaddr, size); }
                return ret;
            }

            RemoveMappingCenter(vaddr, size, ref prev, out ret, out c);
            changed |= c;

            if (ret <= 0)
            {
                if (changed) { RemoveWorkingSet(vaddr, size); }
                return ret;
            }
            var r = prev.Next;
            changed = true;
            var s = end - r.StartAddress;
            r.CutLeft(s);
            prev = r;
            r = r.Next;
            RemoveWorkingSet(vaddr, size);
            return 0;
        }

        private void RemoveMappingLeft(Pointer vaddr, int size, out int ret, out bool changed, out MemoryRegion prev)
        {
            prev = Head;
            var r = prev.Next;
            var end = vaddr + size;

            if (Head.OverlappedInt(vaddr, size))
            {
                ret = -ErrorCode.EINVAL;
                changed = false;
                return;
            }

            while (r != null && r.End <= vaddr)
            {
                prev = r;
                r = r.Next;
            }

            // No overlaps
            if (r == null || r.StartAddress >= end)
            {
                ret = 0;
                changed = false;
                return;
            }

            if (r.IsFixed)
            {
                ret = -ErrorCode.EINVAL;
                changed = false;
                return;
            }

            if (r.StartAddress < vaddr)
            {
                if (end < r.End)
                {
                    var offset = vaddr - r.StartAddress;
                    var middleRegion = Split(r, offset);
                }
                else
                {
                    r.CutRight(r.End - vaddr);
                }
                prev = r;
                r = r.Next;
                ret = 1;
                changed = true;
                return;
            }
            ret = 1;
            changed = false;
            return;
        }

        private void RemoveMappingCenter(Pointer vaddr, int size, ref MemoryRegion prev, out int ret, out bool changed)
        {
            changed = false;
            var r = prev.Next;
            var end = vaddr + size;
            while (r != null && !r.IsFixed && r.End <= end)
            {
                changed = true;
                RemoveNode(prev, r);
                r = prev.Next;
            }
            if (r != null && r.End <= end && r.IsFixed)
            {
                ret = -ErrorCode.EINVAL;
                return;
            }

            if (r == null || r.StartAddress >= end)
            {
                ret = 0;
                return;
            }
            ret = 1;
            return;
        }
    }
}
