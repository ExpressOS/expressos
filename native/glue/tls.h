#ifndef VANDROID_KERNEL_L4X_TLS_H_
#define VANDROID_KERNEL_L4X_TLS_H_

#include <l4/sys/types.h>

#define GDT_ENTRY_TLS_MIN       l4api_fiasco_gdt_entry_offset
#define GDT_ENTRY_TLS_MAX       (GDT_ENTRY_TLS_MIN + GDT_ENTRY_TLS_ENTRIES - 1)
#define GDT_ENTRY_TLS_ENTRIES   3

/* Maximum number of LDT entries supported. */
#define LDT_ENTRIES     8192

/* The size of each LDT entry. */
#define LDT_ENTRY_SIZE  8

/*
 * FIXME: Accessing the desc_struct through its fields is more elegant,
 * and should be the one valid thing to do. However, a lot of open code
 * still touches the a and b accessors, and doing this allow us to do it
 * incrementally. We keep the signature as a struct, rather than an union,
 * so we can get rid of it transparently in the future -- glommer
 */
/* 8 byte segment descriptor */
struct desc_struct {
        union {
                struct {
                        unsigned int a;
                        unsigned int b;
                };
                struct {
                        unsigned short limit0;
                        unsigned short base0;
                        unsigned base1: 8, type: 4, s: 1, dpl: 2, p: 1;
                        unsigned limit: 4, avl: 1, l: 1, d: 1, g: 1, base2: 8;
                };
        };
} __attribute__((packed));

/*
 * Note on 64bit base and limit is ignored and you cannot set DS/ES/CS
 * not to the default values if you still want to do syscalls. This
 * call is more for 32bit mode therefore.
 */
struct user_desc {
        unsigned int  entry_number;
        unsigned int  base_addr;
        unsigned int  limit;
        unsigned int  seg_32bit:1;
        unsigned int  contents:2;
        unsigned int  read_exec_only:1;
        unsigned int  limit_in_pages:1;
        unsigned int  seg_not_present:1;
        unsigned int  useable:1;
        unsigned int  empty:25;
} __attribute__((packed));

#endif
