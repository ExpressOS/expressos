namespace ExpressOS.Kernel
{
    public sealed class FutexCompletionEntry : ThreadCompletionEntry
    {
        public AddressSpace Space
        {
            get
            {
                return thr.Parent.Space;
            }
        }

        public readonly UserPtr uaddr;
        public TimerQueueNode timeoutNode;
        public uint bitset;
        public FutexCompletionEntry prevFutex, nextFutex;

        public FutexCompletionEntry(Thread current, UserPtr uaddr, uint bitset)
            : base(current, Kind.FutexCompletionKind)
        {
            this.uaddr = uaddr;
            this.bitset = bitset;
            this.timeoutNode = null;
        }

        public static FutexCompletionEntry CreateSentinal()
        {
            var e = new FutexCompletionEntry(null, UserPtr.Zero, 0);
            e.nextFutex = e;
            e.prevFutex = e;
            return e;
        }

        public void Unlink()
        {
            if (prevFutex != null)
                prevFutex.nextFutex = nextFutex;

            if (nextFutex != null)
                nextFutex.prevFutex = prevFutex;

            nextFutex = prevFutex = null;
        }

        internal void InsertAtTail(FutexCompletionEntry n)
        {
            var tail = this.prevFutex;

            n.nextFutex = tail.nextFutex;
            n.prevFutex = tail;
            tail.nextFutex.prevFutex = n;
            tail.nextFutex = n;
        }
    }
}
