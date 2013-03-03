#include "expressos/expressos-native.h"
#include "expressos/mm.h"
#include "expressos/printk.h"

#include <l4/re/c/dataspace.h>
#include <l4/re/c/util/cap_alloc.h>
#include <l4/re/c/mem_alloc.h>
#include <l4/re/c/rm.h>
#include <l4/sys/err.h>
#include <l4/sys/ipc_gate.h>

static l4re_ds_t  stack_and_heap_ds;
static char      *heap_ptr;
static char      *heap_end;

char             *g_stack_and_heap_start;
void             *g_stack_end;
char             *g_expressos_ipc_shm_buf;
void             *g_expressos_main_memory_start;
char             *g_linux_main_memory_start;
unsigned long     g_linux_main_memory_size;
l4_cap_idx_t      g_main_mem_ds;

static int init_main_memory(void);

int init_mm(size_t size)
{
        int r;

        if ((r = init_main_memory()))
                return r;
        
        /* Allocate a free capability index for our data space */
        stack_and_heap_ds = l4re_util_cap_alloc();
        if (l4_is_invalid_cap(stack_and_heap_ds))
                return -L4_ENOMEM;

        /* Allocate memory via a dataspace */
        if ((r = l4re_ma_alloc(size, stack_and_heap_ds, L4RE_MA_SUPER_PAGES)))
                return r;

        if ((r = l4re_rm_attach((void**)&g_stack_and_heap_start, size,
                                L4RE_RM_SEARCH_ADDR | L4RE_RM_EAGER_MAP,
                                stack_and_heap_ds, 0,
                                L4_SUPERPAGESHIFT)))
                return r;


        // Reserve the lowest page of the stack to detect overflow
        l4_addr_t p = (l4_addr_t)g_stack_and_heap_start;
        if ((r = l4re_rm_reserve_area((l4_addr_t*)&p, PAGE_SIZE,
                                      L4RE_RM_OVERMAP | L4RE_RM_RESERVED | L4RE_RM_IN_AREA,
                                      L4_PAGESHIFT)))
                return r;

        g_stack_end = (char*)g_stack_and_heap_start + STACK_SIZE;
        heap_ptr = (char*)g_stack_and_heap_start + STACK_SIZE;
        heap_end = (char*)g_stack_and_heap_start + size;

        return 0;
}

static int init_main_memory(void)
{
        int r;
        if ((r = l4_is_invalid_cap(g_main_mem_ds = l4re_util_cap_alloc())))
                return r;

	if ((r = l4re_ma_alloc(EXPRESSOS_MAIN_MEMORY_SIZE, g_main_mem_ds,
                               L4RE_MA_SUPER_PAGES)))
                return r;

        if ((r = l4re_rm_attach(&g_expressos_main_memory_start,
                                EXPRESSOS_MAIN_MEMORY_SIZE,
                                L4RE_RM_EAGER_MAP | L4RE_RM_SEARCH_ADDR, g_main_mem_ds, 0,
                                L4_SUPERPAGESHIFT)))
                return r;

        return 0;
}

int init_shm(void)
{
        int err;
        l4_cap_idx_t src;
        l4_utcb_t *u = l4_utcb();
        l4_msg_regs_t *mr = l4_utcb_mr_u(u);
        l4_buf_regs_t *br = l4_utcb_br_u(u);
        l4re_ds_t linux_main_memory_ds = l4re_util_cap_alloc();
        l4re_ds_t ipc_shm_ds = l4re_util_cap_alloc();
        l4_umword_t ipc_buf_size, ipc_control_block_off;

        br->br[0] = ipc_shm_ds
                        | L4_RCV_ITEM_SINGLE_CAP | L4_RCV_ITEM_LOCAL_ID;
        br->br[1] = linux_main_memory_ds
                        | L4_RCV_ITEM_SINGLE_CAP | L4_RCV_ITEM_LOCAL_ID;
        br->bdr = 0;

        l4_msgtag_t tag = l4_ipc_wait(u, &src, L4_IPC_NEVER);

        if (l4_msgtag_has_error(tag))
                return -1;

        ipc_buf_size             = mr->mr[1];
        ipc_control_block_off    = mr->mr[2];
        g_linux_main_memory_size = mr->mr[3];
        ipc_shm_ds               = br->br[0];
        linux_main_memory_ds     = br->br[1];

        if (l4_is_invalid_cap(ipc_shm_ds) || l4_is_invalid_cap(linux_main_memory_ds))
                return -1;

        err = l4re_rm_attach((void**)(&g_expressos_ipc_shm_buf),
                             ipc_buf_size,
                             L4RE_RM_EAGER_MAP | L4RE_RM_SEARCH_ADDR,
                             ipc_shm_ds, 0, L4_SUPERPAGESHIFT);

        if (err)
                return err;

        err = l4re_rm_attach((void**)(&g_linux_main_memory_start),
                             g_linux_main_memory_size,
                             L4RE_RM_EAGER_MAP | L4RE_RM_SEARCH_ADDR,
                             linux_main_memory_ds, 0, L4_SUPERPAGESHIFT);

        if (err)
                return err;

        g_expressos_control_block = (struct expressos_control_block*)
                        (g_expressos_ipc_shm_buf + ipc_control_block_off);

        return 0;
}

void *gc_malloc(size_t size)
{
        if (heap_ptr + size > heap_end)
        {
                printk("gc_malloc: out of memory, heap_ptr=%p, size=%d, heap_end=%p\n",
                       heap_ptr, size, heap_end);
                return NULL;
        }

        void *r = heap_ptr;
        heap_ptr += size;
        return r;
}

void gc_status(void)
{
        printk("heap:%p~%p, current_ptr:%p\n",
               (char*)g_stack_and_heap_start + STACK_SIZE,
               heap_end,
               heap_ptr);
}
