using System;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;

namespace ExpressOS.Kernel
{
    public class FreeListPageAllocator
    {
        private static class NativeMethods
        {
            [DllImport("glue")]
            internal static extern IntPtr sel4_alloc_new(Pointer start, Pointer end);
            [DllImport("glue")]
            internal static extern Pointer sel4_alloc_alloc(IntPtr handle, int size);
            [DllImport("glue")]
            internal static extern void sel4_alloc_free(IntPtr handle, Pointer page, int size);
        }

        private IntPtr handle;
        private Pointer Start;
        private Pointer End;

        public void Initialize(Pointer start, int num_of_pages)
        {
            this.handle = NativeMethods.sel4_alloc_new(start, start + num_of_pages * Arch.ArchDefinition.PageSize);
            this.Start = start;
            this.End = start + (num_of_pages << Arch.ArchDefinition.PageShift);
        }

        public ByteBufferRef AllocPage()
        {
            return AllocPages(1);
        }

        public ByteBufferRef AllocPages(int pages)
        {
            Contract.Ensures(!Contract.Result<ByteBufferRef>().isValid ||
                Contract.Result<ByteBufferRef>().Length == pages * Arch.ArchDefinition.PageSize);

            var size = pages * Arch.ArchDefinition.PageSize;
            var p = NativeMethods.sel4_alloc_alloc(this.handle, size);

            if (p == Pointer.Zero)
            {
                // Post-condition of ByteBufferRef.Empty
                Contract.Assume(!ByteBufferRef.Empty.isValid);
                return ByteBufferRef.Empty;
            }

            var r = new ByteBufferRef(p.ToIntPtr(), size);
            // Post-condition of ByteBufferRef
            Contract.Assume(r.Length == size);

            Contract.Assert(r.Length == pages * Arch.ArchDefinition.PageSize);
            return r;
        }

        public bool Contains(Pointer page)
        {
            return Start <= page && page < End;
        }

        public bool Contains(ByteBufferRef buf)
        {
            return Start <= new Pointer(buf.Location) && new Pointer(buf.Location) + buf.Length < End;
        }

        public void FreePage(Pointer page)
        {
            NativeMethods.sel4_alloc_free(handle, page, Arch.ArchDefinition.PageSize);
        }

        public void FreePages(Pointer start, int pages)
        {
            NativeMethods.sel4_alloc_free(handle, start, pages * Arch.ArchDefinition.PageSize);
        }
    }
}
