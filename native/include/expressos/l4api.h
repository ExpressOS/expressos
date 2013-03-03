#ifndef EXPRESSOS_KERNEL_L4API_H_
#define EXPRESSOS_KERNEL_L4API_H_

#include <l4/re/c/util/cap.h>

struct l4api_thread_info {
        l4_cap_idx_t thread;
        l4_cap_idx_t gate;
};

struct silk_System_Array;
l4_cap_idx_t l4api_create_task(struct silk_System_Array *name, l4_utcb_t *utcb_area, unsigned utcb_log2_size);
int l4api_create_thread(l4_utcb_t *utcb, l4_cap_idx_t parent, struct l4api_thread_info *ret);
int l4api_delete_thread(struct l4api_thread_info thr);
int l4api_start_thread(l4_cap_idx_t thread, l4_umword_t ip, l4_umword_t sp);
int l4api_set_priority(l4_cap_idx_t thread, int priority);

#endif
