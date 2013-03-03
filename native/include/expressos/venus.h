#ifndef __EXPRESSOS_VENUS_H__
#define __EXPRESSOS_VENUS_H__

#ifdef __KERNEL__
#include <linux/time.h>
#else
#include <sys/time.h>
#endif

#include <linux/ioctl.h>

struct expressos_venus_write_read {
        unsigned long write_size;	/* bytes to write */
	unsigned long write_consumed;	/* bytes consumed by driver */
	unsigned long write_buffer;
	unsigned long read_size;	/* bytes to read */
	unsigned long read_consumed;	/* bytes consumed by driver */
	unsigned long read_buffer;
};

#define EXPRESSOS_VENUS_WRITE_READ _IOWR('b', 1, struct expressos_venus_write_read)

/*
 * Commuincation messages between the venus and the kernel.
 *
 * Following the conventions of the Coda file system. ExpressOS
 * defines downcalls as calls from the venus to the kernel, where the
 * arguments are passed through the expressos_venus_{*}_in
 * structs. Similarly, ExpressOS defines upcalls as calls from the
 * kernel to the venus, where arguments are passed through the
 * expressos_venus_{*}_out structs.
 */
enum {
        EXPRESSOS_VENUS_REGISTER_HELPER = 1,
        EXPRESSOS_VENUS_CLOSE,
        EXPRESSOS_VENUS_PIPE,
        EXPRESSOS_VENUS_SOCKET,
        EXPRESSOS_VENUS_POLL,
        EXPRESSOS_VENUS_BINDER_WRITE_READ,
        EXPRESSOS_VENUS_ALIEN_MMAP2,
        EXPRESSOS_VENUS_FUTEX_WAIT,
        EXPRESSOS_VENUS_FUTEX_WAKE,
        EXPRESSOS_VENUS_CALL_COUNT,
};

struct expressos_venus_hdr {
        unsigned int opcode;
        unsigned int payload_size;
};

struct expressos_venus_register_helper_out {
        unsigned long binder_vm_start;
        int           workspace_fd;
        unsigned      workspace_size;
};

struct expressos_venus_open_in {
        int flags;
        int mode;
        unsigned int name_size;
        char name[0];
};

struct expressos_venus_close_in {
        int fd;
};

struct expressos_venus_pipe_out {
        int ret;
        int read_pipe;
        int write_pipe;
};

struct expressos_venus_socket_in {
        int domain;
        int type;
        int protocol;
};

struct expressos_venus_poll_in {
        unsigned long buffer;        /* Which completion buffer will be used */
        int nfds;
        int timeout;
        char fds[0];
};

struct expressos_venus_poll_out {
        unsigned long buffer;        /* Which completion buffer will be used */
        int ret;
        char fds[0];
};

struct expressos_venus_binder_write_read_in {
        unsigned long buffer;                /* Which completion buffer will be used */
        unsigned int  bwr_write_size;
        unsigned int  patch_table_entry_num;  /* patch table entries */
        unsigned int  patch_table_offset;
        unsigned int  payload_size;            /* total size of the marshaled result */
        char payload[0];
};

struct expressos_venus_binder_write_read_out {
        unsigned long buffer;                /* Which completion buffer will be used */
        int ret;
        int write_consumed;
        int read_consumed;
        unsigned int data_entries;           /* patch table entries */
        unsigned int payload_size;           /* total size of the marshaled result */
        char payload[0];
};

struct expressos_venus_alien_mmap2_in {
        unsigned long addr;
        unsigned length;
        int prot;
        int flags;
        int fd;
        unsigned pgoffset;
};

struct expressos_venus_futex_wait_in {
        int op;
        unsigned long uaddr;
        int val;
        struct timespec ts;
        int bitset;
};

struct expressos_venus_futex_wake_in {
        int op;
        unsigned long uaddr;
        int bitset;
};

struct expressos_venus_downcall {
        struct expressos_venus_hdr hdr;
        union {
                struct {
                        unsigned handle;
                        union {
                                long ret;
                                struct expressos_venus_poll_out poll;
                                struct expressos_venus_binder_write_read_out bwr;
                        };
                } async;

                long ret;
                struct expressos_venus_register_helper_out register_helper;
                struct expressos_venus_pipe_out pipe;
        };
};

struct expressos_venus_upcall {
        struct expressos_venus_hdr hdr;
        union {
                struct {
                        unsigned handle;
                        union {
                                struct expressos_venus_socket_in socket;
                                struct expressos_venus_poll_in poll;
                                struct expressos_venus_binder_write_read_in bwr;
                                struct expressos_venus_futex_wait_in futex_wait;
                                struct expressos_venus_futex_wake_in futex_wake;
                        };
                } async;
                struct expressos_venus_open_in open;
                struct expressos_venus_close_in close;
                struct expressos_venus_alien_mmap2_in mmap2;
        };
};

#endif
