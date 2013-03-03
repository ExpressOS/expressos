using System;
using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    public static class Misc
    {
        /* Structure describing the system and machine.  */
        public const int UTSFieldLength = 65;
        public const int UTSSysnameOffset = 0;
        public const int UTSNodenameOffset = UTSSysnameOffset + UTSFieldLength;
        public const int UTSReleaseOffset = UTSNodenameOffset + UTSFieldLength;
        public const int UTSVersionOffset = UTSReleaseOffset + UTSFieldLength;
        public const int UTSMachineOffset = UTSVersionOffset + UTSFieldLength;
        public const int UTSDomainNameOffset = UTSMachineOffset + UTSFieldLength;
        public const int UTSStructLength = UTSDomainNameOffset + UTSFieldLength;
        
        public static byte[] UTSNameInfo;
        private const int SIZE_OF_TIMESPEC = 8;

        private const int CLOCK_REALTIME = 0;
        private const int CLOCK_MONOTONIC = 1;
        private const int CLOCK_PROCESS_CPUTIME_ID = 2;
        private const int CLOCK_THREAD_CPUTIME_ID = 3;

        public static timespec MonotonicTimeSpec;
        public static timespec UptimeTimeSpec;
        public static ulong Epoch;

        public static void Initialize()
        {
            UTSNameInfo = new byte[UTSStructLength];
            AssignUTSName(UTSSysnameOffset, "Linux");
            AssignUTSName(UTSNodenameOffset, "(none)");
            AssignUTSName(UTSReleaseOffset, "3.0.0-l4-g8732b51-dirty");
            AssignUTSName(UTSVersionOffset, "#11 Thu Apr 5 23:45:03 CDT 2012");
            AssignUTSName(UTSMachineOffset, "i686");
            AssignUTSName(UTSDomainNameOffset, "(none)");

            Epoch = Arch.NativeMethods.l4api_get_system_clock();

            Arch.IPCStubs.linux_sys_clock_gettime(CLOCK_REALTIME);
            UptimeTimeSpec = GetTimeSpec(Globals.LinuxIPCBuffer);

            Arch.IPCStubs.linux_sys_clock_gettime(CLOCK_MONOTONIC);
            MonotonicTimeSpec = GetTimeSpec(Globals.LinuxIPCBuffer);
        }

        [Pure]
        private static timespec GetTimeSpec(ByteBufferRef buf)
        {
            Contract.Requires(buf.Length >= timespec.Size);

            var r = new timespec();
            r.tv_sec = 0;
            r.tv_nsec = 0;
            
            for (var i = 0; i < sizeof(uint); ++i)
            {
                Contract.Assume(buf.Length >= timespec.Size);
                Contract.Assert(i < buf.Length);
                r.tv_sec += (uint)buf.Get(i) << (8 * i);
            }

            for (var i = 0; i < sizeof(uint); ++i)
            {
                Contract.Assume(buf.Length >= timespec.Size);
                Contract.Assert(i + sizeof(uint) < buf.Length);
                r.tv_nsec += (uint)buf.Get(i + sizeof(uint)) << (8 * i);
            }
            return r;
        }

        public static int UName(Thread current, UserPtr buf)
        {
            var res = buf.Write(current, UTSNameInfo);
            return res == 0 ? 0 : -1;
        }

        private static void AssignUTSName(int offset, string v) {
            int i = 0;
            foreach (var c in v)
                UTSNameInfo[offset + i++] = (byte)c;
        }

        public static int Gettid(Thread current)
        {
            return current.Tid;
        }

        // XXX: Move to some places
        public static void DumpStackTrace(Thread current, UserPtr ebp)
        {
            int eip;
            int new_ebp;
            while (ebp != UserPtr.Zero)
            {
                Arch.Console.Write("EIP:");
                if ((ebp + sizeof(int)).Read(current, out eip) != 0)
                    return;
                Arch.Console.Write(eip);
                Arch.Console.WriteLine();
                if (ebp.Read(current, out new_ebp) != 0)
                    return;
                ebp = new UserPtr(new_ebp);
            }
        }

        public static int ClockGetTime(Thread current, int clock_id, UserPtr tp)
        {
            timespec res;
            res.tv_nsec = res.tv_sec = 0;
            switch (clock_id)
            {
                case CLOCK_REALTIME:
                case CLOCK_PROCESS_CPUTIME_ID:
                case CLOCK_THREAD_CPUTIME_ID:
                    res = GetTime(UptimeTimeSpec);
                    break;

                case CLOCK_MONOTONIC:
                    res = GetTime(MonotonicTimeSpec);
                    break;

                default:
                    Arch.Console.Write("Unimplemented ClockGetTime:");
                    Arch.Console.Write(clock_id);
                    Arch.Console.WriteLine();
                    return -ErrorCode.ENOSYS;
            }
    
            if (tp.Write(current, ref res) != 0)
                return -ErrorCode.EFAULT;

            return 0;
        }

        public static int Gettimeofday(Thread current, UserPtr timeval)
        {
            timespec res = GetTime(UptimeTimeSpec);
            timeval r;
            r.tv_sec = res.tv_sec;
            r.tv_usec = res.tv_nsec / 1000;

            if (timeval.Write(current, ref r) != 0)
                return -ErrorCode.EFAULT;

            return 0;
        }

        private static timespec GetTime(timespec start)
        {
            var now = Arch.NativeMethods.l4api_get_system_clock();
            var diff = now - Epoch;

            start.tv_sec += (uint)diff / 1000000;
            start.tv_nsec += (uint)((diff % 1000000) * 1000);
            return start;
        }

        public static int Getpid(Thread current)
        {
            return current.Parent.helperPid;
        }

        public static int Getuid32(Thread current)
        {
            return current.Parent.Credential.Uid;
        }

        public static int Getpriority(Thread current, int arg0, int arg1)
        {
            return 20;
        }
        
        public static int Nanosleep(Thread current, ref Arch.ExceptionRegisters regs, UserPtr rqtp, UserPtr rmtp)
        {
            timespec ts;
            timespec trem;

            trem.tv_sec = 0;
            trem.tv_nsec = 0;

            if (rqtp.Read(current, out ts) != 0 || rmtp.Write(current, ref trem) != 0)
                return -ErrorCode.EFAULT;

            Globals.TimeoutQueue.Enqueue(ts.ToMilliseconds(), current);
            var c = new SleepCompletion(current);

            Globals.CompletionQueue.Enqueue(c);
            current.SaveState(ref regs);
            current.AsyncReturn = true;
            return 0;
        }

        public static int Yield(Thread current, ref Arch.ExceptionRegisters regs)
        {
            // Arch.API.l4api_thread_yield();
            return 0;
        }
    }
}
