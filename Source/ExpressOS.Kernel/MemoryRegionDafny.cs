using System.Diagnostics.Contracts;
namespace ExpressOS.Kernel
{
   /*
    * Verified implementation of MemoryRegion, translated from Dafny code.
    * 
    * There is one deviation from the implementation of Dafny. The C#
    * implementation also tracks the reference count of the File object.
    * The tracking cannot invalidate any proofs about MemoryRegion, or
    * AddressSpace, because ExpressOS has a strong type system, and the
    * properties of MemoryRegion / AddressSpace does not involve the File
    * objects.
    * 
    * The challenge of verifying it in Dafny is that File objects might be
    * shared among multi MemoryRegion objects, where currently the proofs
    * require the representation of MemoryRegion (i.e., all objects reachable
    * from the instance, see the Dafny paper for more details) to be totally
    * isolated. Automatically verifying this case, is still an open research
    * problem.
    * 
    * Another thing to notice is that the reference counting is not strictly
    * necessary -- a GC combined with a compiler that implements the IDisposable
    * interface correctly is the correct way to handle this problem.
    */
    public sealed partial class MemoryRegion
    {
        public Pointer StartAddress;
        public Pointer End { get { return StartAddress + Size; } }
        public int Size;
        public int Flags;
        public uint Access { get; private set; }

        public File BackingFile { get; private set; }
        public ulong FileOffset { get; private set; }
        public long FileSize;
        private ulong FileEnd { get { return FileOffset + (ulong)FileSize; } }

        public MemoryRegion Next;
        public readonly bool IsFixed;
        public bool IsSpecial { get { return Access == 0; } }

        public readonly Process GhostOwner;

        [ContractInvariantMethod]
        private void ObjectInvariantMethod()
        {
            Contract.Invariant(BackingFile == null || BackingFile.GhostOwner == GhostOwner);
        }

        internal MemoryRegion(Process owner, uint access, int flags, File file, uint fileOffset, int fileSize, Pointer vaddr, int size, bool isFixed)
        {
            Contract.Requires(file == null || file.GhostOwner == owner);
            Contract.Ensures(GhostOwner == owner);

            this.Access = access;
            this.Flags = flags;
            this.BackingFile = file;
            this.FileOffset = fileOffset;
            this.FileSize = fileSize;
            this.StartAddress = vaddr;
            this.Size = size;
            this.Next = null;
            this.IsFixed = isFixed;
            this.GhostOwner = owner;

            if (file != null)
                file.inode.IncreaseRefCount();
        }

        public bool Overlapped(MemoryRegion rhs)
        {
            return OverlappedInt(rhs.StartAddress, rhs.Size);
        }

        public bool OverlappedInt(Pointer start, int sz)
        {
            var end = start + sz;
            return !(End <= start || end <= StartAddress);
        }

        internal void CutRight(int size)
        {
            Size -= size;
            if (FileSize > 0)
            {
                FileSize = size;
            }
        }

        internal void CutLeft(int size)
        {
            FileOffset += (uint)size;
            StartAddress += size;
            Size -= (int)size;
            if (FileSize <= size)
            {
                FileSize = 0;
                FileOffset = 0;
                BackingFile = null;
            }
            else
            {
                FileSize = FileSize - size;
            }
        }

        internal void Expand(MemoryRegion r)
        {
            Size += r.Size;
            if (BackingFile != null)
                FileSize += r.FileSize;
        }

        [Pure]
        internal static bool CanMerge(MemoryRegion prev, MemoryRegion next)
        {
            var same_inode = (prev.BackingFile == next.BackingFile ||
                (prev.BackingFile != null && next.BackingFile != null
                && prev.BackingFile.inode == next.BackingFile.inode));

            return same_inode
                    && prev.Access == next.Access
                    && prev.End == next.StartAddress
                    && (prev.BackingFile == null || prev.FileEnd == next.FileOffset);
        }

    }
}
