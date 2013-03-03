using ExpressOS.Kernel;
using System;

namespace ExpressOS.Kernel.Arch
{
    internal static class SyscallDispatcher
    {
        internal static int Dispatch(Thread current, ref ExceptionRegisters regs)
        {
            int scno, arg0, arg1, arg2, arg3, arg4, arg5;
            ArchAPI.GetSyscallParameters(regs, out scno, out arg0, out arg1, out arg2, out arg3, out arg4, out arg5);

            current.AsyncReturn = false;

            int retval = 0;

            switch (scno)
            {
                case __NR_exit:
                    Console.Write("Thread ");
                    Console.Write(current.Tid);
                    Console.WriteLine(" Exited");
                    current.Exit();
                    current.AsyncReturn = true;
                    break;

                case __NR_read:
                    retval = FileSystem.Read(current, ref regs, arg0, new UserPtr(arg1), arg2);
                    break;

                case __NR_write:
                    retval = FileSystem.Write(current, ref regs, arg0, new UserPtr(arg1), arg2);
                    break;

                case __NR_open:
                    retval = FileSystem.Open(current, ref regs, new UserPtr(arg0), arg1, arg2);
                    break;

                case __NR_close:
                    retval = FileSystem.Close(current, arg0);
                    break;

                case __NR_unlink:
                    retval = FileSystem.Unlink(current, new UserPtr(arg0));
                    break;

                case __NR_lseek:
                    retval = FileSystem.Lseek(current, arg0, arg1, arg2);
                    break;

                case __NR_getpid:
                    retval = ExpressOS.Kernel.Misc.Getpid(current);
                    break;

                case __NR_access:
                    retval = FileSystem.Access(current, ref regs, new UserPtr(arg0), arg1);
                    break;

                case __NR_mkdir:
                    retval = FileSystem.Mkdir(current, new UserPtr(arg0), arg1);
                    break;

                case __NR_clone:
                    retval = ExpressOS.Kernel.Exec.Clone(current, (uint)arg0, new UserPtr(arg1), new UserPtr(arg2), new UserPtr(arg3), ref regs);
                    break;

                case __NR_mprotect:
                    retval = ExpressOS.Kernel.Memory.mprotect(current, new UserPtr(arg0), arg1, arg2);
                    break;

                case __NR__newselect:
                    retval = ExpressOS.Kernel.Net.Select(current, ref regs, arg0, new UserPtr(arg1), new UserPtr(arg2), new UserPtr(arg3), new UserPtr(arg4));
                    break; 

                case __NR_writev:
                    retval = FileSystem.Writev(current, ref regs, arg0, new UserPtr(arg1), arg2);
                    break;

                case __NR_uname:
                    retval = ExpressOS.Kernel.Misc.UName(current, new UserPtr(arg0));
                    break;

                case __NR_fcntl64:
                    retval = FileSystem.Fcntl64(current, arg0, arg1, arg2);
                    break;

                case __NR_gettid:
                    retval = ExpressOS.Kernel.Misc.Gettid(current);
                    break;

                case __NR_dup:
                    retval = FileSystem.Dup(current, arg0);
                    break;

                case __NR_pipe:
                    retval = FileSystem.Pipe(current, new UserPtr(arg0));
                    break;

                case __NR_brk:
                    retval = ExpressOS.Kernel.Memory.Brk(current, (uint)arg0);
                    break;

                case __NR_ioctl:
                    retval = FileSystem.Ioctl(current, ref regs, arg0, arg1, arg2);
                    break;

                case __NR_setgid32:
                case __NR_setuid32:
                case __NR_flock:
                case __NR_sigaction:
                case __NR_sigprocmask:
                case __NR_sched_setscheduler:
                case __NR_setpriority:
                case __NR_fsync:

                    //Console.Write("Mock syscall ");
                    //Console.Write(scno);
                    //Console.WriteLine();

                    retval = 0;
                    break;

                case __NR_futex:
                    retval = ExpressOS.Kernel.Futex.DoFutex(current, ref regs, new UserPtr(arg0), arg1, arg2, new UserPtr(arg3), new UserPtr(arg4), (uint)arg5);
                    break;

                case __NR_dup2:
                    retval = FileSystem.Dup2(current, arg0, arg1);
                    break;

                case __NR_gettimeofday:
                    retval = ExpressOS.Kernel.Misc.Gettimeofday(current, new UserPtr(arg0));
                    break;

                case __NR_munmap:
                    retval = ExpressOS.Kernel.Memory.munmap(current, new UserPtr(arg0), arg1);
                    break;

                case __NR_ftruncate:
                    retval = FileSystem.ftruncate(current, arg0, arg1);
                    break;

                case __NR_socketcall:
                    retval = ExpressOS.Kernel.Net.socketcall(current, ref regs, arg0, new UserPtr(arg1));
                    break;

                case __NR_getpriority:
                    retval = ExpressOS.Kernel.Misc.Getpriority(current, arg0, arg1);
                    break;

                case __NR_pread64:
                    retval = FileSystem.Pread64(current, ref regs, arg0, new UserPtr(arg1), arg2, (uint)arg3);
                    break;

                case __NR_getcwd:
                    retval = FileSystem.Getcwd(current, new UserPtr(arg0), arg1);
                    break;

                case __NR_mmap2:
                    retval = ExpressOS.Kernel.Memory.mmap2(current, new UserPtr(arg0), arg1, arg2, arg3, arg4, arg5);
                    break;

                case __NR_stat64:
                    retval = FileSystem.Stat64(current, new UserPtr(arg0), new UserPtr(arg1));
                    break;

                case __NR_fstat64:
                    retval = FileSystem.FStat64(current, arg0, new UserPtr(arg1));
                    break;

                case __NR_lstat64:
                    retval = FileSystem.LStat64(current, new UserPtr(arg0), new UserPtr(arg1));
                    break;

                case __NR_getuid32:
                case __NR_geteuid32:
                    retval = ExpressOS.Kernel.Misc.Getuid32(current);
                    break;

                case __NR_madvise:
                    retval = ExpressOS.Kernel.Memory.madvise(current, (uint)arg0, arg1, arg2);
                    break;

                case __NR_nanosleep:
                    retval = ExpressOS.Kernel.Misc.Nanosleep(current, ref regs, new UserPtr(arg0), new UserPtr(arg1));
                    break;

                case __NR_sched_yield:
                    retval = ExpressOS.Kernel.Misc.Yield(current, ref regs);
                    break;

                case __NR_poll:
                    retval = ExpressOS.Kernel.Net.Poll(current, ref regs, new UserPtr(arg0), arg1, arg2);
                    break;

                case __NR_set_thread_area:
                    retval = ExpressOS.Kernel.TLS.SetThreadArea(current, new UserPtr(arg0));
                    break;

                case __NR_clock_gettime:
                    retval = ExpressOS.Kernel.Misc.ClockGetTime(current, arg0, new UserPtr(arg1));
                    break;

                case __NR_exit_group:
                    Console.WriteLine("Process exited");
                    Console.Flush();

                    //Misc.DumpStackTrace(current, new UserPtr(pt_regs->ebp));
                    //current.Parent.Space.Regions.DumpAll();
                    current.AsyncReturn = true;
                    break;

                case __NR_vbinder:
                    retval = ExpressOS.Kernel.VBinder.Dispatch(current, ref regs, arg0, arg1, arg2, arg3);
                    break;

                default:
                    Console.Write("Unknown syscall ");
                    Console.Write(scno);
                    Console.Write('@');
                    Console.Write(regs.ip);
                    Console.Write(" tid=");
                    Console.Write(current.Tid);
                    Console.WriteLine();
                    retval = -ErrorCode.ENOSYS;
                    break;
            }

            return retval;
        }

