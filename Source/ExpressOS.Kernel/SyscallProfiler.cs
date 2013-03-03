
namespace ExpressOS.Kernel
{
    public static class SyscallProfiler
    {
        public const int SOCKET_CALL_ID = 512;
        public const int PF_ID = SOCKET_CALL_ID + 30;
        public const int OPEN_TYPE_ID = PF_ID + 1;
        public const int MAX_SYSCALLS = OPEN_TYPE_ID + 10;

        private static SyscallStats[] stats;
        public static bool Enable;
        struct SyscallStats
        {
            public int invokeTimes;
            public ulong startTime;
            public long totalTime;
        }

        public static void Initialize()
        {
            stats = new SyscallStats[MAX_SYSCALLS + 1];
            Enable = false;
        }

        public static void EnterPageFault()
        {
            EnterSyscall(PF_ID);
        }
        public static void ExitPageFault()
        {
            ExitSyscall(PF_ID);
        }
        public static void EnterSocketcall(int scno)
        {
            EnterSyscall(SOCKET_CALL_ID + scno);
        }

        public static void ExitSocketcall(int scno)
        {
            ExitSyscall(SOCKET_CALL_ID + scno);
        }

        public static void AccountOpen(int type, long time)
        {
            AccountSyscall(OPEN_TYPE_ID + type, time);
        }

        private static void AccountSyscall(int scno, long time)
        {
            if (!Enable)
                return;

            stats[scno].invokeTimes++;
            stats[scno].totalTime += time;
        }

        public static void EnterSyscall(int scno)
        {
            if (!Enable)
                return;

            stats[scno].invokeTimes++;
            stats[scno].startTime = Arch.NativeMethods.l4api_get_system_clock();
        }

        public static void ExitSyscall(int scno)
        {
            if (!Enable)
                return;
            
            var now = Arch.NativeMethods.l4api_get_system_clock();
            stats[scno].totalTime += (long)(now - stats[scno].startTime);
        }

        public static void Dump()
        {
            for (int i = 0; i < MAX_SYSCALLS + 1; ++i)
            {
                if (stats[i].invokeTimes != 0)
                {
                    Arch.LinuxConsole.Write("Syscall ");
                    Arch.LinuxConsole.Write(i);
                    Arch.LinuxConsole.Write(",");
                    Arch.LinuxConsole.Write(stats[i].invokeTimes);
                    Arch.LinuxConsole.Write(",");
                    Arch.LinuxConsole.Write(stats[i].totalTime);
                    Arch.LinuxConsole.WriteLine();
                }
            }
        }
    }

}
