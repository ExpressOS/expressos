using System;
using Arch = ExpressOS.Kernel.Arch;
using System.Diagnostics.Contracts;


namespace ExpressOS.Kernel
{
    public static class Exec
    {
        const uint INITIAL_STACK_LOCATION = 0xb2000000;
        public const uint CLONE_VM = 0x00000100;     /* set if VM shared between processes */
        public const uint CLONE_FS = 0x00000200;     /* set if fs info shared between processes */
        public const uint CLONE_FILES = 0x00000400;     /* set if open files shared between processes */
        public const uint CLONE_SIGHAND = 0x00000800;     /* set if signal handlers and blocked signals shared */
        public const uint CLONE_PTRACE = 0x00002000;     /* set if we want to let tracing continue on the child too */
        public const uint CLONE_VFORK = 0x00004000;     /* set if the parent wants the child to wake it up on mm_release */
        public const uint CLONE_PARENT = 0x00008000;     /* set if we want to have the same parent as the cloner */
        public const uint CLONE_THREAD = 0x00010000;     /* Same thread group? */
        public const uint CLONE_NEWNS = 0x00020000;     /* New namespace group? */
        public const uint CLONE_SYSVSEM = 0x00040000;     /* share system V SEM_UNDO semantics */
        public const uint CLONE_SETTLS = 0x00080000;     /* create a new TLS for the child */
        public const uint CLONE_PARENT_SETTID = 0x00100000;     /* set the TID in the parent */
        public const uint CLONE_CHILD_CLEARTID = 0x00200000;     /* clear the TID in the child */
        public const uint CLONE_DETACHED = 0x00400000;     /* Unused, ignored */
        public const uint CLONE_UNTRACED = 0x00800000;     /* set if the tracing process can't force CLONE_PTRACE on this clone */
        public const uint CLONE_CHILD_SETTID = 0x01000000;     /* set the TID in the child */
        public const uint CLONE_NEWUTS = 0x04000000;     /* New utsname group? */
        public const uint CLONE_NEWIPC = 0x08000000;     /* New ipcs */
        public const uint CLONE_NEWUSER = 0x10000000;     /* New user namespace */
        public const uint CLONE_NEWPID = 0x20000000;     /* New pid namespace */
        public const uint CLONE_NEWNET = 0x40000000;     /* New network namespace */
        public const uint CLONE_IO = 0x80000000;     /* Clone io context */

