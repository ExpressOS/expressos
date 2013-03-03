using System;
using ExpressOS.Kernel;
using System.Runtime.InteropServices;

namespace ExpressOS.Kernel.Arch
{
    internal static class Looper
    {
        public enum IPCCommand
        {
            EXPRESSOS_CMD_DUMP_PROFILE = 1,
            EXPRESSOS_CMD_ENABLE_PROFILER,
            EXPRESSOS_CMD_DISABLE_PROFILER,
            EXPRESSOS_CMD_FLUSH_CONSOLE,
        }

        //
        // Control whether let vglue reply to the request directly, or defer the reply due to waiting for a reply from the linux side.
        //
        public const int REPLY_IMMEDIATELY = 0;
        public const int REPLY_DEFERRED = 1;

        public static unsafe void ServerLoop()
        {
            bool do_wait = true;
            bool timeouted = false;
            Timeout timeout;
            
            var tag = new Msgtag();
            var src = new L4Handle();
            var u = NativeMethods.l4api_utcb();
            var mr = NativeMethods.l4api_utcb_mr();
            L4Handle linux_server_tid = ArchGlobals.LinuxServerTid;
            var pullTag = new Msgtag((int)Arch.IPCStubs.IPCTag.EXPRESSOS_IPC_FLUSH_RET_QUEUE, 0, 0, 0);
           
            while (true)
            {
                timeouted = false;
                timeout = Globals.TimeoutQueue.NextRecvTimeout();
                while (timeout == Timeout.RecvZero)
                {
                    var thr = Globals.TimeoutQueue.Take();
                    thr.ResumeFromTimeout();
                    timeout = Globals.TimeoutQueue.NextRecvTimeout();
                }

                while (do_wait && !timeouted)
                {
                    if (NativeMethods.linux_pending_reply_count() > 0)
                        tag = NativeMethods.l4api_ipc_send_and_wait(linux_server_tid, u, pullTag, out src, timeout);
                    else
                        tag = NativeMethods.l4api_ipc_wait(u, out src, timeout);
                
                    do_wait = tag.HasError;
                   
                    if (tag.ErrorCode() == ThreadRegister.L4_IPC_RETIMEOUT)
                    {
                        timeouted = true;
                        break;
                    }
                }

                if (timeouted)
                    continue;

                // Get rid of permission mask
                src._value = (src._value >> L4Handle.L4_CAP_SHIFT) << L4Handle.L4_CAP_SHIFT;

                HandleMessage(src, ref tag, ref *NativeMethods.l4api_utcb_exc(), ref *mr);
                do_wait = true;
            }
        }

        private static int HandleMessage(L4Handle src, ref Msgtag tag, ref ExceptionRegisters pt_regs, ref MessageRegisters mr)
        {
            Thread thr;
            if (tag.Label == Msgtag.L4_PROTO_PAGE_FAULT || IsLinuxSyscall(tag, ref pt_regs))
            {
                thr = Globals.Threads.Lookup(src);
                if (thr == null)
                {
                    Console.Write("HandleMessage: Unknown thread ");
                    Console.Write(src._value);
                    Console.Write(" tag=");
                    Console.Write(tag.raw);
                    Console.WriteLine();
                    return REPLY_DEFERRED;
                }

                thr.AsyncReturn = false;
                if (tag.Label == Msgtag.L4_PROTO_PAGE_FAULT)
                {
                    return HandlePageFault(src, thr, ref tag, ref mr);
                }
                else
                {
                    return HandleSyscall(src, thr, ref tag, ref pt_regs);
                }
            }
            else if (tag.Label == (int)Arch.IPCStubs.IPCTag.EXPRESSOS_IPC)
            {
                HandleAsyncCall(ref mr);
                return REPLY_DEFERRED;
            }
            else if (tag.Label == (int)Arch.IPCStubs.IPCTag.EXPRESSOS_IPC_CMD)
            {
                switch ((IPCCommand)mr.mr0)
                {
                    case IPCCommand.EXPRESSOS_CMD_DUMP_PROFILE:
                        SyscallProfiler.Dump();
                        break;
                    case IPCCommand.EXPRESSOS_CMD_ENABLE_PROFILER:
                        SyscallProfiler.Enable = true;
                        break;
                    case IPCCommand.EXPRESSOS_CMD_DISABLE_PROFILER:
                        SyscallProfiler.Enable = false;
                        break;
                    case IPCCommand.EXPRESSOS_CMD_FLUSH_CONSOLE:
                        Console.Flush();
                        break;
                }
                return REPLY_DEFERRED;
            }
            else
            {
                Console.Write("Unhandled exception tag=");
                Console.Write(tag.raw);
                Console.Write(" exc_trapno=");
                Console.Write(pt_regs.trapno);
                Console.Write(" err=");
                Console.Write(pt_regs.err);
                Console.Write(" eip=");
                Console.Write(pt_regs.ip);
                Console.Write(" sp=");
                Console.Write(pt_regs.sp);
                Console.WriteLine();
            }
            return REPLY_DEFERRED;
        }

