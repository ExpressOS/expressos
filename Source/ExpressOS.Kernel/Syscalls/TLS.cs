using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpressOS.Kernel;
using ExpressOS.Kernel.Arch;
using System.Runtime.InteropServices;

using Arch = ExpressOS.Kernel.Arch;

namespace ExpressOS.Kernel
{
    public static class TLS
    {
        private const int USER_DESC_SIZE = 16;

        public static int SetThreadArea(Thread current, UserPtr userDescriptor)
        {
            var info = new userdesc();
            if (userDescriptor.Read(current, out info) != 0)
                return -ErrorCode.EFAULT;

            var ret = NativeMethods.l4api_set_thread_area(current.impl._value.thread, current.TLSArray, -1, ref info, 1);

            if (userDescriptor.Write(current, info) != 0)
                return -ErrorCode.EFAULT;

            return ret;
        }
    }
}
