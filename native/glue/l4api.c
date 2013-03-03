#include <l4/sys/thread.h>
#include <l4/sys/kip.h>

l4_mword_t l4api_ipc_send(l4_cap_idx_t dest, l4_utcb_t * utcb, l4_msgtag_t tag, l4_timeout_t timeout)
{
        return l4_ipc_send(dest, utcb, tag, timeout).raw;
}

l4_umword_t l4api_ipc_send_and_wait(l4_cap_idx_t dest, l4_utcb_t *utcb, l4_msgtag_t tag,
                     l4_umword_t *label, l4_timeout_t timeout)
{
        return l4_ipc_send_and_wait(dest, utcb, tag, label, timeout).raw;
}

l4_umword_t l4api_ipc_wait(l4_utcb_t *utcb, l4_umword_t *label, l4_timeout_t timeout)
{
        return l4_ipc_wait(utcb, label, timeout).raw;
}

l4_mword_t l4api_ipc_call(l4_cap_idx_t dest, l4_utcb_t *utcb, l4_msgtag_t tag, l4_timeout_t timeout)
{
        return l4_ipc_call(dest, utcb, tag, timeout).raw;
}

l4_mword_t l4api_ipc_error(l4_msgtag_t tag, l4_utcb_t *utcb)
{
        return l4_ipc_error(tag, utcb);
}

l4_utcb_t * l4api_utcb(void)
{
        return l4_utcb();
}

l4_exc_regs_t* l4api_utcb_exc(void)
{
        return l4_utcb_exc();
}

l4_msg_regs_t* l4api_utcb_mr(void)
{
        return l4_utcb_mr();
}

l4_thread_regs_t* l4api_utcb_tcr(void)
{
        return l4_utcb_tcr();
}

l4_umword_t l4api_thread_yield(void)
{
        l4_thread_yield();
        return 0;
}

l4_cpu_time_t l4api_get_system_clock(void)
{
        extern char __L4_KIP_ADDR__[];
        l4_kernel_info_t * kip = (l4_kernel_info_t*)(__L4_KIP_ADDR__);
        return kip->clock;
}
