#include "tls.h"

#include "expressos/expressos-native.h"
#include "expressos/string.h"
#include "expressos/errno.h"

#include <l4/sys/segment.h>

static int desc_empty(const struct desc_struct * desc);
static int LDT_empty(const struct user_desc * desc);
static int get_free_idx(const struct desc_struct * tls_array);
static void set_tls_desc(l4_cap_idx_t thread_id, struct desc_struct * tls_array,
                         int idx, const struct user_desc *info, int n);
static inline void native_load_tls(l4_cap_idx_t thread_id, struct desc_struct * tls_array);

static int l4api_fiasco_gdt_entry_offset;

static char slab_tls_array[PAGE_SIZE] __attribute__((aligned(PAGE_SIZE)));
static struct desc_struct *slab_tls_head;

#define TLS_SLAB_SIZE 256

static void slab_init(void)
{
        memset(slab_tls_array, 0, sizeof(slab_tls_array));
        slab_tls_head = (struct desc_struct *)slab_tls_array;

        char *start = slab_tls_array;
        while (start < slab_tls_array + sizeof(slab_tls_array)) {
                *(struct desc_struct**)start = (struct desc_struct *)(start + TLS_SLAB_SIZE);
                start += TLS_SLAB_SIZE;
        }
}

static struct desc_struct *slab_tls_malloc(void)
{
        if (!slab_tls_head)
                return NULL;
        
        struct desc_struct *old = slab_tls_head;
        slab_tls_head = *(struct desc_struct **)slab_tls_head;
        return old;
}

static void slab_tls_free(struct desc_struct *ptr)
{
        *(struct desc_struct **)ptr = slab_tls_head;
        slab_tls_head = ptr;
}


int init_l4api_tls(void)
{
        l4api_fiasco_gdt_entry_offset = fiasco_gdt_get_entry_offset(g_main_thread_tid, l4_utcb());
        slab_init();
        return 0;
}

struct desc_struct * l4api_tls_array_alloc(void)
{
        struct desc_struct *r = slab_tls_malloc();
        memset(r, 0, sizeof(struct desc_struct) * GDT_ENTRY_TLS_ENTRIES);
        return r;
}

void l4api_tls_array_free(struct desc_struct *ptr)
{
        slab_tls_free(ptr);
}

int l4api_set_thread_area(l4_cap_idx_t thread_id, struct desc_struct * tls_array,
                        int idx, struct user_desc * info,
                        int can_allocate)
{
        // printf("%lx: info->entry_number=%d, tls_array=%p, can_allocate=%d, info->base_addr=%x\n", thread_id, info->entry_number, tls_array, can_allocate, info->base_addr);

	if (idx == -1)
		idx = info->entry_number;

	/*
	 * index -1 means the kernel should try to find and
	 * allocate an empty descriptor:
	 */
	if (idx == -1 && can_allocate) {
		idx = get_free_idx(tls_array);
		if (idx < 0)
			return idx;

                info->entry_number = idx;
	}

	if (idx < GDT_ENTRY_TLS_MIN || idx > GDT_ENTRY_TLS_MAX)
		return -EINVAL;

	set_tls_desc(thread_id, tls_array, idx, info, 1);

	return 0;
}

static int desc_empty(const struct desc_struct *d)
{
        return d->a == 0 && d->b == 0;
}

static int LDT_empty(const struct user_desc *info)
{
        return ((info)->base_addr              == 0 &&
                (info)->limit                  == 0 &&
                (info)->contents               == 0 &&
                (info)->read_exec_only         == 1 &&
                (info)->seg_32bit              == 0 &&
                (info)->limit_in_pages         == 0 &&
                (info)->seg_not_present        == 1 &&
                (info)->useable                == 0);
}

static inline void fill_ldt(struct desc_struct *desc, const struct user_desc *info)
{
        desc->limit0            = info->limit & 0x0ffff;

        desc->base0             = (info->base_addr & 0x0000ffff);
        desc->base1             = (info->base_addr & 0x00ff0000) >> 16;

        desc->type              = (info->read_exec_only ^ 1) << 1;
        desc->type             |= info->contents << 2;

        desc->s                 = 1;
        desc->dpl               = 0x3;
        desc->p                 = info->seg_not_present ^ 1;
        desc->limit             = (info->limit & 0xf0000) >> 16;
        desc->avl               = info->useable;
        desc->d                 = info->seg_32bit;
        desc->g                 = info->limit_in_pages;

        desc->base2             = (info->base_addr & 0xff000000) >> 24;
        /*
         * Don't allow setting of the lm bit. It is useless anyway
         * because 64bit system calls require __USER_CS:
         */
        desc->l                 = 0;
}

/*
 * sys_alloc_thread_area: get a yet unused TLS descriptor index.
 */
static int get_free_idx(const struct desc_struct * tls_array)
{
	int idx;

	for (idx = 0; idx < GDT_ENTRY_TLS_ENTRIES; idx++)
		if (desc_empty(&tls_array[idx]))
			return idx + GDT_ENTRY_TLS_MIN;

	return -ESRCH;
}

static void set_tls_desc(l4_cap_idx_t thread_id, struct desc_struct * tls_array,
                         int idx, const struct user_desc *info, int n)
{
	struct desc_struct *desc = &tls_array[idx - GDT_ENTRY_TLS_MIN];
	while (n-- > 0) {
		if (LDT_empty(info))
                        memset(desc, 0, sizeof(struct desc_struct));
		else
			fill_ldt(desc, info);
		++info;
		++desc;
	}

        native_load_tls(thread_id, tls_array);
}

static inline void native_load_tls(l4_cap_idx_t thread_id, struct desc_struct * tls_array)
{
        fiasco_gdt_set(thread_id, tls_array,
                       3 * LDT_ENTRY_SIZE, 0, l4_utcb());
}
