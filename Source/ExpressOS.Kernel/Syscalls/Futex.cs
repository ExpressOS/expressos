using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Arch = ExpressOS.Kernel.Arch;
using ExpressOS.Kernel;
using System.Runtime.InteropServices;
using ExpressOS.Kernel.Arch;

namespace ExpressOS.Kernel
{
    public class Futex
    {
        public const int FUTEX_WAIT = 0;
        public const int FUTEX_WAKE = 1;
        public const int FUTEX_FD = 2;
        public const int FUTEX_REQUEUE = 3;
        public const int FUTEX_CMP_REQUEUE = 4;
        public const int FUTEX_WAKE_OP = 5;
        public const int FUTEX_LOCK_PI = 6;
        public const int FUTEX_UNLOCK_PI = 7;
        public const int FUTEX_TRYLOCK_PI = 8;
        public const int FUTEX_WAIT_BITSET = 9;
        public const int FUTEX_WAKE_BITSET = 10;
        public const int FUTEX_WAIT_REQUEUE_PI = 11;
        public const int FUTEX_CMP_REQUEUE_PI = 12;
        public const int FUTEX_PRIVATE_FLAG = 128;
        public const int FUTEX_CLOCK_REALTIME = 256;
        public const int FUTEX_CMD_MASK = ~(FUTEX_PRIVATE_FLAG | FUTEX_CLOCK_REALTIME);

        public const uint FUTEX_BITSET_MATCH_ANY = 0xffffffff;

        public const int FLAGS_SHARED = 0x01;
        public const int FLAGS_CLOCKRT = 0x02;
        public const int FLAGS_HAS_TIMEOUT = 0x04;

        public static int DoFutex(Thread current, ref Arch.ExceptionRegisters regs, UserPtr uaddr, int op, int val, UserPtr timeoutPtr, UserPtr uaddr2, uint val3)
        {
            var cmd = op & FUTEX_CMD_MASK;
            var flags = ToFlags(op, cmd);
            timespec ts;
            ts.tv_sec = ts.tv_nsec = 0;

            // Don't care about shared mutex
            if ((flags & FLAGS_SHARED) != 0)
            {
                return DoFutexShared(current, ref regs, uaddr, op, val, timeoutPtr, uaddr2, val3);
            }

            bool hasTimeout = timeoutPtr != UserPtr.Zero;
            if (hasTimeout && timeoutPtr.Read(current, out ts) != 0)
                return -ErrorCode.EFAULT;

            switch (cmd)
            {
                case FUTEX_WAIT:
                    return Wait(current, ref regs, uaddr, flags, val, hasTimeout, ts, FUTEX_BITSET_MATCH_ANY);
                case FUTEX_WAIT_BITSET:
                    return Wait(current, ref regs, uaddr, flags, val, hasTimeout, ts, val3);
                case FUTEX_WAKE:
                    return Wake(current, uaddr, flags, val, FUTEX_BITSET_MATCH_ANY);
                case FUTEX_WAKE_BITSET:
                    return Wake(current, uaddr, flags, val, val3);
                default:
                    Arch.Console.Write("futex: unknown primitives ");
                    Arch.Console.Write(cmd);
                    Arch.Console.WriteLine();
                    return -ErrorCode.ENOSYS;
            }
        }

        // Forward the futex request to Linux helper
        private static int DoFutexShared(Thread current, ref Arch.ExceptionRegisters regs, UserPtr uaddr, int op, int val, UserPtr timeoutPtr, UserPtr uaddr2, uint val3)
        {
            // Some local test
            var cmd = op & FUTEX_CMD_MASK;
            timespec ts = new timespec();

            bool hasTimeout = timeoutPtr != UserPtr.Zero;
            if (hasTimeout && timeoutPtr.Read(current, out ts) != 0)
                return -ErrorCode.EFAULT;

            if (cmd == FUTEX_WAIT || cmd == FUTEX_WAIT_BITSET)
            {
                int old_val;
                if (uaddr.Read(current, out old_val) != 0)
                    return -ErrorCode.EFAULT;

                if (old_val != val)
                    return -ErrorCode.EWOULDBLOCK;

                var bitset = cmd == FUTEX_WAIT ? FUTEX_BITSET_MATCH_ANY : val3;
                var shadowAddr = FindShadowAddr(current, uaddr);

                if (shadowAddr == Pointer.Zero)
                {
                    Arch.Console.WriteLine("FutexShared: Don't know how to deal with shared_wait");
                    return 0;
                }

                var futex_entry = new FutexCompletionEntry(current, uaddr, bitset);

                Arch.IPCStubs.linux_sys_futex_wait(current.Parent.helperPid, current.impl._value.thread._value, op, shadowAddr, val, ts, bitset);
               
                Globals.CompletionQueue.Enqueue(futex_entry);
                current.SaveState(ref regs);
                current.AsyncReturn = true;
                return 0;
            }
            else if (cmd == FUTEX_WAKE || cmd == FUTEX_WAKE_BITSET)
            {
                var bitset = cmd == FUTEX_WAKE ? FUTEX_BITSET_MATCH_ANY : val3;
                if (bitset == 0)
                    return -ErrorCode.EINVAL;

                var shadowAddr = FindShadowAddr(current, uaddr);
                if (shadowAddr == Pointer.Zero)
                {
                    Arch.Console.WriteLine("FutexShared: Don't know how to deal with shared_wake");
                    return 0;
                }

                var c = new BridgeCompletion(current, new ByteBufferRef());
               
                Arch.IPCStubs.linux_sys_futex_wake(current.Parent.helperPid, current.impl._value.thread._value, op, shadowAddr, bitset);

                Globals.CompletionQueue.Enqueue(c);
                current.SaveState(ref regs);
                current.AsyncReturn = true;
                
                return 0;
            }

            return 0;
        }

