#ifndef EXPRESSOS_MM_H_
#define EXPRESSOS_MM_H_

#include <stddef.h>

#define STACK_AND_HEAP_SIZE (96 * 1024 * 1024)
#define STACK_SIZE          (128 * 1024)

/* Start of the main memory of ExpressOS */
extern void *g_stack_end;
extern char *g_stack_and_heap_start;
int init_mm(size_t size);
/* Wait for Linux to kickstart. It also gives ExpressOS the shared buffer. */
int init_shm(void);
void *gc_malloc(size_t size) __attribute__((malloc));

#endif
