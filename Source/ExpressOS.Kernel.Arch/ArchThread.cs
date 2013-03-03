
namespace ExpressOS.Kernel.Arch
{
    public class ArchThread
    {
        public readonly ThreadInfo _value;

        private ArchThread(ThreadInfo value)
        {
            this._value = value;
        }

        public static ArchThread Create(ArchAddressSpace parent)
        {
            var utcb = parent.AllocUTCB();
            if (utcb == Pointer.Zero)
                return null;
            
            ThreadInfo info;

            if (NativeMethods.l4api_create_thread(utcb, parent._value, out info) != 0)
                return null;
            
            return new ArchThread(info);
        }

        public void Start(Pointer ip, Pointer sp)
        {
            NativeMethods.l4api_start_thread(_value.thread, ip, sp);
        }

        public void Destroy()
        {
            NativeMethods.l4api_delete_thread(_value);
        }
    }
}
