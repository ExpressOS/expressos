using System.Diagnostics.Contracts;
namespace ExpressOS.Kernel
{
    public class CompletionQueue
    {
        GenericCompletionEntry Head;
        /*
         * Handle ID available completion other than ThreadCompletion.
         * The last 12 bits must not be zero to avoid conflicts with
         * thread handles.
         */
        private uint freeHandle;

        public CompletionQueue()
        {
            Head = null;
            freeHandle = 1;
        }

        public void Enqueue(GenericCompletionEntry e)
        {
            e.next = Head;
            Head = e;
        }

        public void ClearAllPendingCompletion(uint handle)
        {
            while (Take(handle) != null) ;
        }

        public GenericCompletionEntry Take(uint handle)
        {
            if (Head == null)
                return null;

            if (Head.handle == handle)
            {
                var r = Head;
                Head = Head.next;
                return r;
            }

            var prev = Head;
            var h = Head.next;
            while (h != null && h.handle != handle)
            {
                prev = h;
                h = h.next;
            }

            if (h == null)
                return null;

            prev.next = h.next;
            h.next = null;
            return h;
        }

        internal uint NextFreeHandle()
        {
            freeHandle += 2;
            return freeHandle;
        }
    }

    public class ThreadCompletionEntry : GenericCompletionEntry
    {
        public readonly Thread thr;
        public ThreadCompletionEntry(Thread current, Kind kind)
            : base(kind, current == null ? 0 : current.impl._value.thread._value)
        {
            this.thr = current;
        }
    }

    public class ThreadCompletionEntryWithBuffer : ThreadCompletionEntry
    {
        internal readonly ByteBufferRef buf;
        internal ThreadCompletionEntryWithBuffer(Thread current, Kind kind, ByteBufferRef buf)
            : base(current, kind)
        {
            Contract.Ensures(this.buf.Length == buf.Length);
            Contract.Ensures(this.buf.Location == buf.Location);
            this.buf = buf;
        }

        internal void Dispose()
        {
            if (buf.isValid)
                Globals.CompletionQueueAllocator.FreePages(new Pointer(buf.Location), buf.Length >> Arch.ArchDefinition.PageShift);
        }
    }

    public class GenericCompletionEntry
    {
        public enum Kind
        {
            BinderCompletionKind,
            PollCompletionKind,
            IOCompletionKind,
            SelectCompletionKind,
            SleepCompletionKind,
            FutexCompletionKind,
            VBinderCompletionKind,
            BridgeCompletionKind,
            SocketCompletionKind,
            GetSocketParamCompletionKind,
            OpenFileCompletionKind,
            SFSFlushCompletionKind,
        }

        public readonly Kind kind;
        internal readonly uint handle;
        internal GenericCompletionEntry next;

        protected GenericCompletionEntry(Kind kind, uint handle)
        {
            Contract.Ensures(this.kind == kind);
            Contract.Ensures(this.handle == handle);

            this.kind = kind;
            this.handle = handle;
        }

        public BinderCompletion BinderCompletion
        { get { return kind == Kind.BinderCompletionKind ? (BinderCompletion)this : null; } }
        public PollCompletion PollCompletion
        { get { return kind == Kind.PollCompletionKind ? (PollCompletion)this : null; } }
        public IOCompletion IOCompletion
        { get { return kind == Kind.IOCompletionKind ? (IOCompletion)this : null; } }
        public SelectCompletion SelectCompletion
        { get { return kind == Kind.SelectCompletionKind ? (SelectCompletion)this : null; } }
        public FutexCompletionEntry FutexCompletion
        { get { return kind == Kind.FutexCompletionKind ? (FutexCompletionEntry)this : null; } }
        public VBinderCompletion VBinderCompletion
        { get { return kind == Kind.VBinderCompletionKind ? (VBinderCompletion)this : null; } }
        public BridgeCompletion BridgeCompletion
        { get { return kind == Kind.BridgeCompletionKind ? (BridgeCompletion)this : null; } }
        public GetSockParamCompletion GetSockParamCompletion
        { get { return kind == Kind.GetSocketParamCompletionKind ? (GetSockParamCompletion)this : null; } }
        public OpenFileCompletion OpenFileCompletion
        { get { return kind == Kind.OpenFileCompletionKind ? (OpenFileCompletion)this : null; } }
        public SFSFlushCompletion SFSFlushCompletion
        { get { return kind == Kind.SFSFlushCompletionKind ? (SFSFlushCompletion)this : null; } }
        public SocketCompletion SocketCompletion
        { get { return kind == Kind.SocketCompletionKind ? (SocketCompletion)this : null; } }

        public ThreadCompletionEntry ThreadCompletionEntry
        {
            get
            {
                switch (kind)
                {
                    case Kind.BinderCompletionKind:
                    case Kind.PollCompletionKind:
                    case Kind.IOCompletionKind:
                    case Kind.SelectCompletionKind:
                    case Kind.SleepCompletionKind:
                    case Kind.FutexCompletionKind:
                    case Kind.VBinderCompletionKind:
                    case Kind.BridgeCompletionKind:
                    case Kind.SocketCompletionKind:
                    case Kind.GetSocketParamCompletionKind:
                    case Kind.OpenFileCompletionKind:
                        return (ThreadCompletionEntry)this;
                    default:
                        return null;
                }
            }
        }

    }

    public sealed class SleepCompletion : ThreadCompletionEntry
    {
        public SleepCompletion(Thread current)
            : base(current, Kind.SleepCompletionKind)
        { }
    }

    public sealed class SocketCompletion : ThreadCompletionEntry
    {
        public SocketCompletion(Thread current)
            : base(current, Kind.SocketCompletionKind)
        { }
    }
}
