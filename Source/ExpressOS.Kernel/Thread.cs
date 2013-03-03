using System;

using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public class Thread
    {
        public readonly Arch.ArchThread impl;
        public readonly Process Parent;
        internal IntPtr TLSArray;

        public bool AsyncReturn;
        public readonly VBinderThreadState VBinderState;
        private Arch.ExceptionRegisters regs;
       
        [ContractInvariantMethod]
        private void ObjectInvariantMethod()
        {
            Contract.Invariant(Parent != null && VBinderState.Owner == this);
        }

        private Thread(Arch.ArchThread impl, Process parent)
        {
            Contract.Requires(parent != null);
            Contract.Ensures(Parent == parent);
            Contract.Ensures(VBinderState.Owner == this);
            
            this.impl = impl;
            this.Parent = parent;
            this.TLSArray = Arch.NativeMethods.l4api_tls_array_alloc();
            this.VBinderState = new VBinderThreadState(this);
        }

        internal static Thread Create(Process parent)
        {
            var impl = Arch.ArchThread.Create(parent.Space.impl);
            if (impl == null)
            {
                Arch.Console.WriteLine("create thread failed");
                return null;
            }

            var res = new Thread(impl, parent);
            Globals.Threads.Add(res);

            return res;
        }

        public void Start(Pointer ip, Pointer sp)
        {
            impl.Start(ip, sp);
        }

        public int Tid
        {
            get
            {
                return (int)(impl._value.thread._value >> Arch.L4Handle.L4_CAP_SHIFT);
            }
        }

        public void Exit()
        {
            Globals.CompletionQueue.ClearAllPendingCompletion(impl._value.thread._value);
            Globals.Threads.Remove(this);
            impl.Destroy();
            Arch.NativeMethods.l4api_tls_array_free(TLSArray);
        }

        internal void SaveState(ref Arch.ExceptionRegisters pt_regs)
        {
            regs = pt_regs;
        }

        private void ReturnFromSyscall(int ret)
        {
            Arch.ArchAPI.ReturnFromSyscall(impl._value.thread, ref regs, ret);
        }

        internal void ReturnFromCompletion(int ret)
        {
            ReturnFromSyscall(ret);
        }

        public void ResumeFromTimeout()
        {
            var completionState = Globals.CompletionQueue.Take(impl._value.thread._value);
            
            if (completionState == null)
            {
                Arch.Console.Write("resume timeout: empty completion state for thread ");
                Arch.Console.Write(impl._value.thread._value);
                Arch.Console.WriteLine();
                return;
            }

            switch (completionState.kind)
            {
                case GenericCompletionEntry.Kind.SleepCompletionKind:
                    ReturnFromCompletion(0);
                    break;

                case GenericCompletionEntry.Kind.FutexCompletionKind:
                    completionState.FutexCompletion.Unlink();
                    ReturnFromCompletion(-ErrorCode.ETIMEDOUT);
                    break;

                default:
                    Arch.Console.Write("ResumeFromTimeout: Unknown entry ");
                    Arch.Console.Write((uint)completionState.kind);
                    Arch.Console.WriteLine();
                    break;
            }

        }

        public static void ResumeFromCompletion(GenericCompletionEntry c, int arg1, int arg2, int arg3, int arg4, int arg5)
        {
            Contract.Requires(c != null);

            switch (c.kind)
            {
                case GenericCompletionEntry.Kind.BinderCompletionKind:
                    BinderINode.Instance.HandleAsyncCall(c.BinderCompletion, arg1, arg2, arg3, arg4, arg5);
                    break;

                case GenericCompletionEntry.Kind.PollCompletionKind:
                    Net.HandlePollAsync(c.PollCompletion, arg1);
                    break;

                case GenericCompletionEntry.Kind.SelectCompletionKind:
                    Net.HandleSelectAsync(c.SelectCompletion, arg1);
                    break;

                case GenericCompletionEntry.Kind.FutexCompletionKind:
                    Futex.HandleFutexAsync(c.FutexCompletion, arg1);
                    break;

                case GenericCompletionEntry.Kind.IOCompletionKind:
                    Arch.ArchINode.HandleIOCP(c.IOCompletion, arg1, arg2);
                    break;

                case GenericCompletionEntry.Kind.BridgeCompletionKind:
                    HandleBridgeCompletion(c.BridgeCompletion, arg1);
                    break;

                case GenericCompletionEntry.Kind.SocketCompletionKind:
                    Net.HandleSocketCompletion(c.SocketCompletion, arg1);
                    break;

                case GenericCompletionEntry.Kind.GetSocketParamCompletionKind:
                    Net.HandleGetSockParamCompletion(c.GetSockParamCompletion, arg1, arg2);
                    break;

                case GenericCompletionEntry.Kind.OpenFileCompletionKind:
                    FileSystem.HandleOpenFileCompletion(c.OpenFileCompletion, arg1, arg2);
                    break;

                default:
                    Arch.Console.Write("ResumeFromCompletion: Unknown entry ");
                    Arch.Console.Write((uint)c.kind);
                    Arch.Console.WriteLine();
                    break;
            }
        }

        private static void HandleBridgeCompletion(BridgeCompletion c, int ret)
        {
            var current = c.thr;
            c.Dispose();
            current.ReturnFromCompletion(ret);
        }
    }
}
