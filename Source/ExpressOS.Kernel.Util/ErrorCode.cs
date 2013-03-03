using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExpressOS.Kernel
{
    public struct ErrorCode
    {
        public const int NoError = 0;

        public const int EPERM = 1;   /* Operation not permitted */
        public const int ENOENT = 2;   /* No such file or directory */
        public const int ESRCH = 3;   /* No such process */
        public const int EINTR = 4;   /* Interrupted system call */
        public const int EIO = 5;   /* I/O error */
        public const int ENXIO = 6;   /* No such device or address */
        public const int E2BIG = 7;   /* Argument list too long */
        public const int ENOEXEC = 8;   /* Exec format error */
        public const int EBADF = 9;   /* Bad file number */
        public const int ECHILD = 10;   /* No child processes */
        public const int EAGAIN = 11;   /* Try again */
        public const int ENOMEM = 12;   /* Out of memory */
        public const int EACCES = 13;   /* Permission denied */
        public const int EFAULT = 14;   /* Bad address */
        public const int ENOTBLK = 15;   /* Block device required */
        public const int EBUSY = 16;   /* Device or resource busy */
        public const int EEXIST = 17;   /* File exists */
        public const int EXDEV = 18;   /* Cross-device link */
        public const int ENODEV = 19;   /* No such device */
        public const int ENOTDIR = 20;   /* Not a directory */
        public const int EISDIR = 21;   /* Is a directory */
        public const int EINVAL = 22;   /* Invalid argument */
        public const int ENFILE = 23;   /* File table overflow */
        public const int EMFILE = 24;   /* Too many open files */
        public const int ENOTTY = 25;   /* Not a typewriter */
        public const int ETXTBSY = 26;   /* Text file busy */
        public const int EFBIG = 27;   /* File too large */
        public const int ENOSPC = 28;   /* No space left on device */
        public const int ESPIPE = 29;   /* Illegal seek */
        public const int EROFS = 30;   /* Read-only file system */
        public const int EMLINK = 31;   /* Too many links */
        public const int EPIPE = 32;   /* Broken pipe */
        public const int EDOM = 33;   /* Math argument out of domain of func */
        public const int ERANGE = 34;   /* Math result not representable */
        public const int EDEADLK = 35;
        public const int ENAMETOOLONG = 36;
        public const int ENOLCK = 37;
        public const int ENOSYS = 38;
        public const int ENOTEMPTY = 39;
        public const int EWOULDBLOCK = EAGAIN;
        public const int ENOTSOCK = 88;
        public const int ENOBUFS = 105;
        public const int ETIMEDOUT = 110;
        public int Code;

        
        public bool HasError()
        {
            return Code != NoError;
        }

        public int Errno()
        {
            return -Code;
        }
    }
}
