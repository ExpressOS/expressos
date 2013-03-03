
namespace ExpressOS.Kernel.Arch
{
    public class ArchAddressSpace
    {
        public L4Handle _value { get; private set; }
        readonly Pointer UTCBStart;
        readonly int utcb_num;
        int allocated_utcb;

        private ArchAddressSpace(L4Handle value, Pointer UTCBStart, int utcb_size_log2)
        {
            this._value = value;
            this.UTCBStart = UTCBStart;
            this.utcb_num = (1 << utcb_size_log2) / ArchDefinition.UTCBOffset;
            this.allocated_utcb = 0;
        }

        public Pointer AllocUTCB()
        {
            if (allocated_utcb >= utcb_num)
                return Pointer.Zero;

            var ptr = UTCBStart + (allocated_utcb * ArchDefinition.UTCBOffset);
            allocated_utcb++;

            return ptr;
        }

        public static ArchAddressSpace Create(ASCIIString name, Pointer utcb_start, int utcb_size_log2)
        {
            var handle = NativeMethods.l4api_create_task(name.GetByteString(), utcb_start, utcb_size_log2);
            return new ArchAddressSpace(handle, utcb_start, utcb_size_log2);
        }

    }
}
