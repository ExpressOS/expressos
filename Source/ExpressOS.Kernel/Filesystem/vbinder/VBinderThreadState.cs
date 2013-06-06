using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public class VBinderThreadState
    {
        CapabilityRef Capabilities;
        int CapAllocId;
        VBinderMessageBuffer MessageQueue;
        public readonly Thread Owner;
        public const uint Capacity = 64;
        internal VBinderCompletion Completion;

        [ContractInvariantMethod]
        private void ObjectInvariantMethod()
        {
            Contract.Invariant(Owner == MessageQueue.GhostOwner);
        }

        [Pure]
        public bool NoPendingMessages()
        {
            Contract.Ensures(Contract.Result<bool>() == MessageQueue.IsEmpty());
            return MessageQueue.IsEmpty();
        }

        internal VBinderMessage TakeMessage()
        {
            Contract.Requires(!NoPendingMessages());
            Contract.Ensures(Contract.Result<VBinderMessage>().GhostTarget == Owner);
            var msg = MessageQueue.Dequeue();
            return msg;
        }

        public void Enqueue(VBinderMessage msg)
        {
            Contract.Requires(msg != null && msg.GhostTarget == Owner);
            MessageQueue.Enqueue(msg);
        }

        public int MapInCapability(Thread current, Capability cap)
        {
            int id;
            if (cap.parent == current)
            {
                id = cap.Uses.id;
                cap.Uses.InsertAfter(Capabilities);
            }
            else
            {
                id = NewCapAllocId();
                var cap_ref = new CapabilityRef(current, id, cap);
                cap_ref.InsertAfter(Capabilities);
            }

            return id;
        }

        public VBinderThreadState(Thread current)
        {
            Contract.Ensures(Owner == current);
            Capabilities = new CapabilityRef(current, 0, Globals.CapabilityManager.NullCapability);
            MessageQueue = new VBinderMessageBuffer(Capacity, current);
            this.Owner = current;
        }

        public int NewCapAllocId()
        {
            return ++CapAllocId;
        }

        public CapabilityRef Find(Thread current, int cap_idx)
        {
            var ref_chain = current.VBinderState.Capabilities.Next;
            while (ref_chain != null)
            {

                if (ref_chain.id == cap_idx)
                    return ref_chain;

                ref_chain = ref_chain.Next;
            }
            return null;
        }
    }
}
