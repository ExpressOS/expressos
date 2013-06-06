using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    internal class VBinderMessageBuffer
    {
        private readonly VBinderMessage[] data;
        private uint first;
        private uint len;
        public readonly Thread GhostOwner;

        [ContractInvariantMethod]
        private void ObjectInvariantMethod()
        {
            Contract.Invariant(data != null);
            Contract.Invariant(first >= 0 && len >= 0);
            Contract.Invariant(len <= data.Length);
            Contract.Invariant(first < data.Length);
            Contract.Invariant(data.Length > 0);
        }

        internal VBinderMessageBuffer(uint capacity, Thread owner)
        {
            Contract.Requires(capacity > 0);
            Contract.Ensures(GhostOwner == owner);

            this.data = new VBinderMessage[capacity];
            this.first = 0;
            this.len = 0;
            this.GhostOwner = owner;
        }

        public void Clear()
        {
            len = 0;
            var i = 0;
            while (i < data.Length)
            {
                data[i] = null;
                ++i;
            }
        }

        public VBinderMessage Head()
        {
            Contract.Requires(!IsEmpty());
            return data[first];
        }

        [Pure]
        public bool IsEmpty()
        {
            return len == 0;
        }

        public void Enqueue(VBinderMessage x)
        {
            Contract.Requires(x != null && x.GhostTarget == GhostOwner);

            if (len == data.Length)
            {
                Arch.Console.WriteLine("VBinderMessage overflow");
                Utils.Panic();
                return;
            }

            
            var nextEmpty = first + len < data.Length ? first + len : first + len - data.Length;
            data[nextEmpty] = x;
            ++len;
        }

        public VBinderMessage Dequeue()
        {
            Contract.Requires(!IsEmpty());
            Contract.Ensures(Contract.Result<VBinderMessage>() != null && Contract.Result<VBinderMessage>().GhostTarget == GhostOwner);

            var x = data[first];
            data[first++] = null;
            if (first == data.Length)
                first = 0;
            --len;

            // Proven by Dafny
            Contract.Assume(x != null && x.GhostTarget == GhostOwner);
            return x;
        }

    }
}
