/*
 * Adopted form
 *   http://www.ertos.nicta.com.au/research/l4.verified/document-recent.pdf
 * Veriﬁcation of the L4 Kernel Memory Allocator
 * Harvey Tuch Gerwin Klein Michael Norrish
 */

#include "expressos/mm.h"

typedef unsigned long word_t;
typedef void*         addr_t;

struct sel4_alloc {
    word_t *kmem_free_list;
};


struct sel4_alloc * sel4_alloc_new(void * start, void * end);
void sel4_alloc_free (struct sel4_alloc *this_, void * address, word_t size);
void * sel4_alloc_alloc (struct sel4_alloc *this_, word_t size);

#define CHUNK_SIZE 4096

struct sel4_alloc * sel4_alloc_new(void *start, void *end)
{
        /*
         * FIXME: Mark it as reachable
         */
	struct sel4_alloc *this_ = gc_malloc(sizeof(struct sel4_alloc));
	this_->kmem_free_list = 0;
	sel4_alloc_free(this_, start, (word_t)end - (word_t)start);
	return this_;	
}

void sel4_alloc_free(struct sel4_alloc *this_, void * address, word_t size)
{
	word_t* p;
	word_t* prev, *curr;
	size = size >= CHUNK_SIZE ? size : CHUNK_SIZE;
	for (p = (word_t*)address;
			p < ((word_t*)(((word_t)address) + size - (CHUNK_SIZE)));
			p = (word_t*) *p)
		*p = (word_t) p + (CHUNK_SIZE);

	for (prev = (word_t*) &this_->kmem_free_list, curr = this_->kmem_free_list;
			curr && (address > (void *)curr);
			prev = curr, curr = (word_t*) *curr)
		;
	*prev = (word_t) address; *p = (word_t) curr;
}

void * sel4_alloc_alloc(struct sel4_alloc *this_, word_t size)
{
	word_t* prev;
	word_t* curr;
	word_t* tmp;
	word_t i;
	size = size >= CHUNK_SIZE ? size : CHUNK_SIZE;
	for (prev = (word_t*) &this_->kmem_free_list, curr = this_->kmem_free_list;
			curr;
			prev = curr, curr = (word_t*) *curr)

	{
		if (!((word_t) curr & (size - 1)))
		{
			tmp = (word_t*) *curr;
			for (i = 1; tmp && (i < (size / (CHUNK_SIZE))); i++)
			{
				if ((word_t) tmp != ((word_t) curr + (CHUNK_SIZE)*i))
				{
					tmp = 0;
					break;
				};
				tmp = (word_t*) *tmp;
			}
			if (tmp)
			{
				*prev = (word_t) tmp;
				for (i = 0; i < (size / sizeof(word_t)); i++)
					curr[i] = 0;
				return curr;
			}
		}
	}
	return 0;
}
