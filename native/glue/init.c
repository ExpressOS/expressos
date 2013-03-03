#include "expressos/expressos-native.h"
#include "expressos/errno.h"
#include "expressos/printk.h"
#include "expressos/mm.h"

#include <l4/re/env.h>
#include <l4/sys/ipc_gate.h>

l4_cap_idx_t g_linux_server_tid = L4_INVALID_CAP;
l4_cap_idx_t g_main_thread_tid = L4_INVALID_CAP;

struct expressos_control_block *g_expressos_control_block;

static int init_ipc_gate(void);

int init_l4api_tls(void);

int expressos_init(void)
{
        int ret;
        g_main_thread_tid = l4re_env()->main_thread;
        
        g_linux_server_tid = l4re_get_env_cap(EXPRESSOS_GLUE_GATE);
        if (l4_is_invalid_cap(g_linux_server_tid))
                return -ENOENT;

        if ((ret = init_ipc_gate()))
                return ret;

        if ((ret = init_l4api_tls()))
                return ret;
        
        if ((ret = init_mm(STACK_AND_HEAP_SIZE)))
                return ret;

        /* Wait for Linux */
        if ((ret = init_shm()))
                return ret;
        
        return 0;
}


static int init_ipc_gate(void)
{
        l4_cap_idx_t gate;
        l4_msgtag_t tag;

	gate = l4re_get_env_cap(EXPRESSOS_GATE);
        if (l4_is_invalid_cap(gate))
                return -1;

        tag = l4_ipc_gate_bind_thread(gate, g_main_thread_tid, g_linux_server_tid);
        if (l4_ipc_error(tag, l4_utcb()))
                return -1;

        return 0;
}

