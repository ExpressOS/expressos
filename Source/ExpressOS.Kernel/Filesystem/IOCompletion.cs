namespace ExpressOS.Kernel
{
    public sealed class IOCompletion : ThreadCompletionEntryWithBuffer
    {
        public enum Type
        {
            Read,
            Write,
        }

        public Type type;
        public UserPtr userBuf;
        public int len;
        public File posToUpdate;
        
        public IOCompletion(Thread current, Type type, UserPtr userBuf, int len, File file, ByteBufferRef buf)
            : base(current, Kind.IOCompletionKind, buf)
        {
            this.type = type;
            this.userBuf = userBuf;
            this.len = len;
            this.posToUpdate = file;
        }

        internal static IOCompletion CreateReadIOCP(Thread current, UserPtr userBuf, int len, File file, ByteBufferRef buf)
        {
            return new IOCompletion(current, Type.Read, userBuf, len, file, buf);
        }

        internal static IOCompletion CreateWriteIOCP(Thread current, File file, ByteBufferRef buf)
        {
            return new IOCompletion(current, Type.Write, UserPtr.Zero, 0, file, buf);
        }
    }
}