        private static Pointer FindShadowAddr(Thread current, UserPtr uaddr)
        {
            var r = current.Parent.Space.Find(uaddr.Value);
            if (r == null)
                return Pointer.Zero;

            var f = r.BackingFile;
            
            var shm_inode = f.inode.AlienSharedMemoryINode;
            if (shm_inode != null)
                return shm_inode.vaddrInShadowProcess + (uaddr.Value - r.StartAddress);
            else
                return Pointer.Zero;

        }

        private static int Wake(Thread current, UserPtr uaddr, int flags, int nr_wake, uint bitset)
        {
            if (bitset == 0)
                return -ErrorCode.EINVAL;

            var space = current.Parent.Space;

            int ret = 0;

            //Arch.Console.Write("Futex_wake: thr=");
            //Arch.Console.Write(current.Tid);
            //Arch.Console.Write(" uaddr=");
            //Arch.Console.Write(uaddr.Value.ToUInt32());
            //Arch.Console.Write(" nr_wake=");
            //Arch.Console.Write(nr_wake);
            //Arch.Console.WriteLine();

            FutexCompletionEntry q;
            // XXX: For simplicity (right now), we scan through the whole completion queue
            for (var p = Globals.FutexLists.nextFutex; p != Globals.FutexLists && ret < nr_wake; p = q)
            {
                q = p.nextFutex;

                //Arch.Console.Write("wake_loop: uaddr=");
                //Arch.Console.Write(p.uaddr.Value.ToUInt32());
                //Arch.Console.Write(" tid=");
                //Arch.Console.Write(p.thr.Tid);
                //Arch.Console.WriteLine();

                if (!(p.Space == space && p.uaddr == uaddr && (p.bitset & bitset) != 0))
                    continue;

                ++ret;
                WakeUp(p, true, 0);
            }

            return ret;
        }

        private static void WakeUp(FutexCompletionEntry entry, bool cancelTimeout, int ret)
        {
            if (cancelTimeout)
            {
                var p = entry.timeoutNode;
                if (p != null)
                    p.Cancel();
            }

            /* Dequeue the futex in the completion queue */
            Globals.CompletionQueue.Take(entry.thr.impl._value.thread._value);
            entry.Unlink();
            entry.thr.ReturnFromCompletion(ret);
        }

        private static int Wait(Thread current, ref Arch.ExceptionRegisters regs, UserPtr uaddr, int flags, int val, bool hasTimeout, timespec ts, uint bitset)
        {
            int old_val;
            if (uaddr.Read(current, out old_val) != 0)
                return -ErrorCode.EFAULT;

            if (old_val != val)
                return -ErrorCode.EWOULDBLOCK;

            //Arch.Console.Write("wait: addr=");
            //Arch.Console.Write(uaddr.Value.ToUInt32());
            //Arch.Console.Write(" thr=");
            //Arch.Console.Write(current.Tid);
            //Arch.Console.WriteLine();

            TimerQueueNode node;
            var futex_entry = new FutexCompletionEntry(current, uaddr, bitset);
            Globals.FutexLists.InsertAtTail(futex_entry);

            if (hasTimeout)
            {
                node = Globals.TimeoutQueue.Enqueue(ts.ToMilliseconds(), current);
                futex_entry.timeoutNode = node;
            }

            Globals.CompletionQueue.Enqueue(futex_entry);
            current.SaveState(ref regs);
            current.AsyncReturn = true;
            return 0;
        }

        private static int ToFlags(int op, int cmd)
        {
            int flags = 0;

            if ((op & FUTEX_PRIVATE_FLAG) == 0)
                flags |= FLAGS_SHARED;

            if ((op & FUTEX_CLOCK_REALTIME) != 0)
            {
                flags |= FLAGS_CLOCKRT;
                if (cmd != FUTEX_WAIT_BITSET && cmd != FUTEX_WAIT_REQUEUE_PI)
                    return -ErrorCode.ENOSYS;
            }

            return flags;
        }

        public static void HandleFutexAsync(FutexCompletionEntry c, int ret)
        {
            var current = c.thr;
            c.Unlink();
            current.ReturnFromCompletion(ret);
        }
    }
}