        #region Syscall numbers
        public const int __NR_restart_syscall = 0;
        public const int __NR_exit = 1;
        public const int __NR_fork = 2;
        public const int __NR_read = 3;
        public const int __NR_write = 4;
        public const int __NR_open = 5;
        public const int __NR_close = 6;
        public const int __NR_waitpid = 7;
        public const int __NR_creat = 8;
        public const int __NR_link = 9;
        public const int __NR_unlink = 10;
        public const int __NR_execve = 11;
        public const int __NR_chdir = 12;
        public const int __NR_time = 13;
        public const int __NR_mknod = 14;
        public const int __NR_chmod = 15;
        public const int __NR_lchown = 16;
        public const int __NR_break = 17;
        public const int __NR_oldstat = 18;
        public const int __NR_lseek = 19;
        public const int __NR_getpid = 20;
        public const int __NR_mount = 21;
        public const int __NR_umount = 22;
        public const int __NR_setuid = 23;
        public const int __NR_getuid = 24;
        public const int __NR_stime = 25;
        public const int __NR_ptrace = 26;
        public const int __NR_alarm = 27;
        public const int __NR_oldfstat = 28;
        public const int __NR_pause = 29;
        public const int __NR_utime = 30;
        public const int __NR_stty = 31;
        public const int __NR_gtty = 32;
        public const int __NR_access = 33;
        public const int __NR_nice = 34;
        public const int __NR_ftime = 35;
        public const int __NR_sync = 36;
        public const int __NR_kill = 37;
        public const int __NR_rename = 38;
        public const int __NR_mkdir = 39;
        public const int __NR_rmdir = 40;
        public const int __NR_dup = 41;
        public const int __NR_pipe = 42;
        public const int __NR_times = 43;
        public const int __NR_prof = 44;
        public const int __NR_brk = 45;
        public const int __NR_setgid = 46;
        public const int __NR_getgid = 47;
        public const int __NR_signal = 48;
        public const int __NR_geteuid = 49;
        public const int __NR_getegid = 50;
        public const int __NR_acct = 51;
        public const int __NR_umount2 = 52;
        public const int __NR_lock = 53;
        public const int __NR_ioctl = 54;
        public const int __NR_fcntl = 55;
        public const int __NR_mpx = 56;
        public const int __NR_setpgid = 57;
        public const int __NR_ulimit = 58;
        public const int __NR_oldolduname = 59;
        public const int __NR_umask = 60;
        public const int __NR_chroot = 61;
        public const int __NR_ustat = 62;
        public const int __NR_dup2 = 63;
        public const int __NR_getppid = 64;
        public const int __NR_getpgrp = 65;
        public const int __NR_setsid = 66;
        public const int __NR_sigaction = 67;
        public const int __NR_sgetmask = 68;
        public const int __NR_ssetmask = 69;
        public const int __NR_setreuid = 70;
        public const int __NR_setregid = 71;
        public const int __NR_sigsuspend = 72;
        public const int __NR_sigpending = 73;
        public const int __NR_sethostname = 74;
        public const int __NR_setrlimit = 75;
        public const int __NR_getrlimit = 76;
        public const int __NR_getrusage = 77;
        public const int __NR_gettimeofday = 78;
        public const int __NR_settimeofday = 79;
        public const int __NR_getgroups = 80;
        public const int __NR_setgroups = 81;
        public const int __NR_select = 82;
        public const int __NR_symlink = 83;
        public const int __NR_oldlstat = 84;
        public const int __NR_readlink = 85;
        public const int __NR_uselib = 86;
        public const int __NR_swapon = 87;
        public const int __NR_reboot = 88;
        public const int __NR_readdir = 89;
        public const int __NR_mmap = 90;
        public const int __NR_munmap = 91;
        public const int __NR_truncate = 92;
        public const int __NR_ftruncate = 93;
        public const int __NR_fchmod = 94;
        public const int __NR_fchown = 95;
        public const int __NR_getpriority = 96;
        public const int __NR_setpriority = 97;
        public const int __NR_profil = 98;
        public const int __NR_statfs = 99;
        public const int __NR_fstatfs = 100;
        public const int __NR_ioperm = 101;
        public const int __NR_socketcall = 102;
        public const int __NR_syslog = 103;
        public const int __NR_setitimer = 104;
        public const int __NR_getitimer = 105;
        public const int __NR_stat = 106;
        public const int __NR_lstat = 107;
        public const int __NR_fstat = 108;
        public const int __NR_olduname = 109;
        public const int __NR_iopl = 110;
        public const int __NR_vhangup = 111;
        public const int __NR_idle = 112;
        public const int __NR_vm86old = 113;
        public const int __NR_wait4 = 114;
        public const int __NR_swapoff = 115;
        public const int __NR_sysinfo = 116;
        public const int __NR_ipc = 117;
        public const int __NR_fsync = 118;
        public const int __NR_sigreturn = 119;
        public const int __NR_clone = 120;
        public const int __NR_setdomainname = 121;
        public const int __NR_uname = 122;
        public const int __NR_modify_ldt = 123;
        public const int __NR_adjtimex = 124;
        public const int __NR_mprotect = 125;
        public const int __NR_sigprocmask = 126;
        public const int __NR_create_module = 127;
        public const int __NR_init_module = 128;
        public const int __NR_delete_module = 129;
        public const int __NR_get_kernel_syms = 130;
        public const int __NR_quotactl = 131;
        public const int __NR_getpgid = 132;
        public const int __NR_fchdir = 133;
        public const int __NR_bdflush = 134;
        public const int __NR_sysfs = 135;
        public const int __NR_personality = 136;
        public const int __NR_afs_syscall = 137;
        public const int __NR_setfsuid = 138;
        public const int __NR_setfsgid = 139;
        public const int __NR__llseek = 140;
        public const int __NR_getdents = 141;
        public const int __NR__newselect = 142;
        public const int __NR_flock = 143;
        public const int __NR_msync = 144;
        public const int __NR_readv = 145;
        public const int __NR_writev = 146;
        public const int __NR_getsid = 147;
        public const int __NR_fdatasync = 148;
        public const int __NR__sysctl = 149;
        public const int __NR_mlock = 150;
        public const int __NR_munlock = 151;
        public const int __NR_mlockall = 152;
        public const int __NR_munlockall = 153;
        public const int __NR_sched_setparam = 154;
        public const int __NR_sched_getparam = 155;
        public const int __NR_sched_setscheduler = 156;
        public const int __NR_sched_getscheduler = 157;
        public const int __NR_sched_yield = 158;
        public const int __NR_sched_get_priority_max = 159;
        public const int __NR_sched_get_priority_min = 160;
        public const int __NR_sched_rr_get_interval = 161;
        public const int __NR_nanosleep = 162;
        public const int __NR_mremap = 163;
        public const int __NR_setresuid = 164;
        public const int __NR_getresuid = 165;
        public const int __NR_vm86 = 166;
        public const int __NR_query_module = 167;
        public const int __NR_poll = 168;
        public const int __NR_nfsservctl = 169;
        public const int __NR_setresgid = 170;
        public const int __NR_getresgid = 171;
        public const int __NR_prctl = 172;
        public const int __NR_rt_sigreturn = 173;
        public const int __NR_rt_sigaction = 174;
        public const int __NR_rt_sigprocmask = 175;
        public const int __NR_rt_sigpending = 176;
        public const int __NR_rt_sigtimedwait = 177;
        public const int __NR_rt_sigqueueinfo = 178;
        public const int __NR_rt_sigsuspend = 179;
        public const int __NR_pread64 = 180;
        public const int __NR_pwrite64 = 181;
        public const int __NR_chown = 182;
        public const int __NR_getcwd = 183;
        public const int __NR_capget = 184;
        public const int __NR_capset = 185;
        public const int __NR_sigaltstack = 186;
        public const int __NR_sendfile = 187;
        public const int __NR_getpmsg = 188;
        public const int __NR_putpmsg = 189;
        public const int __NR_vfork = 190;
        public const int __NR_ugetrlimit = 191;
        public const int __NR_mmap2 = 192;
        public const int __NR_truncate64 = 193;
        public const int __NR_ftruncate64 = 194;
        public const int __NR_stat64 = 195;
        public const int __NR_lstat64 = 196;
        public const int __NR_fstat64 = 197;
        public const int __NR_lchown32 = 198;
        public const int __NR_getuid32 = 199;
        public const int __NR_getgid32 = 200;
        public const int __NR_geteuid32 = 201;
        public const int __NR_getegid32 = 202;
        public const int __NR_setreuid32 = 203;
        public const int __NR_setregid32 = 204;
        public const int __NR_getgroups32 = 205;
        public const int __NR_setgroups32 = 206;
        public const int __NR_fchown32 = 207;
        public const int __NR_setresuid32 = 208;
        public const int __NR_getresuid32 = 209;
        public const int __NR_setresgid32 = 210;
        public const int __NR_getresgid32 = 211;
        public const int __NR_chown32 = 212;
        public const int __NR_setuid32 = 213;
        public const int __NR_setgid32 = 214;
        public const int __NR_setfsuid32 = 215;
        public const int __NR_setfsgid32 = 216;
        public const int __NR_pivot_root = 217;
        public const int __NR_mincore = 218;
        public const int __NR_madvise = 219;
        public const int __NR_madvise1 = 219;
        public const int __NR_getdents64 = 220;
        public const int __NR_fcntl64 = 221;
        public const int __NR_gettid = 224;
        public const int __NR_readahead = 225;
        public const int __NR_setxattr = 226;
        public const int __NR_lsetxattr = 227;
        public const int __NR_fsetxattr = 228;
        public const int __NR_getxattr = 229;
        public const int __NR_lgetxattr = 230;
        public const int __NR_fgetxattr = 231;
        public const int __NR_listxattr = 232;
        public const int __NR_llistxattr = 233;
        public const int __NR_flistxattr = 234;
        public const int __NR_removexattr = 235;
        public const int __NR_lremovexattr = 236;
        public const int __NR_fremovexattr = 237;
        public const int __NR_tkill = 238;
        public const int __NR_sendfile64 = 239;
        public const int __NR_futex = 240;
        public const int __NR_sched_setaffinity = 241;
        public const int __NR_sched_getaffinity = 242;
        public const int __NR_set_thread_area = 243;
        public const int __NR_get_thread_area = 244;
        public const int __NR_io_setup = 245;
        public const int __NR_io_destroy = 246;
        public const int __NR_io_getevents = 247;
        public const int __NR_io_submit = 248;
        public const int __NR_io_cancel = 249;
        public const int __NR_fadvise64 = 250;
        public const int __NR_exit_group = 252;
        public const int __NR_lookup_dcookie = 253;
        public const int __NR_epoll_create = 254;
        public const int __NR_epoll_ctl = 255;
        public const int __NR_epoll_wait = 256;
        public const int __NR_remap_file_pages = 257;
        public const int __NR_set_tid_address = 258;
        public const int __NR_timer_create = 259;
        public const int __NR_timer_settime = (__NR_timer_create + 1);
        public const int __NR_timer_gettime = (__NR_timer_create + 2);
        public const int __NR_timer_getoverrun = (__NR_timer_create + 3);
        public const int __NR_timer_delete = (__NR_timer_create + 4);
        public const int __NR_clock_settime = (__NR_timer_create + 5);
        public const int __NR_clock_gettime = (__NR_timer_create + 6);
        public const int __NR_clock_getres = (__NR_timer_create + 7);
        public const int __NR_clock_nanosleep = (__NR_timer_create + 8);
        public const int __NR_statfs64 = 268;
        public const int __NR_fstatfs64 = 269;
        public const int __NR_tgkill = 270;
        public const int __NR_utimes = 271;
        public const int __NR_fadvise64_64 = 272;
        public const int __NR_vserver = 273;
        public const int __NR_mbind = 274;
        public const int __NR_get_mempolicy = 275;
        public const int __NR_set_mempolicy = 276;
        public const int __NR_mq_open = 277;
        public const int __NR_mq_unlink = (__NR_mq_open + 1);
        public const int __NR_mq_timedsend = (__NR_mq_open + 2);
        public const int __NR_mq_timedreceive = (__NR_mq_open + 3);
        public const int __NR_mq_notify = (__NR_mq_open + 4);
        public const int __NR_mq_getsetattr = (__NR_mq_open + 5);
        public const int __NR_kexec_load = 283;
        public const int __NR_waitid = 284;
        public const int __NR_add_key = 286;
        public const int __NR_request_key = 287;
        public const int __NR_keyctl = 288;
        public const int __NR_ioprio_set = 289;
        public const int __NR_ioprio_get = 290;
        public const int __NR_inotify_init = 291;
        public const int __NR_inotify_add_watch = 292;
        public const int __NR_inotify_rm_watch = 293;
        public const int __NR_migrate_pages = 294;
        public const int __NR_openat = 295;
        public const int __NR_mkdirat = 296;
        public const int __NR_mknodat = 297;
        public const int __NR_fchownat = 298;
        public const int __NR_futimesat = 299;
        public const int __NR_fstatat64 = 300;
        public const int __NR_unlinkat = 301;
        public const int __NR_renameat = 302;
        public const int __NR_linkat = 303;
        public const int __NR_symlinkat = 304;
        public const int __NR_readlinkat = 305;
        public const int __NR_fchmodat = 306;
        public const int __NR_faccessat = 307;
        public const int __NR_pselect6 = 308;
        public const int __NR_ppoll = 309;
        public const int __NR_unshare = 310;
        public const int __NR_set_robust_list = 311;
        public const int __NR_get_robust_list = 312;
        public const int __NR_splice = 313;
        public const int __NR_sync_file_range = 314;
        public const int __NR_tee = 315;
        public const int __NR_vmsplice = 316;
        public const int __NR_move_pages = 317;
        public const int __NR_getcpu = 318;
        public const int __NR_epoll_pwait = 319;
        public const int __NR_utimensat = 320;
        public const int __NR_signalfd = 321;
        public const int __NR_timerfd = 322;
        public const int __NR_eventfd = 323;
        public const int __NR_fallocate = 324;
        #endregion

        public const int __NR_vbinder = 512;
    }
}
