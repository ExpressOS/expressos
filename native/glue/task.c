#include "expressos/expressos-native.h"
#include "expressos/pinvoke.h"
#include "expressos/printk.h"
#include "expressos/l4api.h"

#include <l4/sys/debugger.h>
#include <l4/sys/factory.h>
#include <l4/sys/ipc_gate.h>
#include <l4/sys/scheduler.h>
#include <l4/sys/thread.h>

#include <l4/re/c/util/cap_alloc.h>
#include <l4/re/env.h>

#include <stddef.h>

l4_cap_idx_t l4api_create_task(struct silk_System_Array *name, l4_utcb_t * utcb_area, unsigned utcb_log2_size)
{
        l4_cap_idx_t cap = l4re_util_cap_alloc();
        l4_fpage_t utcb_fpage = l4_fpage((l4_umword_t)utcb_area, utcb_log2_size, 0);

        l4_msgtag_t res = l4_factory_create_task(l4re_env()->factory, cap, utcb_fpage);

        if (l4_error(res)) {
                printk("l4api_create_task: failed utcb_area=%p, utcb_log2_size=%d\n", utcb_area, utcb_log2_size);
                goto err;
        }

        if (name)
                l4_debugger_set_object_name(cap, (const char*)name->base);

        return cap;

err:
        l4re_util_cap_free(cap);
        return L4_INVALID_CAP;

}

int l4api_create_thread(l4_utcb_t *utcb, l4_cap_idx_t parent, struct l4api_thread_info *ret)
{
        l4_cap_idx_t cap  = l4re_util_cap_alloc();
        l4_cap_idx_t gate = l4re_util_cap_alloc();

        l4_msgtag_t res = l4_factory_create_thread(l4re_env()->factory, cap);

        if (l4_error(res))
                goto err;

        res = l4_factory_create_gate(l4re_env()->factory, gate, g_main_thread_tid, cap);

        if (l4_error(res))
                goto err;

        /* Enable the new task to send IPC to the pager */
        res = l4_task_map(parent, L4RE_THIS_TASK_CAP,
                          l4_obj_fpage(gate, 0, L4_FPAGE_RWX),
                          l4_map_obj_control(gate, L4_MAP_ITEM_MAP));

        if (l4_error(res))
                goto err;

        l4_thread_control_start();
        l4_thread_control_pager(gate);
        l4_thread_control_exc_handler(gate);
        /* Attach the thread to the address space */
        l4_thread_control_bind(utcb, parent);
        
        /* Alien mode: the thread can't call L4 IPC directly */
        // l4_thread_control_alien(1);
        res = l4_thread_control_commit(cap);

        if (l4_error(res)) {
                printk("l4api_create_thread: thread_control failed\n");
                goto err;
        }

        // printk("l4api_create_thread: cap=%lx, utcb=%p\n", cap, utcb);

        ret->thread = cap;
        ret->gate = gate;

        return 0;

err:
        l4re_util_cap_free(cap);
        l4re_util_cap_free(gate);
        return -1;
}

int l4api_start_thread(l4_cap_idx_t thread, l4_umword_t ip, l4_umword_t sp)
{
        l4_msgtag_t tag = l4_thread_ex_regs(thread, ip, sp, 0);
        return l4_error(tag);
}

static int l4api_task_delete_obj(l4_cap_idx_t obj)
{
        l4_msgtag_t t;
        l4_utcb_t *u = l4_utcb();
        t = l4_task_unmap_u(L4RE_THIS_TASK_CAP,
                            l4_obj_fpage(obj, 0, L4_FPAGE_RWX),
                            L4_FP_DELETE_OBJ, u);
        return l4_error_u(t, u);
}

int l4api_delete_thread(struct l4api_thread_info thr)
{
        if (l4api_task_delete_obj(thr.thread)
            || l4api_task_delete_obj(thr.gate)) {
                printk("Failed to kill thread %lx\n", thr.thread);
                return -1;
        }

        l4re_util_cap_free(thr.thread);
        l4re_util_cap_free(thr.gate);
        return 0;
}

int l4api_set_priority(l4_cap_idx_t thread, int priority)
{
        l4_sched_param_t l4sp = l4_sched_param(priority, 0);
        l4_msgtag_t tag = l4_scheduler_run_thread(l4re_env()->scheduler, thread, &l4sp);
        return l4_error(tag);
}

static inline int fls(int x)
{
        int r = 32;
        if (!x)
                return 0;

        if (!(x & 0xffff0000u)) {
                x <<= 16;
                r -= 16;
        }
        if (!(x & 0xff000000u)) {
                x <<= 8;
                r -= 8;
        }
        if (!(x & 0xf0000000u)) {
                x <<= 4;
                r -= 4;
        }
        if (!(x & 0xc0000000u)) {
                x <<= 2;
                r -= 2;
        }
        if (!(x & 0x80000000u)) {
                x <<= 1;
                r -= 1;
        }
        return r;
}

static size_t split_region(unsigned long *start, unsigned long end, unsigned long rights,
                           l4_fpage_t fpages[], size_t len)
{
        size_t ret = 0;
        while (ret < len && *start < end)
        {
                int p = __builtin_ffs(*start);
                int q = fls(end - *start);
                int r = p > q ? q : p;
                fpages[ret++] = l4_fpage(*start, r - 1, rights);
                *start += 1 << (r - 1);
        }
        return ret;
}

void l4api_flush_regions(l4_cap_idx_t task,
                       unsigned long vaddr_start,
                       unsigned long vaddr_end,
                       unsigned long flush_rights)
{
        static const size_t PAGE_MASK = ~4095;

	if (l4_is_invalid_cap(task))
                return;

        vaddr_start &= PAGE_MASK;
        vaddr_end &= PAGE_MASK;

        /* Direct flush in the child, use virtual address in the
           child address space */

        while (vaddr_start < vaddr_end)
        {
                l4_fpage_t fpages[L4_UTCB_GENERIC_DATA_SIZE - 2];
                int num_pages = split_region(&vaddr_start, vaddr_end, flush_rights, fpages, sizeof(fpages) / sizeof(l4_fpage_t));
                l4_task_unmap_batch(task, fpages, num_pages, L4_FP_ALL_SPACES);
        }
}