        private static int HandlePageFault(L4Handle src, Thread thr, ref Msgtag tag, ref MessageRegisters mr)
        {
            uint pfa;
            uint pc;
            uint faultType;
            ArchAPI.GetPageFaultInfo(ref mr, out pfa, out pc, out faultType);

            Pointer physicalPage;
            uint permssion;
            Pager.HandlePageFault(thr.Parent, faultType, new Pointer(pfa), new Pointer(pc), out physicalPage, out permssion);

            if (thr.AsyncReturn)
                return REPLY_DEFERRED;
           
            if (physicalPage == Pointer.Zero)
            {
                // We got an error, don't reply
                // thr.Parent.Space.Regions.DumpAll();
                Console.Write("Unhandled page fault ");
                Console.Write(pfa);
                Console.Write("@");
                Console.Write(pc);
                Console.Write(" thr=");
                Console.Write(thr.Tid);
                Console.WriteLine();

                thr.Parent.Space.DumpAll();

                return REPLY_DEFERRED;
            }

            ArchAPI.ReturnFromPageFault(src, out tag, ref mr, pfa, physicalPage, permssion);
            return REPLY_IMMEDIATELY;
        }

        private static int HandleSyscall(L4Handle src, Thread thr, ref Msgtag pt_tag, ref ExceptionRegisters pt_regs)
        {
            /*
             * Make a copy of the registers, as other IPC calls can override
             * the same area.
             */
            ExceptionRegisters exc = pt_regs;
            var scno = exc.eax;

            SyscallProfiler.EnterSyscall(scno);
            var ret = SyscallDispatcher.Dispatch(thr, ref exc);

            if (thr.AsyncReturn)
                return REPLY_DEFERRED;

            ArchAPI.ReturnFromSyscall(src, ref exc, ret);
            SyscallProfiler.ExitSyscall(scno);

            return REPLY_DEFERRED;
        }

        public static void HandleAsyncCall(ref MessageRegisters mr)
        {
            int asyncCallType = (int)mr.mr0;
            uint handle = (uint)mr.mr1;
            int arg1 = (int)mr.mr2;
            int arg2 = (int)mr.mr3;
            int arg3 = (int)mr.mr4;
            int arg4 = (int)mr.mr5;
            int arg5 = (int)mr.mr6;

            var e = Globals.CompletionQueue.Take(handle);
            if (e == null)
            {
                Console.Write("HandleAsyncCall: cannot find completion for handle ");
                Console.Write(handle);
                Console.WriteLine();
                return;
            }

            var sfs_close = e.SFSFlushCompletion;
            if (sfs_close != null)
            {
                sfs_close.Dispose();
                return;
            }

            var thr_completion = e.ThreadCompletionEntry;
            Thread.ResumeFromCompletion(thr_completion, arg1, arg2, arg3, arg4, arg5);
        }

        private static bool IsLinuxSyscall(Msgtag tag, ref ExceptionRegisters exc)
        {
            const uint GP_TRAP_NO = 0xd;
            const uint LINUX_SYSCALL_GATE = 0x80;
            return tag.Label == Msgtag.L4_PROTO_EXCEPTION && exc.trapno == GP_TRAP_NO && exc.err == LINUX_SYSCALL_GATE * 8 + 2;
        }
    }
}
