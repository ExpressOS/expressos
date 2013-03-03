
namespace ExpressOS.Kernel
{
    public class TimerQueueNode
    {
        public ulong clock;
        public Thread thr;
        public TimerQueueNode prev, next;
        public TimerQueueNode(ulong clock, Thread thr)
        {
            this.clock = clock;
            this.thr = thr;
        }

        public static TimerQueueNode CreateSentinal()
        {
            return new TimerQueueNode(0, null);
        }

        public void Cancel()
        {
            Unlink();
            thr = null;
        }

        internal void Unlink()
        {
            if (prev != null)
                prev.next = next;

            if (next != null)
                next.prev = prev;

            prev = next = null;
        }
    }

    public class TimerQueue
    {
        TimerQueueNode list;

        public TimerQueue()
        {
            list = TimerQueueNode.CreateSentinal();
        }

        public Arch.Timeout NextRecvTimeout()
        {
            var currentTime = Arch.NativeMethods.l4api_get_system_clock();
            var r = list.next;
            if (r == null) {
                return Arch.Timeout.Never;
            }
            else if (r.clock <= currentTime)
            {
                return Arch.Timeout.RecvZero;
            }
            else {
                return new Arch.Timeout(0, (uint)(r.clock - currentTime));
            }
        }

        public Thread Take()
        {
            var r = list.next;
            r.Unlink();
            return r.thr;
        }

        public TimerQueueNode Enqueue(ulong timeout, Thread thr)
        {
            var currentTime = Arch.NativeMethods.l4api_get_system_clock();
            var node = new TimerQueueNode(currentTime + timeout, thr);

            var r = list.next;
            var prev = list;
            while (r != null && r.clock < node.clock)
            {
                prev = r;
                r = r.next;
            }

            node.next = prev.next;
            node.prev = prev;
            
            if (prev.next != null)
                prev.next.prev = node;
            prev.next = node;

            return node;
        }
    }
}
