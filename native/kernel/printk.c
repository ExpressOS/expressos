#include <l4/re/env.h>
#include <l4/re/c/log.h>

#include <stdarg.h>
#include <stddef.h>

int vsnprintk(char *str, size_t size, const char *fmt, va_list ap);

int printk(const char *fmt, ...)
{
        char outbuf[256];
        va_list ap;
        int r;

        /*
         * Safety check
         */
        if (fmt == NULL)
                return 0;

        /*
         * Print into buffer.
         */
        va_start(ap, fmt);
        r = vsnprintk(outbuf, sizeof(outbuf), fmt, ap);
        va_end(ap);

        /*
         * Output to terminal.
         */
        if (r > 0)
                l4re_log_printn(outbuf, r);

        return r;
}
