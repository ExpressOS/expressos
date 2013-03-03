#ifndef EXPRESSOS_NATIVE_H_
#define EXPRESSOS_NATIVE_H_

#include "expressos/linux.h"
#include <l4/sys/types.h>

#define EXPRESSOS_MAIN_MEMORY_SIZE (96 * 1024 * 1024)
#define PAGE_SIZE                  4096

/* entrance of the managed code */
int csharp_main(const struct expressos_boot_params *);

/* Shared memory buffer to pass arguments back and forth */
extern char          *g_expressos_ipc_shm_buf;
extern l4_cap_idx_t   g_linux_server_tid;
extern l4_cap_idx_t   g_main_thread_tid;
extern l4_cap_idx_t   g_main_mem_ds;
extern void          *g_expressos_main_memory_start;
extern char          *g_linux_main_memory_start;
extern unsigned long  g_linux_main_memory_size;

struct expressos_control_block;
extern struct expressos_control_block *g_expressos_control_block;

#endif
