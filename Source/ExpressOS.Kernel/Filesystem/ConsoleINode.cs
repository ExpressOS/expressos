namespace ExpressOS.Kernel
{
    internal sealed class ConsoleINode : GenericINode
    {
        internal static ConsoleINode instance;
        internal static ConsoleINode Instance
        {
            get
            {
                if (instance == null)
                    instance = new ConsoleINode();
                return instance;
            }
        }

        ConsoleINode()
            : base(INodeKind.ConsoleINodeKind)
        { }
   
        internal int WriteImpl(Thread current, ByteBufferRef buf, int len, ref uint pos)
        {
            for (var i = 0; i < len; ++i)
                Arch.Console.Write((char)buf[i]);

            Arch.Console.Flush();
            return len;
        }
    }
}
