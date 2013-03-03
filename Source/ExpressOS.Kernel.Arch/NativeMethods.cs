using ExpressOS.Kernel.Arch;
using System;
using System.Runtime.InteropServices;

namespace ExpressOS.Kernel.Arch
{
    public static class NativeMethods
    {
        [DllImport("glue")]
        internal static extern int console_putchar(int c);
        [DllImport("glue")]
        internal static extern void console_flush();

        //[DllImport("glue")]
        //internal static extern int linux_console_putchar(int c);
        //[DllImport("glue")]
        //internal static extern void linux_console_flush();
        internal static int linux_console_putchar(int c) { return 0; }
        internal static void linux_console_flush() { }

        [DllImport("glue")]
        public static extern IntPtr l4api_tls_array_alloc();
        [DllImport("glue")]
        public static extern void l4api_tls_array_free(IntPtr tls);
        [DllImport("glue")]
        public static extern int l4api_set_thread_area(L4Handle thread_id, IntPtr tls_array, int idx,
            ref userdesc info, int can_allocate);

        [DllImport("glue")]
        public static extern int linux_pending_reply_count();
        [DllImport("glue")]
        internal static extern L4Handle l4api_create_task(byte[] name, Pointer utcb_area, int utcb_log2_size);
        [DllImport("glue")]
        internal static extern int l4api_create_thread(Pointer utcb, L4Handle parent, out ThreadInfo info);
        [DllImport("glue")]
        internal static extern void l4api_delete_thread(ThreadInfo info);
        [DllImport("glue")]
        internal static extern int l4api_start_thread(L4Handle thread, Pointer ip, Pointer sp);
        [DllImport("glue")]
        public static extern void l4api_flush_regions(L4Handle l4Handle, Pointer StartAddress, Pointer End, int unmap_rights);

        // L4-specific calls;
        [DllImport("glue")]
        public static extern Msgtag l4api_ipc_send(L4Handle dest, Pointer utcb, Msgtag tag, Timeout timeout);
        [DllImport("glue")]
        public static extern Msgtag l4api_ipc_wait(Pointer utcb, out L4Handle src, Timeout timeout);
        [DllImport("glue")]
        public static extern Msgtag l4api_ipc_send_and_wait(L4Handle dest, Pointer utcb, Msgtag tag, out L4Handle src, Timeout timeout);
        [DllImport("glue")]
        public static extern Pointer l4api_utcb();
        [DllImport("glue")]
        public static unsafe extern ExceptionRegisters *l4api_utcb_exc();
        [DllImport("glue")]
        public static unsafe extern MessageRegisters *l4api_utcb_mr();
        [DllImport("glue")]
        public static extern ulong l4api_get_system_clock();
        [DllImport("glue")]
        internal static extern unsafe ThreadRegister *l4api_utcb_tcr();
        [DllImport("glue")]
        internal static extern Msgtag l4api_ipc_call(L4Handle dest, Pointer utcb, Msgtag tag, Timeout timeout);
        [DllImport("glue")]
        internal static extern int l4api_ipc_error(Msgtag tag, Pointer utcb);

        [DllImport("glue")]
        internal static extern void panic();
    }
}
