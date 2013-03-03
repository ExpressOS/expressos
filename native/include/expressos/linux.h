#ifndef EXPRESSOS_LINUX_H_
#define EXPRESSOS_LINUX_H_

#define EXPRESSOS_GLUE_GATE "linux_server"
#define EXPRESSOS_GATE      "vandroid"

/*
 * Layout of the shared IPC message buffer
 *
 * |Buffer for synchronous call| |Control block| |Buffer for asynchronous call|
 *
 * The size of the buffer of synchronous IPC calls has to be at least 65k,
 * because recvfrom() is a synchronous call.
 *
 * The asynchronous call buffer has to be aligned with 4k boundary due to
 * the allocator.
 */
#define EXPRESSOS_IPC_BUF_SIZE           (2 * 1024 * 1024)
#define EXPRESSOS_IPC_SYNC_CALL_BUF_SIZE (68 * 1024)
#define EXPRESSOS_CONTROL_BLOCK_SIZE     4096
#define EXPRESSOS_CONTROL_BLOCK_OFFSET   EXPRESSOS_IPC_SYNC_CALL_BUF_SIZE

/*
 * pending_reply_count records the number of pending responses from
 * Linux to ExpressOS. The access is racy but here we take advantage
 * of the fact that accessing aligned 32-bit integer is atomic under
 * X86.
 */
struct expressos_control_block {
        unsigned int pending_reply_count;
};

/* Keep in sync with the definition of managed environment */
struct expressos_boot_params {
        unsigned long main_memory_start;
        unsigned long main_memory_size;
        unsigned long linux_server_tid;
        unsigned long linux_main_memory_start;
        unsigned long linux_main_memory_size;
        unsigned long sync_ipc_shm_base;
        unsigned long sync_ipc_shm_size;
        unsigned long completion_queue_buf_start;
        unsigned long completion_queue_size;
};

enum {
        EXPRESSOS_CMD_KICKSTART,
        EXPRESSOS_CMD_ENABLE_PROFILER,
        EXPRESSOS_CMD_DISABLE_PROFILER,
        EXPRESSOS_CMD_FLUSH_CONSOLE,
};

enum {
        EXPRESSOS_IPC = 2,
        EXPRESSOS_IPC_FLUSH_RET_QUEUE,
        EXPRESSOS_IPC_CMD,
};

/*
 * Tag to define which ABI is used in different variants of stat()
 * calls.
 */
enum {
        EXPRESSOS_STAT_STAT,
        EXPRESSOS_STAT_NEWSTAT,
        EXPRESSOS_STAT_STAT64,
        EXPRESSOS_STAT_LSTAT64,
        EXPRESSOS_STAT_COUNT,
};

#endif
