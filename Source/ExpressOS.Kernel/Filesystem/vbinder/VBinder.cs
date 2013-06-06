using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public class VBinder
    {
        public const int VBINDER_REGISTER_CHANNEL = 1;
        public const int VBINDER_ACQUIRE_CHANNEL = 2;
        public const int VBINDER_SEND = 3;
        public const int VBINDER_RECV = 4;

        private static int CreateChannel(Thread current, int label, int permission)
        {
            Contract.Requires(current != null && current.VBinderState.Owner == current);

            var has_permission = Globals.SecurityManager.CanCreateVBinderChannel(current, label, permission);
            if (!has_permission)
                return -ErrorCode.EPERM;

            var cap = Globals.CapabilityManager.Create(current, label, permission);

            Contract.Assert(has_permission);

            var id = current.VBinderState.MapInCapability(current, cap);

            return id;
        }

        private static int AcquireChannel(Thread current, int tid, int label)
        {
            var cap = Globals.CapabilityManager.Find(current, tid, label);

            if (cap == null)
                return -ErrorCode.EPERM;

            var id = current.VBinderState.MapInCapability(current, cap);

            return id;
        }

        public static int Dispatch(Thread current, ref Arch.ExceptionRegisters regs, int cmd, int arg1, int arg2, int arg3)
        {
            Contract.Requires(current != null && current.VBinderState.Owner == current);
            switch (cmd)
            {
                case VBINDER_REGISTER_CHANNEL:
                    return CreateChannel(current, arg1, arg2);
                case VBINDER_ACQUIRE_CHANNEL:
                    return AcquireChannel(current, arg1, arg2);
                case VBINDER_SEND:
                    return Send(current, arg1, new UserPtr(arg2), (uint)arg3);
                case VBINDER_RECV:
                    return Recv(current, ref regs, new UserPtr(arg1), new UserPtr(arg2), (uint)arg3);
            }

            return -ErrorCode.ENOSYS;
        }

        private static int Recv(Thread current, ref Arch.ExceptionRegisters regs, UserPtr ptr_label, UserPtr userBuf, uint size)
        {
            Contract.Requires(current.VBinderState.Owner == current);

            if (!current.Parent.Space.VerifyWrite(userBuf, size) || !current.Parent.Space.VerifyWrite(ptr_label, sizeof(int)))
                return -ErrorCode.EFAULT;

            var b = current.VBinderState.NoPendingMessages();
            if (b)
            {
                var entry = new VBinderCompletion(current, ptr_label, userBuf, size);
                current.VBinderState.Completion = entry;

                current.SaveState(ref regs);
                current.AsyncReturn = true;
                return 0;
            }
            else
            {
                var msg = current.VBinderState.TakeMessage();

                Contract.Assert(msg.GhostTarget == current);

                var length = msg.Length;
                ptr_label.Write(current.Parent, msg.label);
                userBuf.Write(current, new Pointer(msg.payload.Location), length);
                msg.Recycle();
                return length;
            }
        }

        private static void HandleAsyncCall(Thread target, VBinderMessage msg)
        {
            var entry = target.VBinderState.Completion;

            var length = msg.Length;
            entry.ptr_label.Write(target.Parent, msg.label);
            entry.userBuf.Write(target, new Pointer(msg.payload.Location), length);
            msg.Recycle();

            target.VBinderState.Completion = null;
            target.ReturnFromCompletion(length);
        }

        private static int Send(Thread current, int cap_idx, UserPtr userBuf, uint size)
        {
            if (!current.Parent.Space.VerifyRead(userBuf, size))
                return -ErrorCode.EFAULT;

            var cap_ref = current.VBinderState.Find(current, cap_idx);
            if (cap_ref == null)
                return -ErrorCode.EINVAL;

            var blob = Globals.AllocateAlignedCompletionBuffer((int)size);
            if (!blob.isValid)
                return -ErrorCode.ENOMEM;

            if (userBuf.Read(current, blob, (int)size) != 0)
                return -ErrorCode.EFAULT;

            var targetThread = cap_ref.def.parent;

            // Object invariant of thread
            Contract.Assume(targetThread.VBinderState.Owner == targetThread);

            var msg = new VBinderMessage(current, targetThread, cap_ref.def.label, blob, (int)size);

            if (targetThread.VBinderState.Completion != null)
            {
                HandleAsyncCall(targetThread, msg);
            }
            else
            {
                Contract.Assert(msg.GhostTarget == targetThread);
                targetThread.VBinderState.Enqueue(msg);
            }
            return (int)size;
        }
    }
}
