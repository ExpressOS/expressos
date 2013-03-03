
namespace ExpressOS.Kernel
{
    public sealed class BinderCompletion : ThreadCompletionEntryWithBuffer
    {
        internal readonly sys_binder_write_desc desc;
        public readonly UserPtr userBwrBuf;
        
        internal BinderCompletion(Thread current, UserPtr userBwrBuf, sys_binder_write_desc desc, ByteBufferRef buf)
            : base(current, Kind.BinderCompletionKind, buf)
        {
            this.userBwrBuf = userBwrBuf;
            this.desc = desc;
        }
    }
}
