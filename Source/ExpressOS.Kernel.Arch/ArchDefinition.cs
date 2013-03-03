using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel.Arch
{
    public static class ArchDefinition
    {
        public const int PageShift = 12;
        public const int PageSize = 1 << PageShift;
        public const int PageIndexMask = ~(PageSize - 1);
        public const int UTCBOffset = PageSize;
        public const int UTCBSizeShift = PageShift;
        public const int MaxThreadPerTaskLog2 = 5;

        [Pure]
        public static uint PageIndex(uint addr)
        {
            return (uint)(addr & PageIndexMask);
        }

        [Pure]
        public static int PageOffset(int addr)
        {
            return (int)(addr & ~PageIndexMask);
        }

        [Pure]
        public static int PageOffset(uint addr)
        {
            Contract.Ensures(Contract.Result<int>() >= 0);
            Contract.Ensures(Contract.Result<int>() < PageSize);
            return (int)(addr & ~PageIndexMask);
        }

        [Pure]
        public static uint PageAlign(uint addr)
        {
            Contract.Ensures(Contract.Result<uint>() >= addr);
            Contract.Ensures(Contract.Result<uint>() % PageSize == 0);
            return (uint)((addr + PageSize - 1) & PageIndexMask);
        }

        [Pure]
        public static int PageAlign(int addr)
        {
            Contract.Ensures(Contract.Result<int>() >= addr);
            Contract.Ensures(Contract.Result<int>() % PageSize == 0);
            return ((addr + PageSize - 1) & PageIndexMask);
        }

        public static void Assert(bool condition)
        {
            Contract.Ensures(condition);

            if (!condition)
                Panic();

            // Runtime assertion, thus it is safe to assume it
            Contract.Assume(condition);
        }

        public static void Panic()
        {
            NativeMethods.panic();
        }
    }
}

