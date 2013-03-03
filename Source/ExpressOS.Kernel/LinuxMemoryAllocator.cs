
using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public class LinuxMemoryAllocator
    {
        private const uint MAX_FREE_PAGES_NUM = 64;
        private uint FreePageCounts;
        private Pointer[] FreedPages;

        [ContractInvariantMethod]
        private void ObjectInvariantMethod()
        {
            Contract.Invariant(FreedPages.Length == MAX_FREE_PAGES_NUM);
            Contract.Invariant(FreePageCounts <= MAX_FREE_PAGES_NUM);
        }

        public ByteBufferRef GetUserPage(Process process, uint faultType, Pointer shadowAddress)
        {
            var p = Arch.IPCStubs.linux_sys_get_user_page(process.helperPid, faultType, shadowAddress);
            return new ByteBufferRef(p.ToIntPtr(), Arch.ArchDefinition.PageSize);
        }

        public void Free(Pointer addr)
        {
            if (FreePageCounts == MAX_FREE_PAGES_NUM)
            {
                Arch.IPCStubs.linux_sys_free_linux_pages(FreedPages);
                FreePageCounts = 0;
            }

            FreedPages[FreePageCounts++] = addr;
        }

        public LinuxMemoryAllocator()
        {
            FreePageCounts = 0;
            FreedPages = new Pointer[MAX_FREE_PAGES_NUM];
        }
    }
}
