using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public class Capability
    {
        public readonly Thread parent;
        public readonly int label;
        int permission;
        internal CapabilityRef Uses;
        internal Capability prev, next;

        internal Capability(Thread parent, int label, int permission, bool isNullCapability)
        {
            Contract.Requires(parent == null || parent.VBinderState.Owner == parent);

            this.parent = parent;
            this.label = label;
            this.permission = permission;

            if (!isNullCapability)
            {
                var id = parent.VBinderState.NewCapAllocId();
                this.Uses = new CapabilityRef(parent, id, this);
            }
        }

        internal void AddUses(CapabilityRef uses)
        {
            // throw new NotImplementedException();
        }

        public int HandleInParent
        {
            get
            {
                return Uses.id;
            }
        }
    }

    public class CapabilityRef {
        public readonly Thread Namespace;
        public readonly int id;
        // use chain
        public CapabilityRef UsePrev, UseNext;
        // per-thread ref chain
        public CapabilityRef Prev, Next;
        // HACK: A capabiilty should be a sub class of Capability ref..
        public readonly Capability def;

        public CapabilityRef(Thread name_space, int id, Capability cap)
        {
            this.Namespace = name_space;
            this.id = id;
            this.def = cap;
        }

        public void InsertAfter(CapabilityRef head)
        {
            this.Prev = head;
            this.Next = head.Next;
            if (head.Next != null)
                head.Next.Prev = this;
            head.Next = this;
        }
    }

    public class CapabilityManager
    {
        private Capability head;
        public readonly Capability NullCapability;

        public CapabilityManager()
        {
            NullCapability = new Capability(null, 0, 0, true);
            head = NullCapability;
        }

        public Capability Create(Thread current, int label, int permission)
        {
            Contract.Requires(current != null && current.VBinderState.Owner == current);
            var cap = new Capability(current, label, permission, false);

            cap.prev = head;
            cap.next = head.next;

            if (head.next != null)
                head.next.prev = cap;
            
            head.next = cap;

            return cap;
        }

        public Capability Find(Thread current, int target_tid, int label)
        {
            var cap = head.next;
            while (cap != null)
            {
                if (cap.label == label && cap.parent.Tid == target_tid)
                    return cap;

                cap = cap.next;
            }

            return null;
        }
    }

}
