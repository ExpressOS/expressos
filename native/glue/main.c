#include "expressos/expressos-native.h"
#include "expressos/printk.h"
#include "expressos/mm.h"
#include "expressos/l4api.h"

#include <l4/re/env.h>

static struct expressos_boot_params boot_params;

int expressos_init(void);

int main()
{
        int ret = 0;
        char *ipc_buf_end;
        char *completion_queue_start, *completion_queue_end;

        /*
         * Set the priority to 10 (which is lower than L4Linux), so that
         * it won't compete against it.
         */
        l4api_set_priority(l4re_env()->main_thread, 10);

        ret = expressos_init();
        if (ret) {
                printk("Failed to initialize ExpressOS, ret=%d\n", ret);
                goto out;
        }

        ipc_buf_end = g_expressos_ipc_shm_buf + EXPRESSOS_IPC_BUF_SIZE;
        completion_queue_start = g_expressos_ipc_shm_buf
                        + EXPRESSOS_IPC_SYNC_CALL_BUF_SIZE + EXPRESSOS_CONTROL_BLOCK_SIZE;
        completion_queue_end = ipc_buf_end;

        printk("Entering ExpressOS\n"
               "Linux tid: %lu\n"
               "Stack: %p~%p\n"
               "Heap: %p~%p\n"
               "Shared memory: %p~%p\n"
               "Compltion queue start:%p~%p\n"
               "Main memory: %p~%p\n"
               "Linux main memory:%p~%p\n"
               "\n",
               g_linux_server_tid,
               g_stack_and_heap_start, g_stack_end,
               g_stack_end, g_stack_end + STACK_AND_HEAP_SIZE - STACK_SIZE,
               g_expressos_ipc_shm_buf, g_expressos_ipc_shm_buf + EXPRESSOS_IPC_BUF_SIZE,
               completion_queue_start, completion_queue_end,
               g_expressos_main_memory_start,
               g_expressos_main_memory_start + EXPRESSOS_MAIN_MEMORY_SIZE,
               g_linux_main_memory_start, g_linux_main_memory_start + g_linux_main_memory_size
               );

        struct expressos_boot_params p = {
                .main_memory_start          = (unsigned long)g_expressos_main_memory_start,
                .main_memory_size           = EXPRESSOS_MAIN_MEMORY_SIZE,
                .linux_server_tid           = g_linux_server_tid,
                .linux_main_memory_start    = (unsigned long)g_linux_main_memory_start,
                .linux_main_memory_size     = (unsigned long)g_linux_main_memory_size,
                .sync_ipc_shm_base          = (unsigned long)g_expressos_ipc_shm_buf,
                .sync_ipc_shm_size          = EXPRESSOS_IPC_SYNC_CALL_BUF_SIZE,
                .completion_queue_buf_start = (unsigned long)completion_queue_start,
                .completion_queue_size      = completion_queue_end - completion_queue_start,
        };

        /*
         * The boot_param struct has to be placed on the BSS, as we
         * explicitly switch the stack before entering the managed
         * code.
         */
        boot_params = p;
        asm volatile ("movl %0, %%esp" : : "r"(g_stack_end));

        csharp_main(&boot_params);
        
out:
        printk("The ExpressOS kernel exited\n");
        return ret;
}
