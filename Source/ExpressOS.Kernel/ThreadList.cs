

namespace ExpressOS.Kernel
{
    public class ThreadList
    {
        public readonly Thread thr;
        ThreadList next;

        public static ThreadList CreateSentinal()
        {
            var tl = new ThreadList(null);
            return tl;
        }

        private ThreadList(Thread thr)
        {
            this.thr = thr;
        }

        public void Add(Thread thr)
        {
            var tl = new ThreadList(thr);
            tl.next = this.next;
            this.next = tl;
        }

        public Thread Lookup(Arch.L4Handle handle)
        {
            var h = handle._value;
            var t = next;

            while (t != null && t.thr.impl._value.thread._value != h)
                t = t.next;

            return t == null ? null : t.thr;
        }

        internal void Remove(Thread thread)
        {
            var prev = this;
            ThreadList tl = this.next;
            while (tl != null && tl.thr != thread)
            {
                prev = tl;
                tl = tl.next;
            }

            if (tl != null)
                prev.next = tl.next;
        }
    }
}