        //
        // We do sync read for this one, since it's simpler..
        //
        public static Process CreateProcess(ASCIIString path, ASCIIString[] argv, ASCIIString[] envp, AndroidApplicationInfo appInfo)
        {
            var proc = new Process(path, appInfo);
            Utils.Assert(!proc.Space.impl._value.isInvalid);

            uint addr;
            int workspace_fd;
            uint workspace_size;
            proc.helperPid = Arch.IPCStubs.linux_sys_take_helper(out addr, out workspace_fd, out workspace_size);

            if (proc.helperPid < 0)
            {
                Arch.Console.WriteLine("CreateProcess: cannot get helper");
                return null;
            }

            proc.ShadowBinderVMStart = addr;

            ErrorCode ec;
            var inode = Arch.ArchFS.Open(proc.helperPid, path, 0, 0, out ec);
            if (inode == null)
            {
                Arch.Console.WriteLine("CreateProcess: cannot open file");
                return null;
            }

            var stack_top = new UserPtr(INITIAL_STACK_LOCATION);

            // 4M Initial stack
            var stack_size = 4096 * Arch.ArchDefinition.PageSize;
            proc.Space.AddStackMapping(stack_top, stack_size);
            stack_top += stack_size;

            var augmented_envp = CreateEnvpArrayWithWorkspace(envp, proc, workspace_fd, workspace_size);

            var envp_ptr = PushCharArray(proc, augmented_envp, ref stack_top);
            if (envp_ptr == null)
            {
                Arch.Console.WriteLine("CreateProcess: Push envp failed");
                return null;
            }

            var argv_ptr = PushCharArray(proc, argv, ref stack_top);
            if (argv_ptr == null)
            {
                Arch.Console.WriteLine("CreateProcess: Push argv failed");
                return null;
            }

            stack_top = UserPtr.RoundDown(stack_top);

            // Parse the ELF file, which might push additional info on to the stack
            // (i.e., aux vectors when the ELF is dynamically linked)
            var file = new File(proc, inode, FileFlags.ReadWrite, 0);

            int ret = ELF32Header.Parse(proc.helperPid, file, proc, ref stack_top);
            
            if (ret != 0)
            {
                Arch.Console.WriteLine("CreateProcess: Parse ELF file failed");
                return null;
            }

            //%esp         The stack contains the arguments and environment:
            //     0(%esp)                 argc
            //     4(%esp)                 argv[0]
            //     ...
            //     (4*argc)(%esp)          NULL
            //     (4*(argc+1))(%esp)      envp[0]
            //     ...
            //                             NULL
            if (PushArgumentPointers(proc, envp_ptr, ref stack_top) != 0)
                return null;

            if (PushArgumentPointers(proc, argv_ptr, ref stack_top) != 0)
                return null;

            if (PushInt(proc, argv_ptr.Length, ref stack_top) != 0)
                return null;

   
            // Stdio
            var file_stdout = File.CreateStdout(proc);
            Contract.Assume(proc.Files.IsAvailableFd(Process.STDOUT_FD));
            proc.InstallFd(Process.STDOUT_FD, file_stdout);

            var file_stderr = File.CreateStdout(proc);
            Contract.Assume(proc.Files.IsAvailableFd(Process.STDERR_FD));
            proc.InstallFd(Process.STDERR_FD, file_stderr);

            var mainThread = Thread.Create(proc);

            if (appInfo != null)
            {
                var p = appInfo.ToParcel();
                Globals.LinuxIPCBuffer.CopyFrom(0, p);
                Arch.IPCStubs.WriteAppInfo(proc.helperPid, p.Length);
            }

            // Start the main thread
            mainThread.Start(new Pointer(proc.EntryPoint), stack_top.Value);
            return proc;
        }

        
        private static ASCIIString[] CreateEnvpArrayWithWorkspace(ASCIIString[] envp, Process proc, int workspace_fd, uint workspace_size)
        {
            var res = new ASCIIString[envp.Length + 1];
            for (int i = 0; i < envp.Length; ++i)
                res[i] = envp[i];

            var inode = new Arch.ArchINode(workspace_fd, workspace_size, proc.helperPid);
         
            var file = new File(proc, inode, FileFlags.ReadOnly, 0);

            var fd = proc.GetUnusedFd();
            proc.InstallFd(fd, file);

            var s = "ANDROID_PROPERTY_WORKSPACE=" + fd.ToString() + "," + workspace_size.ToString();
            res[envp.Length] = new ASCIIString(s);

            return res;
        }

        private static int PushArgumentPointers(Process proc, UserPtr[] arr, ref UserPtr stack_top)
        {
            if (PushInt(proc, 0, ref stack_top) != 0)
                return -1;

            for (int i = arr.Length - 1; i >= 0; --i)
            {
                var p = arr[i];
                if (PushInt(proc, p.Value.ToInt32(), ref stack_top) != 0)
                    return -1;
            }
            return 0;
        }

        private static int PushInt(Process proc, int v, ref UserPtr stack_top)
        {
            stack_top -= sizeof(int);
            if (stack_top.Write(proc, v) != 0)
                return -1;

            return 0;
        }

        private static UserPtr[] PushCharArray(Process proc, ASCIIString[] arr, ref UserPtr stack_top)
        {
            var res = new UserPtr[arr.Length];
            for (int i = 0; i < arr.Length; ++i)
            {
                // Include the terminator
                stack_top -= arr[i].Length + 1;
                res[i] = stack_top;
                if (stack_top.Write(proc, arr[i].GetByteString()) != 0)
                    return null;
            }
            return res;
        }

        public static int Clone(Thread current, uint flags, UserPtr newsp, UserPtr parent_tidptr, UserPtr child_tidptr, ref Arch.ExceptionRegisters pt_regs)
        {
            // Only support pthread_create right now
            if (flags != (CLONE_FILES | CLONE_FS | CLONE_VM | CLONE_SIGHAND
                | CLONE_THREAD | CLONE_SYSVSEM | CLONE_DETACHED))
                return -ErrorCode.EINVAL;

            var proc = current.Parent;
            var thr = Thread.Create(proc);

            if (thr == null)
            {
                Arch.Console.WriteLine("Failed to create thread");
                return -ErrorCode.EINVAL;
            }

            // Start the main thread
            // Skipping the int $0x80
            thr.Start(new Pointer(pt_regs.ip + 2), newsp.Value);

            // There's no need to set eax for thr as they are zero by default..

            return thr.Tid;
        }
    }
}
