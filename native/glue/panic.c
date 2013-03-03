#include "expressos/printk.h"

static inline void dump_backtrace()
{
        long *ebp;
        asm ("movl %%ebp, %0" : "=r"(ebp));
        while (ebp)
        {
                long eip = *(ebp + 1);
                printk("EIP: %lx\n", eip);
                ebp = (long*)*ebp;
        }
}

void panic()
{
        printk("Panic!, backtrace:\n");
        dump_backtrace();
        *(volatile char*)0xdeadbeef;
        __builtin_unreachable();
}
